using System.Windows;
using System.Windows.Controls;

namespace SwissTimingDisplay.Controls
{
    public partial class SwissTimingDisplayToolbar : UserControl
    {
        public static readonly DependencyProperty DisplayControlProperty =
            DependencyProperty.Register(
                nameof(DisplayControl),
                typeof(SwissTimingDisplayControl),
                typeof(SwissTimingDisplayToolbar));

        public SwissTimingDisplayControl? DisplayControl
        {
            get => (SwissTimingDisplayControl?)GetValue(DisplayControlProperty);
            set => SetValue(DisplayControlProperty, value);
        }

        public static readonly DependencyProperty SiriccoControlProperty =
            DependencyProperty.Register(
                nameof(SiriccoControl),
                typeof(SiriccoWindGaugeControl),
                typeof(SwissTimingDisplayToolbar));

        public SiriccoWindGaugeControl? SiriccoControl
        {
            get => (SiriccoWindGaugeControl?)GetValue(SiriccoControlProperty);
            set => SetValue(SiriccoControlProperty, value);
        }

        public static readonly DependencyProperty ShowBackExitProperty =
            DependencyProperty.Register(
                nameof(ShowBackExit),
                typeof(bool),
                typeof(SwissTimingDisplayToolbar),
                new PropertyMetadata(true));

        public bool ShowBackExit
        {
            get => (bool)GetValue(ShowBackExitProperty);
            set => SetValue(ShowBackExitProperty, value);
        }

        public SwissTimingDisplayToolbar()
        {
            InitializeComponent();
        }

        private SwissTimingDisplayControl? Control => DisplayControl;

 
   


        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            Control?.ToggleStart();
        }

        private void LapButton_Click(object sender, RoutedEventArgs e)
        {
            Control?.Lap();
        }

        private void ToggleControls_Click(object sender, RoutedEventArgs e)
        {
            if (Control == null) return;
            Control.ControlVisibility = Control.ControlVisibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
            if (Control.ControlVisibility == Visibility.Visible && SiriccoControl is not null)
            {
                SiriccoControl.ControlVisibility = Visibility.Collapsed;
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            var splash = new SwissTimingDisplay.SplashWindow();
            splash.Show();
            Window.GetWindow(this)?.Close();
        }

        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            Control?.ViewModel?.BeginShutdown();
            Application.Current.Shutdown();
        }


        /// ////////////////////////////////////////////////////////////////////////
    }
}
