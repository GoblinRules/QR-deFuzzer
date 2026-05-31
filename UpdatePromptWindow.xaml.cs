using System.Windows;
using System.Windows.Input;

namespace QR_deFuzzer
{
    public partial class UpdatePromptWindow : Window
    {
        public UpdatePromptWindow(string availableVersion, string currentVersion)
        {
            InitializeComponent();
            AvailableVersionTextBlock.Text = $"QR-deFuzzer v{availableVersion} is available.";
            CurrentVersionTextBlock.Text = $"Current version: v{currentVersion}";
        }

        private void DownloadButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }
    }
}
