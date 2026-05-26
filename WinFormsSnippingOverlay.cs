using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
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
    internal sealed class WinFormsSnippingOverlay : IDisposable
    {
        private readonly List<MonitorOverlayForm> _forms = new();
        private readonly DrawingRectangle _virtualScreen;
        private MonitorOverlayForm? _primaryForm;
        private bool _isDragging;
        private bool _isFinished;
        private DrawingPoint _startScreenPoint;
        private DrawingPoint _currentScreenPoint;

        public string? DecodedText { get; private set; }
        public bool SnippedSuccessfully { get; private set; }

        public WinFormsSnippingOverlay()
        {
            System.Windows.Forms.Screen[] screens = System.Windows.Forms.Screen.AllScreens;
            _virtualScreen = screens
                .Select(screen => screen.Bounds)
                .Aggregate(DrawingRectangle.Union);

            AppLogger.Info($"Detected {screens.Length} monitor(s). Virtual bounds: {_virtualScreen.Left},{_virtualScreen.Top} {_virtualScreen.Width}x{_virtualScreen.Height}");
            foreach (System.Windows.Forms.Screen screen in screens)
            {
                AppLogger.Info($"Monitor: Device={screen.DeviceName}, Primary={screen.Primary}, Bounds={screen.Bounds}, WorkingArea={screen.WorkingArea}");
                _forms.Add(new MonitorOverlayForm(this, screen));
            }
        }

        public System.Windows.Forms.DialogResult ShowDialog()
        {
            if (_forms.Count == 0)
            {
                return System.Windows.Forms.DialogResult.Cancel;
            }

            _primaryForm = _forms.FirstOrDefault(form => form.Screen.Primary) ?? _forms[0];

            foreach (MonitorOverlayForm form in _forms)
            {
                if (!ReferenceEquals(form, _primaryForm))
                {
                    form.Show(_primaryForm);
                }
            }

            return _primaryForm.ShowDialog();
        }

        public void Dispose()
        {
            foreach (MonitorOverlayForm form in _forms)
            {
                form.Dispose();
            }

            _forms.Clear();
        }

        private void BeginSelection(DrawingPoint screenPoint)
        {
            if (_isFinished)
            {
                return;
            }

            _isDragging = true;
            _startScreenPoint = screenPoint;
            _currentScreenPoint = screenPoint;
            InvalidateAll();
        }

        private void MoveSelection(DrawingPoint screenPoint)
        {
            _currentScreenPoint = screenPoint;
            InvalidateAll();
        }

        private void EndSelection(DrawingPoint screenPoint)
        {
            if (!_isDragging || _isFinished)
            {
                return;
            }

            _isDragging = false;
            _currentScreenPoint = screenPoint;

            DrawingRectangle selection = GetSelectionRectangle();
            if (selection.Width <= 5 || selection.Height <= 5)
            {
                Cancel();
                return;
            }

            DecodeSelectedArea(selection);
        }

        private DrawingRectangle GetSelectionRectangle()
        {
            int x = Math.Min(_startScreenPoint.X, _currentScreenPoint.X);
            int y = Math.Min(_startScreenPoint.Y, _currentScreenPoint.Y);
            int width = Math.Abs(_startScreenPoint.X - _currentScreenPoint.X);
            int height = Math.Abs(_startScreenPoint.Y - _currentScreenPoint.Y);
            return new DrawingRectangle(x, y, width, height);
        }

        private void DecodeSelectedArea(DrawingRectangle selection)
        {
            try
            {
                DrawingRectangle captureRect = AddPadding(selection);
                captureRect.Intersect(_virtualScreen);
                AppLogger.Info($"Capturing selected crop: {captureRect.X},{captureRect.Y} {captureRect.Width}x{captureRect.Height}. Selection={selection}");

                HideAll();
                System.Windows.Forms.Application.DoEvents();
                Thread.Sleep(140);

                using var croppedBitmap = new DrawingBitmap(captureRect.Width, captureRect.Height, PixelFormat.Format32bppArgb);
                using (DrawingGraphics graphics = DrawingGraphics.FromImage(croppedBitmap))
                {
                    graphics.CopyFromScreen(captureRect.Left, captureRect.Top, 0, 0, captureRect.Size, CopyPixelOperation.SourceCopy);
                }

                SaveLastSnip(croppedBitmap);
                DecodedText = QrDecoder.Decode(croppedBitmap);
                SnippedSuccessfully = true;
                Finish(System.Windows.Forms.DialogResult.OK);
            }
            catch (Exception ex)
            {
                AppLogger.Error("Error cropping or decoding selected area.", ex);
                System.Windows.MessageBox.Show($"Error cropping or decoding selected area: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                Cancel();
            }
        }

        private static DrawingRectangle AddPadding(DrawingRectangle selection)
        {
            int padding = Math.Max(24, Math.Min(selection.Width, selection.Height) / 8);
            return new DrawingRectangle(
                selection.Left - padding,
                selection.Top - padding,
                selection.Width + padding * 2,
                selection.Height + padding * 2);
        }

        private static void SaveLastSnip(DrawingBitmap bitmap)
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

        private void Cancel()
        {
            DecodedText = null;
            SnippedSuccessfully = false;
            Finish(System.Windows.Forms.DialogResult.Cancel);
        }

        private void Finish(System.Windows.Forms.DialogResult result)
        {
            if (_isFinished)
            {
                return;
            }

            _isFinished = true;

            foreach (MonitorOverlayForm form in _forms)
            {
                if (form.IsDisposed)
                {
                    continue;
                }

                if (ReferenceEquals(form, _primaryForm))
                {
                    form.DialogResult = result;
                }
                else
                {
                    form.Close();
                }
            }
        }

        private void HideAll()
        {
            foreach (MonitorOverlayForm form in _forms)
            {
                if (!form.IsDisposed)
                {
                    form.Opacity = 0;
                    form.Hide();
                }
            }
        }

        private void InvalidateAll()
        {
            foreach (MonitorOverlayForm form in _forms)
            {
                if (!form.IsDisposed)
                {
                    form.Invalidate();
                }
            }
        }

        private sealed class MonitorOverlayForm : System.Windows.Forms.Form
        {
            private readonly WinFormsSnippingOverlay _owner;

            public System.Windows.Forms.Screen Screen { get; }

            public MonitorOverlayForm(WinFormsSnippingOverlay owner, System.Windows.Forms.Screen screen)
            {
                _owner = owner;
                Screen = screen;

                StartPosition = System.Windows.Forms.FormStartPosition.Manual;
                Bounds = screen.Bounds;
                FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
                ShowInTaskbar = false;
                TopMost = true;
                KeyPreview = true;
                DoubleBuffered = true;
                Cursor = System.Windows.Forms.Cursors.Cross;
                BackColor = DrawingColor.Black;
                Opacity = 0.32;
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

                DrawReticle(e.Graphics, PointToClient(System.Windows.Forms.Cursor.Position));

                if (_owner._isDragging)
                {
                    DrawSelection(e.Graphics);
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
                    _owner.Cancel();
                    return;
                }

                if (e.Button == System.Windows.Forms.MouseButtons.Left)
                {
                    Capture = true;
                    _owner.BeginSelection(PointToScreen(e.Location));
                }
            }

            protected override void OnMouseMove(System.Windows.Forms.MouseEventArgs e)
            {
                base.OnMouseMove(e);
                _owner.MoveSelection(PointToScreen(e.Location));
            }

            protected override void OnMouseUp(System.Windows.Forms.MouseEventArgs e)
            {
                base.OnMouseUp(e);

                if (e.Button == System.Windows.Forms.MouseButtons.Left)
                {
                    Capture = false;
                    _owner.EndSelection(PointToScreen(e.Location));
                }
            }

            protected override void OnKeyDown(System.Windows.Forms.KeyEventArgs e)
            {
                base.OnKeyDown(e);
                if (e.KeyCode == System.Windows.Forms.Keys.Escape)
                {
                    _owner.Cancel();
                }
            }

            private void DrawSelection(DrawingGraphics graphics)
            {
                DrawingRectangle screenSelection = _owner.GetSelectionRectangle();
                DrawingRectangle monitorSelection = DrawingRectangle.Intersect(screenSelection, Bounds);
                if (monitorSelection.IsEmpty)
                {
                    return;
                }

                DrawingPoint clientTopLeft = PointToClient(monitorSelection.Location);
                var clientSelection = new DrawingRectangle(clientTopLeft.X, clientTopLeft.Y, monitorSelection.Width, monitorSelection.Height);

                using var glowPen = new DrawingPen(DrawingColor.FromArgb(255, 0, 229, 255), 6);
                using var whitePen = new DrawingPen(DrawingColor.White, 2);
                graphics.DrawRectangle(glowPen, clientSelection);
                graphics.DrawRectangle(whitePen, clientSelection);
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
        }
    }
}
