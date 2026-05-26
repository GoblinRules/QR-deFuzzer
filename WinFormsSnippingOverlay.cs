using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;
using ZXing;

namespace QR_deFuzzer
{
    internal sealed class WinFormsSnippingOverlay : System.Windows.Forms.Form
    {
        private readonly System.Drawing.Rectangle _virtualScreen;
        private System.Drawing.Point _startScreenPoint;
        private System.Drawing.Point _currentScreenPoint;
        private bool _isDragging;

        public string? DecodedText { get; private set; }
        public bool SnippedSuccessfully { get; private set; }

        public WinFormsSnippingOverlay()
        {
            _virtualScreen = System.Windows.Forms.SystemInformation.VirtualScreen;

            StartPosition = System.Windows.Forms.FormStartPosition.Manual;
            Bounds = _virtualScreen;
            FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            KeyPreview = true;
            DoubleBuffered = true;
            Cursor = System.Windows.Forms.Cursors.Cross;
            BackColor = System.Drawing.Color.Black;
            Opacity = 0.32;

            AppLogger.Info($"WinForms snipping overlay bounds: {_virtualScreen.Left},{_virtualScreen.Top} {_virtualScreen.Width}x{_virtualScreen.Height}");
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

            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;

            System.Drawing.Point cursorClient = PointToClient(System.Windows.Forms.Cursor.Position);
            DrawReticle(e.Graphics, cursorClient);

            if (_isDragging)
            {
                System.Drawing.Rectangle selection = GetClientSelectionRectangle();
                using var glowPen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(255, 0, 229, 255), 6);
                using var whitePen = new System.Drawing.Pen(System.Drawing.Color.White, 2);
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

            if (e.Button != System.Windows.Forms.MouseButtons.Left)
            {
                return;
            }

            _isDragging = true;
            _startScreenPoint = System.Windows.Forms.Cursor.Position;
            _currentScreenPoint = _startScreenPoint;
            Invalidate();
        }

        protected override void OnMouseMove(System.Windows.Forms.MouseEventArgs e)
        {
            base.OnMouseMove(e);
            _currentScreenPoint = System.Windows.Forms.Cursor.Position;
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
            _currentScreenPoint = System.Windows.Forms.Cursor.Position;

            System.Drawing.Rectangle selection = GetScreenSelectionRectangle();
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

        private void DecodeSelectedArea(System.Drawing.Rectangle selection)
        {
            try
            {
                System.Drawing.Rectangle captureRect = AddPadding(selection);
                captureRect.Intersect(_virtualScreen);
                AppLogger.Info($"Capturing selected crop: {captureRect.X},{captureRect.Y} {captureRect.Width}x{captureRect.Height}. Selection={selection}");

                Opacity = 0;
                Hide();
                System.Windows.Forms.Application.DoEvents();
                Thread.Sleep(120);

                using var croppedBitmap = new Bitmap(captureRect.Width, captureRect.Height, PixelFormat.Format32bppArgb);
                using (Graphics graphics = Graphics.FromImage(croppedBitmap))
                {
                    graphics.CopyFromScreen(captureRect.Left, captureRect.Top, 0, 0, captureRect.Size, CopyPixelOperation.SourceCopy);
                }

                SaveLastSnip(croppedBitmap);
                DecodedText = QrDecoder.Decode(croppedBitmap);
                SnippedSuccessfully = true;
                DialogResult = System.Windows.Forms.DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                AppLogger.Error("Error cropping or decoding selected area.", ex);
                System.Windows.MessageBox.Show($"Error cropping or decoding selected area: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                CancelSnipping();
            }
        }

        private System.Drawing.Rectangle GetScreenSelectionRectangle()
        {
            int x = Math.Min(_startScreenPoint.X, _currentScreenPoint.X);
            int y = Math.Min(_startScreenPoint.Y, _currentScreenPoint.Y);
            int width = Math.Abs(_startScreenPoint.X - _currentScreenPoint.X);
            int height = Math.Abs(_startScreenPoint.Y - _currentScreenPoint.Y);
            return new System.Drawing.Rectangle(x, y, width, height);
        }

        private System.Drawing.Rectangle GetClientSelectionRectangle()
        {
            System.Drawing.Rectangle screenSelection = GetScreenSelectionRectangle();
            System.Drawing.Point clientTopLeft = PointToClient(screenSelection.Location);
            return new System.Drawing.Rectangle(clientTopLeft.X, clientTopLeft.Y, screenSelection.Width, screenSelection.Height);
        }

        private static System.Drawing.Rectangle AddPadding(System.Drawing.Rectangle selection)
        {
            int padding = Math.Max(24, Math.Min(selection.Width, selection.Height) / 8);
            return new System.Drawing.Rectangle(
                selection.Left - padding,
                selection.Top - padding,
                selection.Width + padding * 2,
                selection.Height + padding * 2);
        }

        private static void DrawReticle(System.Drawing.Graphics graphics, System.Drawing.Point point)
        {
            const int arm = 14;
            using var shadowPen = new System.Drawing.Pen(System.Drawing.Color.Black, 4);
            using var reticlePen = new System.Drawing.Pen(System.Drawing.Color.Yellow, 2);

            graphics.DrawLine(shadowPen, point.X, point.Y - arm, point.X, point.Y + arm);
            graphics.DrawLine(shadowPen, point.X - arm, point.Y, point.X + arm, point.Y);
            graphics.DrawLine(reticlePen, point.X, point.Y - arm, point.X, point.Y + arm);
            graphics.DrawLine(reticlePen, point.X - arm, point.Y, point.X + arm, point.Y);
        }

        private static void DrawHint(System.Drawing.Graphics graphics)
        {
            const string text = "QR-deFuzzer | Drag to select QR code | Esc or right-click to cancel";
            using var font = new System.Drawing.Font("Segoe UI", 10, System.Drawing.FontStyle.Bold);
            System.Drawing.SizeF textSize = graphics.MeasureString(text, font);
            var box = new System.Drawing.RectangleF(24, 24, textSize.Width + 24, textSize.Height + 16);
            using var backgroundBrush = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(235, 16, 16, 21));
            using var textBrush = new System.Drawing.SolidBrush(System.Drawing.Color.White);
            graphics.FillRectangle(backgroundBrush, box);
            graphics.DrawString(text, font, textBrush, box.Left + 12, box.Top + 8);
        }

        private static void SaveLastSnip(Bitmap bitmap)
        {
            try
            {
                string? directory = Path.GetDirectoryName(AppLogger.LogPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                    bitmap.Save(Path.Combine(directory, "last-snip.png"), ImageFormat.Png);
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error("Failed to save last snip image.", ex);
            }
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
