using System.Windows;
using SwissTimingDisplay.ViewModels;

namespace SwissTimingDisplay
{
    public partial class SplashWindow : Window
    {
        private static MainWindow? _mainWindow;
        private static AthsSprintWindow? _athsSprintWindow;
        private static MistralWindGaugeWindow? _mistralWindGaugeWindow;
        private static SiriccoWindowControlled? _siriccoControlledWindow;
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
            
            _mistralWindGaugeWindow = new MistralWindGaugeWindow(_vm);
            _mistralWindGaugeWindow.Show();
            this.Close();
        }


        private void SiriccoWindGaugeButtonControlled_Click(object sender, RoutedEventArgs e)
        {
            // Get or create shared instance
            _vm = MainViewModel.SharedInstance;

            // Set active window and show WindGauge
            _vm.CurrentWindow = MainViewModel.ActiveWindow.SiriccoWindowControlled;
            _vm.ShowWindGaugeWindow = true;

            _siriccoControlledWindow = new SiriccoWindowControlled(_vm);
            _siriccoControlledWindow.Show();
            this.Close();
        }

        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void SprintButton_Click(object sender, RoutedEventArgs e)
        {
            _vm = MainViewModel.SharedInstance;

            // Set active window and show Display
            _vm.CurrentWindow = MainViewModel.ActiveWindow.AthsSprint;
            _vm.ShowWindGaugeWindow = false;

            _athsSprintWindow = new AthsSprintWindow(_vm);
            _athsSprintWindow.Show();
            this.Close();   
        }
    }
}
