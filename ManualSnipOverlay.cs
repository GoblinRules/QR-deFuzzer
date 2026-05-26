using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;
using DrawingBitmap = System.Drawing.Bitmap;
using DrawingColor = System.Drawing.Color;
using DrawingFont = System.Drawing.Font;
using DrawingFontStyle = System.Drawing.FontStyle;
using DrawingGraphics = System.Drawing.Graphics;
using DrawingPen = System.Drawing.Pen;
using DrawingPoint = System.Drawing.Point;
using DrawingRectangle = System.Drawing.Rectangle;
using DrawingRectangleF = System.Drawing.RectangleF;
using DrawingSizeF = System.Drawing.SizeF;
using DrawingSolidBrush = System.Drawing.SolidBrush;

namespace QR_deFuzzer
{
    internal sealed class ManualSnipOverlay : System.Windows.Forms.Form
    {
        private readonly System.Windows.Forms.Screen _screen;
        private DrawingPoint _startPoint;
        private DrawingPoint _currentPoint;
        private bool _isDragging;

        public string? DecodedText { get; private set; }
        public bool SnippedSuccessfully { get; private set; }

        public ManualSnipOverlay(System.Windows.Forms.Screen screen)
        {
            _screen = screen;

            StartPosition = System.Windows.Forms.FormStartPosition.Manual;
            Bounds = screen.Bounds;
            FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            KeyPreview = true;
            DoubleBuffered = true;
            Cursor = System.Windows.Forms.Cursors.Cross;
            BackColor = DrawingColor.Black;
            Opacity = 0.18;

            AppLogger.Info($"Opening manual snip overlay on {screen.DeviceName}, Bounds={screen.Bounds}");
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            Activate();
            Focus();
        }

        protected override void OnPaint(System.Windows.Forms.PaintEventArgs e)
        {
            base.OnPaint(e);

            DrawReticle(e.Graphics, PointToClient(System.Windows.Forms.Cursor.Position));

            if (_isDragging)
            {
                DrawingRectangle selection = GetClientSelection();
                using var glowPen = new DrawingPen(DrawingColor.FromArgb(255, 0, 229, 255), 6);
                using var whitePen = new DrawingPen(DrawingColor.White, 2);
                e.Graphics.DrawRectangle(glowPen, selection);
                e.Graphics.DrawRectangle(whitePen, selection);
            }
            else
            {
                DrawHint(e.Graphics);
            }
        }

        protected override void OnMouseDown(System.Windows.Forms.MouseEventArgs e)
        {
            base.OnMouseDown(e);

            if (e.Button == System.Windows.Forms.MouseButtons.Right)
            {
                CancelSnipping();
                return;
            }

            if (e.Button == System.Windows.Forms.MouseButtons.Left)
            {
                _isDragging = true;
                _startPoint = e.Location;
                _currentPoint = e.Location;
                Invalidate();
            }
        }

        protected override void OnMouseMove(System.Windows.Forms.MouseEventArgs e)
        {
            base.OnMouseMove(e);
            _currentPoint = e.Location;
            Invalidate();
        }

        protected override void OnMouseUp(System.Windows.Forms.MouseEventArgs e)
        {
            base.OnMouseUp(e);

            if (e.Button != System.Windows.Forms.MouseButtons.Left || !_isDragging)
            {
                return;
            }

            _isDragging = false;
            _currentPoint = e.Location;

            DrawingRectangle selection = GetScreenSelection();
            if (selection.Width <= 5 || selection.Height <= 5)
            {
                CancelSnipping();
                return;
            }

            DecodeSelectedArea(selection);
        }

        protected override void OnKeyDown(System.Windows.Forms.KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == System.Windows.Forms.Keys.Escape)
            {
                CancelSnipping();
            }
        }

