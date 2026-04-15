using System.Windows;
using System.Windows.Controls;

namespace SwissTimingDisplay.Controls
{
    public partial class NineDigitSevenSegmentDisplay : UserControl
    {
        public static readonly DependencyProperty TimeInputProperty = DependencyProperty.Register(
            nameof(TimeInput),
            typeof(string),
            typeof(NineDigitSevenSegmentDisplay),
            new PropertyMetadata(string.Empty));

        public string TimeInput
        {
            get => (string)GetValue(TimeInputProperty);
            set => SetValue(TimeInputProperty, value);
        }

        public NineDigitSevenSegmentDisplay()
        {
            InitializeComponent();
        }
    }
}
