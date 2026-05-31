using System.Windows;
using System.Windows.Input;

namespace QR_deFuzzer
{
    public partial class ThemedInfoWindow : Window
    {
        public ThemedInfoWindow(string title, string message)
        {
            InitializeComponent();
            Title = title;
            TitleTextBlock.Text = title;
            MessageTextBlock.Text = message;
        }

        public static void Show(string title, string message)
        {
            var window = new ThemedInfoWindow(title, message);
            window.ShowDialog();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
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
