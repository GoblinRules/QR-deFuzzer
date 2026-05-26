using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace QR_deFuzzer
{
    public partial class SnippingOverlayWindow : Window
    {
        private Bitmap? _fullScreenshot;
        private System.Windows.Point _startPoint;
        private bool _isDragging = false;
        private Rect _selectionRect;
        
        public string? DecodedText { get; private set; }
        public bool SnippedSuccessfully { get; private set; } = false;

        public SnippingOverlayWindow()
        {
            InitializeComponent();
            
            // Set window positioning and size to cover all monitors (Virtual Screen)
            this.Left = SystemParameters.VirtualScreenLeft;
            this.Top = SystemParameters.VirtualScreenTop;
            this.Width = SystemParameters.VirtualScreenWidth;
            this.Height = SystemParameters.VirtualScreenHeight;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            CaptureScreen();
            
            // Position the Info Tooltip in the upper-middle of the virtual screen
            Canvas.SetLeft(InfoTooltip, (this.Width - InfoTooltip.ActualWidth) / 2);
            Canvas.SetTop(InfoTooltip, 100);
            InfoTooltip.Visibility = Visibility.Visible;
            
            // Draw initial solid overlay
            UpdateOverlay(new Rect(0, 0, 0, 0));
            
            // Force focus to capture Escape key
            this.Focus();
        }

        private void CaptureScreen()
        {
            try
            {
                int left = (int)SystemParameters.VirtualScreenLeft;
                int top = (int)SystemParameters.VirtualScreenTop;
                int width = (int)SystemParameters.VirtualScreenWidth;
                int height = (int)SystemParameters.VirtualScreenHeight;

                double dpiScaleX = 1.0;
                double dpiScaleY = 1.0;
                var source = PresentationSource.FromVisual(this);
                if (source?.CompositionTarget != null)
                {
                    dpiScaleX = source.CompositionTarget.TransformToDevice.M11;
                    dpiScaleY = source.CompositionTarget.TransformToDevice.M22;
                }

                int physicalLeft = (int)(left * dpiScaleX);
                int physicalTop = (int)(top * dpiScaleY);
                int physicalWidth = (int)(width * dpiScaleX);
                int physicalHeight = (int)(height * dpiScaleY);

                _fullScreenshot = new Bitmap(physicalWidth, physicalHeight, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                using (Graphics g = Graphics.FromImage(_fullScreenshot))
                {
                    g.CopyFromScreen(physicalLeft, physicalTop, 0, 0, new System.Drawing.Size(physicalWidth, physicalHeight), CopyPixelOperation.SourceCopy);
                }

                BackgroundImage.Source = BitmapToImageSource(_fullScreenshot);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to capture screen: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
            }
        }

        private BitmapSource BitmapToImageSource(Bitmap bitmap)
        {
            using (MemoryStream memory = new MemoryStream())
            {
                bitmap.Save(memory, ImageFormat.Png);
                memory.Position = 0;
                BitmapImage bitmapImage = new BitmapImage();
                bitmapImage.BeginInit();
                bitmapImage.StreamSource = memory;
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.EndInit();
                bitmapImage.Freeze();
                return bitmapImage;
            }
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
                
                SelectionBorder.Visibility = Visibility.Visible;
                Canvas.SetLeft(SelectionBorder, _startPoint.X);
                Canvas.SetTop(SelectionBorder, _startPoint.Y);
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
            if (_isDragging)
            {
                System.Windows.Point currentPoint = e.GetPosition(SelectionCanvas);
                
                double x = Math.Min(_startPoint.X, currentPoint.X);
                double y = Math.Min(_startPoint.Y, currentPoint.Y);
                double width = Math.Abs(_startPoint.X - currentPoint.X);
                double height = Math.Abs(_startPoint.Y - currentPoint.Y);

                x = Math.Max(0, x);
                y = Math.Max(0, y);
                width = Math.Min(this.Width - x, width);
                height = Math.Min(this.Height - y, height);

                _selectionRect = new Rect(x, y, width, height);

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
                if (_fullScreenshot == null)
                {
                    CancelSnipping();
                    return;
                }

                double dpiScaleX = 1.0;
                double dpiScaleY = 1.0;
                var source = PresentationSource.FromVisual(this);
                if (source?.CompositionTarget != null)
                {
                    dpiScaleX = source.CompositionTarget.TransformToDevice.M11;
                    dpiScaleY = source.CompositionTarget.TransformToDevice.M22;
                }

                int cropX = (int)(_selectionRect.X * dpiScaleX);
                int cropY = (int)(_selectionRect.Y * dpiScaleY);
                int cropWidth = (int)(_selectionRect.Width * dpiScaleX);
                int cropHeight = (int)(_selectionRect.Height * dpiScaleY);

                cropX = Math.Max(0, Math.Min(_fullScreenshot.Width - 1, cropX));
                cropY = Math.Max(0, Math.Min(_fullScreenshot.Height - 1, cropY));
                cropWidth = Math.Max(1, Math.Min(_fullScreenshot.Width - cropX, cropWidth));
                cropHeight = Math.Max(1, Math.Min(_fullScreenshot.Height - cropY, cropHeight));

                using (Bitmap croppedBitmap = _fullScreenshot.Clone(new Rectangle(cropX, cropY, cropWidth, cropHeight), _fullScreenshot.PixelFormat))
                {
                    DecodedText = QrDecoder.Decode(croppedBitmap);
                }

                SnippedSuccessfully = true;
                this.DialogResult = true;
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
            try
            {
                this.DialogResult = false;
            }
            catch
            {
                // In case window is not shown as dialog
            }
            Close();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                CancelSnipping();
            }
        }
    }
}