        private void DecodeSelectedArea(DrawingRectangle selection)
        {
            try
            {
                DrawingRectangle captureRect = AddPadding(selection);
                captureRect.Intersect(_screen.Bounds);
                AppLogger.Info($"Manual snip capture: {captureRect.X},{captureRect.Y} {captureRect.Width}x{captureRect.Height}. Selection={selection}");

                Opacity = 0;
                Hide();
                System.Windows.Forms.Application.DoEvents();
                Thread.Sleep(120);

                using var bitmap = CaptureBitmap(captureRect);
                SaveLastSnip(bitmap);
                DecodedText = QrDecoder.Decode(bitmap);
                SnippedSuccessfully = true;
                DialogResult = System.Windows.Forms.DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                AppLogger.Error("Manual snip failed.", ex);
                System.Windows.MessageBox.Show($"Manual snip failed: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                CancelSnipping();
            }
        }

        private DrawingRectangle GetClientSelection()
        {
            int x = Math.Min(_startPoint.X, _currentPoint.X);
            int y = Math.Min(_startPoint.Y, _currentPoint.Y);
            int width = Math.Abs(_startPoint.X - _currentPoint.X);
            int height = Math.Abs(_startPoint.Y - _currentPoint.Y);
            return new DrawingRectangle(x, y, width, height);
        }

        private DrawingRectangle GetScreenSelection()
        {
            DrawingRectangle clientSelection = GetClientSelection();
            DrawingPoint screenTopLeft = PointToScreen(clientSelection.Location);
            return new DrawingRectangle(screenTopLeft.X, screenTopLeft.Y, clientSelection.Width, clientSelection.Height);
        }

        private static DrawingRectangle AddPadding(DrawingRectangle selection)
        {
            int padding = Math.Max(24, Math.Min(selection.Width, selection.Height) / 8);
            return new DrawingRectangle(selection.Left - padding, selection.Top - padding, selection.Width + padding * 2, selection.Height + padding * 2);
        }

        private static DrawingBitmap CaptureBitmap(DrawingRectangle bounds)
        {
            var bitmap = new DrawingBitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb);
            using DrawingGraphics graphics = DrawingGraphics.FromImage(bitmap);
            graphics.CopyFromScreen(bounds.Left, bounds.Top, 0, 0, bounds.Size, CopyPixelOperation.SourceCopy);
            return bitmap;
        }

        private static void SaveLastSnip(DrawingBitmap bitmap)
        {
            ScreenshotCache.Save(bitmap, "last-snip.png");
        }

        private static void DrawReticle(DrawingGraphics graphics, DrawingPoint point)
        {
            const int arm = 14;
            using var shadowPen = new DrawingPen(DrawingColor.Black, 4);
            using var reticlePen = new DrawingPen(DrawingColor.Yellow, 2);
            graphics.DrawLine(shadowPen, point.X, point.Y - arm, point.X, point.Y + arm);
            graphics.DrawLine(shadowPen, point.X - arm, point.Y, point.X + arm, point.Y);
            graphics.DrawLine(reticlePen, point.X, point.Y - arm, point.X, point.Y + arm);
            graphics.DrawLine(reticlePen, point.X - arm, point.Y, point.X + arm, point.Y);
        }

        private static void DrawHint(DrawingGraphics graphics)
        {
            const string text = "QR-deFuzzer | Drag to select QR code | Esc or right-click to cancel";
            using var font = new DrawingFont("Segoe UI", 10, DrawingFontStyle.Bold);
            DrawingSizeF textSize = graphics.MeasureString(text, font);
            var box = new DrawingRectangleF(24, 24, textSize.Width + 24, textSize.Height + 16);
            using var backgroundBrush = new DrawingSolidBrush(DrawingColor.FromArgb(235, 16, 16, 21));
            using var textBrush = new DrawingSolidBrush(DrawingColor.White);
            graphics.FillRectangle(backgroundBrush, box);
            graphics.DrawString(text, font, textBrush, box.Left + 12, box.Top + 8);
        }

        private void CancelSnipping()
        {
            DecodedText = null;
            SnippedSuccessfully = false;
            DialogResult = System.Windows.Forms.DialogResult.Cancel;
            Close();
        }
    }
}
