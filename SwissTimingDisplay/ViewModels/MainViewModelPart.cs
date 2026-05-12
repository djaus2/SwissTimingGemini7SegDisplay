using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

using SwissTimingDisplay.Models;
using SwissTimingDisplay.ViewModels;

namespace SwissTimingDisplay.ViewModels
{
    public sealed partial class MainViewModel : INotifyPropertyChanged, IDisposable
    {
        private string _windGaugeDisplay = string.Empty;
        private string _windGaugeCaptureCountdown = "10";
        public string WindGaugeDisplay
        {
            get => _windGaugeDisplay;
            set => Set(ref _windGaugeDisplay, value);
        }

        public string WindGaugeCaptureCountdown
        {
            get => _windGaugeCaptureCountdown;
            set
            {
                if (Set(ref _windGaugeCaptureCountdown, value))
                {
                    // Update the static WindGauge.duration when the property changes
                    if (int.TryParse(value, out int duration))
                    {
                        WindGauge.duration = duration;
                    }
                    // Save the setting only if not loading
                    if (!_isLoadingSettings)
                    {
                        SavePersistedPortNames();
                    }
                }
            }
        }

        private bool _showDecimalDot = false;
        public bool ShowDecimalDot
        {
            get => _showDecimalDot;
            set => Set(ref _showDecimalDot, value);
        }

        private static class WindGauge
        {
            public const int AcquisitionDurationMin = 0;
            public const int AcquisitionDurationMax = 30;
            public const int AcquisitionDurationDefault = 10;
            public static int duration { get; set; } = AcquisitionDurationDefault;

            public static double WindSpeed { get; set; } = 0;

            private static DispatcherTimer? _timer;
            private static int _countdown;

            public static void Start(MainViewModel viewModel)
            {
                _countdown = duration;
                _timer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(1)
                };
                _timer.Tick += (s, e) =>
                {
                    if (_countdown > 0)
                    {
                        viewModel.UpdateSimulatedWindGaugeDisplayCount(_countdown);
                        _countdown--;
                    }
                    else
                    {
                        _timer?.Stop();
                        WindSpeed = new Random().Next(-100, 100); // Simulate wind speed measurement
                        WindSpeed /= 10.0;
                        viewModel.UpdateSimulatedWindGaugeDisplaySpeed();
                    }
                };
                _timer.Start();
            }

            public static void Stop()
            {
                _timer?.Stop();
                WindSpeed = 0;
            }

            public static void ClearDisplay(MainViewModel viewModel)
            {
                viewModel.ShowDecimalDot = false;
                viewModel.WindGaugeDisplay = string.Empty;
            }

        }
        private void ProcessReceivedWindGaugeFrame(TcpCommand tcpCommand, List<char> chars)
        {
            switch (tcpCommand)
            {
                case TcpCommand.WindGauge_Acquisition_Duration:
                    // CWI Handle acquisition duration update if needed
                    int duration = 10;
                    if (chars?.Count == 2)
                    {
                        int durationTens = (byte)chars[0] - (byte)'0';
                        int durationUnits = (byte)chars[1] - (byte)'0';
                        if ((durationTens >= 0) &&
                            (durationTens < 3) &&
                            (durationUnits >= 0) &&
                            (durationTens < 10))
                        {
                            duration = durationTens * 10 + durationUnits;
                        }
                    }
                    WindGaugeSetAquisitionDuration(duration);
                    break;
                case TcpCommand.WindGauge_Start_of_Measurement:
                    // CWS Handle start of measurement if needed
                    WindGaugeStartMeasurement();
                    break;
                case TcpCommand.WindGauge_Reset_Stop_Clear:
                    // CWR Handle Reset(stop acquisition, clear the scoreboard)
                    WindGaugeResetStopClear();
                    break;
                case TcpCommand.WindGauge_Resend_Latest:
                    // CWO Handle resend latest if needed
                    WindGaugeResendLatest();
                    break;


            }
        }

        private void WindGaugeSetAquisitionDuration(int duration)
        {
            WindGauge.duration = duration;
        }

        private void WindGaugeResendLatest()
        {
            UpdateSimulatedWindGaugeDisplaySpeed();
        }

        private void WindGaugeResetStopClear()
        {
            WindGauge.Stop();
            WindGauge.ClearDisplay(this);
        }

        private void WindGaugeStartMeasurement()
        {
            WindGauge.Start(this);
        }

        private void SendResult()
        {
            double speed = WindGauge.WindSpeed;
            var cmd = TcpCommand.WindGauge_Output;
            var commandWindSeed = TcpCommandDefinitions.Commands[cmd];
            CharCommand[] command = new CharCommand[commandWindSeed.Count()] ;
            for (int i = 0; i < (commandWindSeed.Count()); i++) {
                command[i] = commandWindSeed[i];
            }            

            CharCommand sign = CharCommand.plus;
            if (speed < 0)
            {
                sign = CharCommand.minus;
                speed = -speed;
            }
            int locationOfS = 10; // Array.IndexOf(command, CharCommand.s);
            string speedStr = speed.ToString("F1").PadLeft(4, '0');
            command[locationOfS++] = sign;
            command[locationOfS++] = TcpCommandDefinitions.GetCharCmdDigit(int.Parse(speedStr.Substring(0, 1)));   // First digit
            command[locationOfS++] = TcpCommandDefinitions.GetCharCmdDigit(int.Parse(speedStr.Substring(1, 1)));
            locationOfS++;
            command[locationOfS++] = TcpCommandDefinitions.GetCharCmdDigit(int.Parse(speedStr.Substring(3, 1)));

            var msg = string.Join(",", command.Select(c => CharCommandToString(c)));
            Status = $"Command: {msg}";

            // Send via receive port
            byte[] payload = command.Select(c => (byte)c).ToArray();
            SendRawAsyncReceive(payload);
        }

        private string CharCommandToString(CharCommand cmd)
        {
            char ch = (char)cmd;
            if (char.IsControl(ch))
            {
                return cmd.ToString();
            }
            return ch.ToString();
        }

        private void UpdateSimulatedWindGaugeDisplayCount(int count)
        {
            // Only update display if DisplaySimulatorSpeed is true
            if (DisplaySimulatorSpeed)
            {
                ShowDecimalDot = false;
                WindGaugeDisplay = count.ToString().PadLeft(4, ' ');
            }
            string msg = $"Acquisition Duration: {count} seconds remaining";
            Status = msg;
        }

        private void UpdateSimulatedWindGaugeDisplaySpeed()
        {
            // Only update display if DisplaySimulatorSpeed is true
            if (DisplaySimulatorSpeed)
            {
                ShowDecimalDot = true;
                // Format without decimal point in the string (separator dot will be shown separately)
                int wholePart = (int)Math.Abs(WindGauge.WindSpeed);
                int decimalPart = (int)(Math.Abs(WindGauge.WindSpeed * 10) % 10);
                string sign = WindGauge.WindSpeed < 0 ? "-" : " ";

                // Format as "XXX" with leading space for sign (no decimal point in string)
                WindGaugeDisplay = $"{sign}{wholePart:00}{decimalPart}";
            }

            Status = $"Wind Speed: {WindGauge.WindSpeed}";
            SendResult();

            // Notify that measurement is complete
            WindGaugeMeasurementComplete?.Invoke(this, EventArgs.Empty);
        }

        public event EventHandler? WindGaugeMeasurementComplete;
    }
}
