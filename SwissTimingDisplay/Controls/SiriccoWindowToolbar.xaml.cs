using System.Windows;
using System.Windows.Controls;

namespace SwissTimingDisplay.Controls
{
    public partial class SiriccoWindowToolbar : UserControl
    {
        public static readonly DependencyProperty SiriccoControlProperty =
            DependencyProperty.Register(
                nameof(SiriccoControl),
                typeof(SiriccoWindGaugeControl),
                typeof(SiriccoWindowToolbar));

        public SiriccoWindGaugeControl? SiriccoControl
        {
            get => (SiriccoWindGaugeControl?)GetValue(SiriccoControlProperty);
            set => SetValue(SiriccoControlProperty, value);
        }

        public SiriccoWindowToolbar()
        {
            InitializeComponent();
        }

        private SiriccoWindGaugeControl? Control => SiriccoControl;

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            Control?.ToggleStart();
        }

        private void ToggleControls_Click(object sender, RoutedEventArgs e)
        {
            if (Control == null) return;
            Control.ControlVisibility = Control.ControlVisibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            var splash = new SwissTimingDisplay.SplashWindow();
            splash.Show();
            Window.GetWindow(this)?.Close();
        }

        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            if (Control is { } control)
            {
                control.ViewModel?.BeginShutdown();
            }
            Application.Current.Shutdown();
        }
    }
}
