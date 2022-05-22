using Emgu.CV.Util;
using Nostrum.WPF.Extensions;
using System.ComponentModel;
using System.Drawing;
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
