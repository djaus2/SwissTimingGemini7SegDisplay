using System.Windows;
using System.Windows.Controls;

namespace SwissTimingDisplay.Controls
{
    public partial class FourDigitSevenSegmentDisplay : UserControl
    {
        public static readonly DependencyProperty TimeInputProperty = DependencyProperty.Register(
            nameof(TimeInput),
            typeof(string),
            typeof(FourDigitSevenSegmentDisplay),
            new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty NumDigitsProperty = DependencyProperty.Register(
            nameof(NumDigits),
            typeof(int),
            typeof(FourDigitSevenSegmentDisplay),
            new PropertyMetadata(4));

        public static readonly DependencyProperty ShowDecimalDotProperty = DependencyProperty.Register(
            nameof(ShowDecimalDot),
            typeof(bool),
            typeof(FourDigitSevenSegmentDisplay),
            new PropertyMetadata(false));


        public string TimeInput
        {
            get => (string)GetValue(TimeInputProperty);
            set => SetValue(TimeInputProperty, value);
        }

        public int NumDigits
        {
            get => (int)GetValue(NumDigitsProperty);
            set => SetValue(NumDigitsProperty, value);
        }

        public bool ShowDecimalDot
        {
            get => (bool)GetValue(ShowDecimalDotProperty);
            set => SetValue(ShowDecimalDotProperty, value);
        }


        public FourDigitSevenSegmentDisplay()
        {
            InitializeComponent();
        }
    }
}
