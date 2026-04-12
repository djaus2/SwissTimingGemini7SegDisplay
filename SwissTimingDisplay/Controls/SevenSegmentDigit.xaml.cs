using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SwissTimingDisplay.Controls
{
    public partial class SevenSegmentDigit : UserControl
    {
        public static readonly DependencyProperty DigitProperty = DependencyProperty.Register(
            nameof(Digit),
            typeof(int),
            typeof(SevenSegmentDigit),
            new PropertyMetadata(0, OnDigitChanged));

        public int Digit
        {
            get => (int)GetValue(DigitProperty);
            set => SetValue(DigitProperty, value);
        }

        public SevenSegmentDigit()
        {
            InitializeComponent();
            UpdateSegments(Digit);
        }

        private static void OnDigitChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((SevenSegmentDigit)d).UpdateSegments((int)e.NewValue);
        }

        private void UpdateSegments(int digit)
        {
            var on = (Brush)Resources["SegmentOnBrush"];
            var off = (Brush)Resources["SegmentOffBrush"];

            if (digit < 0 || digit > 9)
            {
                SegA.Fill = off;
                SegB.Fill = off;
                SegC.Fill = off;
                SegD.Fill = off;
                SegE.Fill = off;
                SegF.Fill = off;
                SegG.Fill = off;
                return;
            }

            // Order: A, B, C, D, E, F, G
            bool[] seg = digit switch
            {
                0 => new[] { true, true, true, true, true, true, false },
                1 => new[] { false, true, true, false, false, false, false },
                2 => new[] { true, true, false, true, true, false, true },
                3 => new[] { true, true, true, true, false, false, true },
                4 => new[] { false, true, true, false, false, true, true },
                5 => new[] { true, false, true, true, false, true, true },
                6 => new[] { true, false, true, true, true, true, true },
                7 => new[] { true, true, true, false, false, false, false },
                8 => new[] { true, true, true, true, true, true, true },
                9 => new[] { true, true, true, true, false, true, true },
                _ => new[] { false, false, false, false, false, false, false },
            };

            SegA.Fill = seg[0] ? on : off;
            SegB.Fill = seg[1] ? on : off;
            SegC.Fill = seg[2] ? on : off;
            SegD.Fill = seg[3] ? on : off;
            SegE.Fill = seg[4] ? on : off;
            SegF.Fill = seg[5] ? on : off;
            SegG.Fill = seg[6] ? on : off;
        }
    }
}
