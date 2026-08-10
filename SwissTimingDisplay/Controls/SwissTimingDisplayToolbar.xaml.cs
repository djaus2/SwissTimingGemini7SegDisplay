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
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            var splash = new SwissTimingDisplay.SplashWindow();
            splash.Show();
            Window.GetWindow(this)?.Close();
        }

        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            Control?.Exit();
        }
    }
}
