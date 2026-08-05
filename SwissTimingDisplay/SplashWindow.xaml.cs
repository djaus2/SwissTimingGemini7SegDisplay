using System.Windows;
using SwissTimingDisplay.ViewModels;

namespace SwissTimingDisplay
{
    public partial class SplashWindow : Window
    {
        private static MainWindow? _mainWindow;
        private static WindGaugeWindow? _windGaugeWindow;
        private static SiriccoWindowWG? _siriccoWindow;
        private MainViewModel? _vm;

        public bool useSiriccoSimulator
        {
            get => Properties.Settings.Default.useSiriccoSimulator;
            set
            {
                Properties.Settings.Default.useSiriccoSimulator = value;
                Properties.Settings.Default.Save();
            }
        }

        public SplashWindow()
        {
            InitializeComponent();
            // Don't initialize ViewModel in constructor - do it lazily
        }

        private void DisplayButton_Click(object sender, RoutedEventArgs e)
        {
            // Get or create shared instance
            _vm = MainViewModel.SharedInstance;
            
            // Set active window and show Display
            _vm.CurrentWindow = MainViewModel.ActiveWindow.Display;
            _vm.ShowWindGaugeWindow = false;
            
            _mainWindow = new MainWindow(_vm);
            _mainWindow.Show();
            this.Close();
        }

        private void WindGaugeButton_Click(object sender, RoutedEventArgs e)
        {
            // Get or create shared instance
            _vm = MainViewModel.SharedInstance;
            
            // Set active window and show WindGauge
            _vm.CurrentWindow = MainViewModel.ActiveWindow.WindGauge;
            _vm.ShowWindGaugeWindow = true;
            
            _windGaugeWindow = new WindGaugeWindow(_vm);
            _windGaugeWindow.Show();
            this.Close();
        }


        private void SiriccoWindGaugeButtonWG_Click(object sender, RoutedEventArgs e)
        {
            // Get or create shared instance
            _vm = MainViewModel.SharedInstance;

            // Set active window and show WindGauge
            _vm.CurrentWindow = MainViewModel.ActiveWindow.SiriccoWG;
            _vm.ShowWindGaugeWindow = true;

            _siriccoWindow = new SiriccoWindowWG(_vm);
            _siriccoWindow.Show();
            this.Close();
        }

        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }
    }
}
