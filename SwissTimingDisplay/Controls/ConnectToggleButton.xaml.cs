using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SwissTimingDisplay.Controls
{
    public partial class ConnectToggleButton : UserControl
    {
        public static readonly DependencyProperty CommandProperty =
            DependencyProperty.Register(
                nameof(Command),
                typeof(ICommand),
                typeof(ConnectToggleButton),
                new PropertyMetadata(null));

        public ICommand? Command
        {
            get => (ICommand?)GetValue(CommandProperty);
            set => SetValue(CommandProperty, value);
        }

        public static readonly DependencyProperty IsConnectedProperty =
            DependencyProperty.Register(
                nameof(IsConnected),
                typeof(bool),
                typeof(ConnectToggleButton),
                new PropertyMetadata(false));

        public bool IsConnected
        {
            get => (bool)GetValue(IsConnectedProperty);
            set => SetValue(IsConnectedProperty, value);
        }

        public ConnectToggleButton()
        {
            InitializeComponent();
        }
    }
}
