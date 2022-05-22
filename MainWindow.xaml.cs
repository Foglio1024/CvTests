using System.ComponentModel;
using System.Windows;

namespace CvTests
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new ViewModel();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);
            ((ViewModel)DataContext).StopCapture();
        }
    }
}
