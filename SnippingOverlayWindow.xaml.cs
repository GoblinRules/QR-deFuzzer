using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace QR_deFuzzer
{
    public partial class SnippingOverlayWindow : Window
    {
        private System.Windows.Point _startPoint;
        private bool _isDragging = false;
        private Rect _selectionRect;
        private System.Drawing.Rectangle _screenBounds;
        
        public string? DecodedText { get; private set; }
        public bool SnippedSuccessfully { get; private set; } = false;

        public SnippingOverlayWindow()
        {
            InitializeComponent();

            // Set window positioning and size to cover all monitors (Virtual Screen)
            _screenBounds = System.Windows.Forms.SystemInformation.VirtualScreen;
            this.Left = SystemParameters.VirtualScreenLeft;
            this.Top = SystemParameters.VirtualScreenTop;
            this.Width = SystemParameters.VirtualScreenWidth;
            this.Height = SystemParameters.VirtualScreenHeight;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            SelectionCanvas.Width = this.Width;
            SelectionCanvas.Height = this.Height;

            // Position the Info Tooltip in the upper-middle of the virtual screen
            Canvas.SetLeft(InfoTooltip, (this.Width - InfoTooltip.ActualWidth) / 2);
            Canvas.SetTop(InfoTooltip, 100);
            InfoTooltip.Visibility = Visibility.Visible;
            
            // Draw initial solid overlay
            UpdateOverlay(new Rect(0, 0, 0, 0));
            UpdateCrosshair(new System.Windows.Point(this.Width / 2, this.Height / 2));
            
            // Force focus to capture Escape key
            this.Focus();
        }

        private void UpdateOverlay(Rect selectionRect)
        {
            var group = new GeometryGroup { FillRule = FillRule.EvenOdd };
            group.Children.Add(new RectangleGeometry(new Rect(0, 0, this.Width, this.Height)));
            group.Children.Add(new RectangleGeometry(selectionRect));
            OverlayPath.Data = group;
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                _isDragging = true;
                _startPoint = e.GetPosition(SelectionCanvas);
                
                SelectionGlowBorder.Visibility = Visibility.Visible;
                SelectionBorder.Visibility = Visibility.Visible;
                Canvas.SetLeft(SelectionGlowBorder, _startPoint.X);
                Canvas.SetTop(SelectionGlowBorder, _startPoint.Y);
                Canvas.SetLeft(SelectionBorder, _startPoint.X);
                Canvas.SetTop(SelectionBorder, _startPoint.Y);
                SelectionGlowBorder.Width = 0;
                SelectionGlowBorder.Height = 0;
                SelectionBorder.Width = 0;
                SelectionBorder.Height = 0;
                
                InfoTooltip.Visibility = Visibility.Collapsed;
            }
            else if (e.ChangedButton == MouseButton.Right)
            {
                CancelSnipping();
            }
        }

        private void Window_MouseMove(object sender, MouseEventArgs e)
        {
            System.Windows.Point currentPoint = e.GetPosition(SelectionCanvas);
            UpdateCrosshair(currentPoint);

            if (_isDragging)
            {
                double x = Math.Min(_startPoint.X, currentPoint.X);
                double y = Math.Min(_startPoint.Y, currentPoint.Y);
                double width = Math.Abs(_startPoint.X - currentPoint.X);
                double height = Math.Abs(_startPoint.Y - currentPoint.Y);

                x = Math.Max(0, x);
                y = Math.Max(0, y);
                width = Math.Min(this.Width - x, width);
                height = Math.Min(this.Height - y, height);

                _selectionRect = new Rect(x, y, width, height);

                Canvas.SetLeft(SelectionGlowBorder, x);
                Canvas.SetTop(SelectionGlowBorder, y);
                SelectionGlowBorder.Width = width;
                SelectionGlowBorder.Height = height;

                Canvas.SetLeft(SelectionBorder, x);
                Canvas.SetTop(SelectionBorder, y);
                SelectionBorder.Width = width;
                SelectionBorder.Height = height;

                UpdateOverlay(_selectionRect);
            }
        }

        private void Window_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left && _isDragging)
            {
                _isDragging = false;
                SelectionGlowBorder.Visibility = Visibility.Collapsed;
                SelectionBorder.Visibility = Visibility.Collapsed;

                if (_selectionRect.Width > 5 && _selectionRect.Height > 5)
                {
                    DecodeSelectedArea();
                }
                else
                {
                    CancelSnipping();
                }
            }
        }

        private void DecodeSelectedArea()
        {
            try
            {
                Matrix transformToDevice = PresentationSource.FromVisual(this)?.CompositionTarget?.TransformToDevice
                    ?? Matrix.Identity;

                System.Windows.Point selectionTopLeft = transformToDevice.Transform(new System.Windows.Point(_selectionRect.Left, _selectionRect.Top));
                System.Windows.Point selectionBottomRight = transformToDevice.Transform(new System.Windows.Point(_selectionRect.Right, _selectionRect.Bottom));
                System.Windows.Point windowTopLeft = transformToDevice.Transform(new System.Windows.Point(Left, Top));

                int screenX = (int)Math.Round(windowTopLeft.X + Math.Min(selectionTopLeft.X, selectionBottomRight.X));
                int screenY = (int)Math.Round(windowTopLeft.Y + Math.Min(selectionTopLeft.Y, selectionBottomRight.Y));
                int cropWidth = (int)Math.Round(Math.Abs(selectionBottomRight.X - selectionTopLeft.X));
                int cropHeight = (int)Math.Round(Math.Abs(selectionBottomRight.Y - selectionTopLeft.Y));

                int padding = Math.Max(24, Math.Min(cropWidth, cropHeight) / 8);
                screenX -= padding;
                screenY -= padding;
                cropWidth += padding * 2;
                cropHeight += padding * 2;

                screenX = Math.Max(_screenBounds.Left, Math.Min(_screenBounds.Right - 1, screenX));
                screenY = Math.Max(_screenBounds.Top, Math.Min(_screenBounds.Bottom - 1, screenY));
                cropWidth = Math.Max(1, Math.Min(_screenBounds.Right - screenX, cropWidth));
                cropHeight = Math.Max(1, Math.Min(_screenBounds.Bottom - screenY, cropHeight));

                AppLogger.Info($"Capturing selected crop: {screenX},{screenY} {cropWidth}x{cropHeight}. Window={Left},{Top}. Dpi={transformToDevice.M11},{transformToDevice.M22}. Selection={_selectionRect}");

                HideCaptureChrome();

                using (Bitmap croppedBitmap = new Bitmap(cropWidth, cropHeight, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
                {
                    using Graphics graphics = Graphics.FromImage(croppedBitmap);
                    graphics.CopyFromScreen(screenX, screenY, 0, 0, new System.Drawing.Size(cropWidth, cropHeight), CopyPixelOperation.SourceCopy);
                    SaveLastSnip(croppedBitmap);
                    DecodedText = QrDecoder.Decode(croppedBitmap);
                }

                SnippedSuccessfully = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error cropping or decoding selected area: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                CancelSnipping();
            }
        }

        private void CancelSnipping()
        {
            DecodedText = null;
            SnippedSuccessfully = false;
            Close();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                CancelSnipping();
            }
        }

        private void UpdateCrosshair(System.Windows.Point point)
        {
            const double arm = 14;

            SetLine(CrosshairVerticalShadow, point.X, point.Y - arm, point.X, point.Y + arm);
            SetLine(CrosshairVertical, point.X, point.Y - arm, point.X, point.Y + arm);
            SetLine(CrosshairHorizontalShadow, point.X - arm, point.Y, point.X + arm, point.Y);
            SetLine(CrosshairHorizontal, point.X - arm, point.Y, point.X + arm, point.Y);
        }

        private static void SetLine(System.Windows.Shapes.Line line, double x1, double y1, double x2, double y2)
        {
            line.X1 = x1;
            line.Y1 = y1;
            line.X2 = x2;
            line.Y2 = y2;
        }

        private void HideCaptureChrome()
        {
            Opacity = 0;
            OverlayPath.Visibility = Visibility.Collapsed;
            CrosshairVerticalShadow.Visibility = Visibility.Collapsed;
            CrosshairHorizontalShadow.Visibility = Visibility.Collapsed;
            CrosshairVertical.Visibility = Visibility.Collapsed;
            CrosshairHorizontal.Visibility = Visibility.Collapsed;
            SelectionGlowBorder.Visibility = Visibility.Collapsed;
            SelectionBorder.Visibility = Visibility.Collapsed;
            InfoTooltip.Visibility = Visibility.Collapsed;

            Dispatcher.Invoke(() => { }, DispatcherPriority.Render);
            Thread.Sleep(160);
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
    }
}
