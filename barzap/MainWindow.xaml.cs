using Microsoft.Extensions.Logging;
using MahApps.Metro.Controls;
using barzap.Models;

namespace barzap {

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow {

        private readonly ILogger<MainWindow> _Logger;

        public MainWindow(ILogger<MainWindow> logger) {
            _Logger = logger;

            InitializeComponent();
        }

        private void Header_Selected(object sender, System.Windows.RoutedEventArgs e) {
            _Logger.LogInformation($"saving settings");
            Settings.Instance.SaveSettings();
        }

    }
}
