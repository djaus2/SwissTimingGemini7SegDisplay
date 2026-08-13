using System.Windows;
using System.Windows.Controls;
using SwissTimingDisplay.ViewModels;

namespace SwissTimingDisplay.Controls
{
    public partial class SprintToolbar : UserControl
    {
        public static readonly DependencyProperty DisplayControlProperty =
            DependencyProperty.Register(
                nameof(DisplayControl),
                typeof(SwissTimingDisplayControl),
                typeof(SprintToolbar));

        public SwissTimingDisplayControl? DisplayControl
        {
            get => (SwissTimingDisplayControl?)GetValue(DisplayControlProperty);
            set => SetValue(DisplayControlProperty, value);
        }

        public static readonly DependencyProperty GaugeControlProperty =
            DependencyProperty.Register(
                nameof(GaugeControl),
                typeof(SiriccoWindGaugeControl),
                typeof(SprintToolbar));

        public SiriccoWindGaugeControl? GaugeControl
        {
            get => (SiriccoWindGaugeControl?)GetValue(GaugeControlProperty);
            set => SetValue(GaugeControlProperty, value);
        }

        public SprintToolbar()
        {
            InitializeComponent();
        }

        private void StartRaceButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                var metres = MainViewModel.GetSprintDistanceInMetres(vm.Sprint);
                bool startSiriccoWindGauge = (DisplayControl?.RaceIsRunning != true) &&
                                              (DisplayControl?.RaceHasStartedSinceReset != true) &&
                                              (metres > 0 && metres <= 110);
                bool resetSiriccoWindGauge = (DisplayControl?.RaceIsRunning != true) &&
                              (DisplayControl?.RaceHasStartedSinceReset == true) &&
                              (metres > 0 && metres <= 200);

                DisplayControl?.ToggleStart();

                // Only start the Wind Gauge if the race is not running and not reset and sprint distance is <=110m
                if (startSiriccoWindGauge)
                {
                        GaugeControl?.ToggleStart();
                }
                else if (resetSiriccoWindGauge)
                {
                    GaugeControl?.ToggleStart();
                }
            }
            else
            {
                DisplayControl?.ToggleStart();
            }

        }

        private void StartWindGaugeButton_Click(object sender, RoutedEventArgs e)
        {
            GaugeControl?.ToggleStart();
        }

        private void ToggleControls_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                vm.SprintShowSetup = !vm.SprintShowSetup;
            }
        }

        private void ShowSetup_Unchecked(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                vm.SprintShowSetup = false;
            }
        }

        private void ShowSetup_Checked(object sender, RoutedEventArgs e)
        {
            // Setup just reveals the Show/Hide Controls button; it does not show the toolbars.
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            var splash = new SwissTimingDisplay.SplashWindow();
            splash.Show();
            Window.GetWindow(this)?.Close();
        }

        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            //DisplayControl?.Exit();
            if (GaugeControl is not null)
            {
                GaugeControl.ViewModel?.BeginShutdown();
            }
            if (DisplayControl is not null)
            {
                DisplayControl.ViewModel?.BeginShutdown();
            }
            Application.Current.Shutdown();
        }
    }
}
