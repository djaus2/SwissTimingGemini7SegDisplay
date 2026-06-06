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
    public sealed partial class MainViewModel
    {
        private string _windGaugeDisplay = string.Empty;
        private string _windGaugeCaptureCountdownPeriodSecsStr = "10";
        private string _windGaugeCaptureCountsPerSecStr = "4";
        private int _windGaugeCaptureCountdownPeriodSecs = 10;
        private TimeSpan _lapContinueDelay = TimeSpan.FromSeconds(5);
        private int _windGaugeCaptureCountsPerSec = 4;

        // This gets sent to the display control
        public string WindGaugeDisplay
        {
            get => _windGaugeDisplay;
            set => SetProperty(ref _windGaugeDisplay, value);
        }

        
        public int WindGaugeCaptureCountdownPeriodSecs
        {
            get => _windGaugeCaptureCountdownPeriodSecs;
            set
            {
                if (SetProperty(ref _windGaugeCaptureCountdownPeriodSecs, value))
                {
                    if (value.ToString() != _windGaugeCaptureCountdownPeriodSecsStr)
                    {
                        _windGaugeCaptureCountdownPeriodSecsStr = value.ToString();
                    }
                    WindGauge.SiriccoWindGaugeAcquisitionDurationSecs = value;
                }
            }
        }



        public string WindGaugeCaptureCountdownPeriodSecsStr
        {
            get => _windGaugeCaptureCountdownPeriodSecsStr;
            set
            {
                if (SetProperty(ref _windGaugeCaptureCountdownPeriodSecsStr, value))
                {
                    // Update the static WindGauge.duration when the property changes
                    if (int.TryParse(value, out int duration) && duration != _windGaugeCaptureCountdownPeriodSecs)
                    {
                        WindGaugeCaptureCountdownPeriodSecs = duration;
                    }
                }
            }
        }

        
        public int WindGaugeCaptureCountsPerSec
        {
            get => _windGaugeCaptureCountsPerSec;
            set
            {
                if (SetProperty(ref _windGaugeCaptureCountsPerSec, value))
                {
                    if (value.ToString() != _windGaugeCaptureCountsPerSecStr)
                    {
                        WindGaugeCaptureCountsPerSecStr = value.ToString();
                    }
                    WindGauge.SiriccoWindGaugeCaptureCountsPerSec = value;
                    OnPropertyChanged(nameof(SiriccoWindGaugePeriodSec));
                    OnPropertyChanged(nameof(SiriccoWindGaugePeriodMs));
                    SiriccoWindGaugePeriodChanged?.Invoke();
                }
            }
        }

        public string WindGaugeCaptureCountsPerSecStr
        {
            get => _windGaugeCaptureCountsPerSecStr;
            set
            {
                if (SetProperty(ref _windGaugeCaptureCountsPerSecStr, value))
                {
                    // Update the static WindGauge.duration when the property changes
                    if (int.TryParse(value, out int counts) && counts != _windGaugeCaptureCountsPerSec)
                    {
                        WindGaugeCaptureCountsPerSec = counts;
                    }
                }
            }
        }

        private bool _showDecimalDot = false;
        public bool ShowDecimalDot
        {
            get => _showDecimalDot;
            set => SetProperty(ref _showDecimalDot, value);
        }

        public static class WindGauge
        {
            public const int AcquisitionDurationMin = 0;
            public const int AcquisitionDurationMax = 30;
            public const int SirricoAcquisitionDurationSecsDefault = 10; //Mistral uses this
            public const int SiriccoAcquisitionMeasurementsPerSecDefault = 4; //Siricco uses this and AcquisitionDurationSecsDefault

            // Mistral mode averages the wind speed over the acquisition duration and sends the result at the end of the acquisition duration.
            public static int SiriccoWindGaugeAcquisitionDurationSecs { get; set; } = SirricoAcquisitionDurationSecsDefault;

            // Simulator for Siricco mode tallies and averages the wind speed over the acquisition duration.
            // Total counts for the Siricco acquisition duration at the default rate would be 40 (10 secs * 4 counts/sec)
            public static int SiriccoWindGaugeCaptureCountsPerSec { get; set; } = SiriccoAcquisitionMeasurementsPerSecDefault;
            public static double WindSpeed { get; set; } = 0;

            private static DispatcherTimer? _timer;
            private static int _countdown;

            public static void Start(MainViewModel viewModel)
            {
                _countdown = SiriccoWindGaugeAcquisitionDurationSecs;
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
                        System.Diagnostics.Debug.WriteLine($"Simulated Wind Speed: {WindSpeed}");
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
            WindGauge.SiriccoWindGaugeAcquisitionDurationSecs = duration;
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

            // Sign: 11 for positive (space/off), 10 for negative (g segment on)
            byte signByte = speed < 0 ? (byte)'-': (byte)'+'; //'(byte)10 : (byte)11;
            if (speed < 0)
            {
                speed = -speed;
            }
            int locationOfS = 10; // Array.IndexOf(command, CharCommand.s);
            command[locationOfS++] = (CharCommand)signByte;

            // Extract digits directly from speed value
            int wholePart = (int)speed;
            int decimalPart = (int)((speed * 10) % 10);

            // First digit (tens place of whole part) - always send actual digit
            int d1 = wholePart / 10;
            command[locationOfS++] = TcpCommandDefinitions.GetCharCmdDigit(d1);
            // Second digit (ones place of whole part)
            command[locationOfS++] = TcpCommandDefinitions.GetCharCmdDigit(wholePart % 10);
            locationOfS++; // Skip dot position
            // Third digit (decimal place)
            command[locationOfS++] = TcpCommandDefinitions.GetCharCmdDigit(decimalPart);

            var csv = string.Join("_", command.Select(c => CharCommandToString(c)));
            csv = csv.Replace(" ", "SPC");
            csv = csv.Replace("_", "");
            SendStatus = $"Send: {csv}";

            // Send via receive port
            byte[] payload = command.Select(c => (byte)c).ToArray();
            SendRawAsyncReceive(payload);
        }

        private void SendCountdown(int count)
        {
            var cmd = TcpCommand.WindGauge_Output;
            var commandWindSeed = TcpCommandDefinitions.Commands[cmd];
            CharCommand[] command = new CharCommand[commandWindSeed.Count()];
            for (int i = 0; i < (commandWindSeed.Count()); i++)
            {
                command[i] = commandWindSeed[i];
            }

            int locationOfS = 10;
            command[locationOfS++] = CharCommand.Space; // No sign for countdown
            // Pad with zeros, always send actual digits
            string countStr = count.ToString("D3"); // Always 3 digits: "010", "009", etc.
            // First digit
            int d1 = int.Parse(countStr.Substring(0, 1));
            command[locationOfS++] = TcpCommandDefinitions.GetCharCmdDigit(d1);
            // Second digit
            int d2 = int.Parse(countStr.Substring(1, 1));
            command[locationOfS++] = TcpCommandDefinitions.GetCharCmdDigit(d2);
            locationOfS++; // Skip dot position
            // Third digit (decimal position)
            int d3 = int.Parse(countStr.Substring(2, 1));
            command[locationOfS++] = TcpCommandDefinitions.GetCharCmdDigit(d3);

            var csv = string.Join("_", command.Select(c => CharCommandToString(c)));
            csv = csv.Replace(" ", "SPC");
            csv = csv.Replace("_", "");
            Status = $"Countdown: {csv}";

            byte[] payload = command.Select(c => (byte)c).ToArray();
            SendRawAsyncReceive(payload);
        }

        public static string CharCommandToString(CharCommand cmd)
        {
            byte b = (byte)cmd;
            char ch = (char)cmd;
            if (char.IsControl(ch))
            {
                return '<' + cmd.ToString() + '>';
            }
            return ch.ToString();
        }

        private void UpdateSimulatedWindGaugeDisplayCount(int count)
        {
            // Always update display if DisplaySimulatorSpeed is true
            if (DisplaySimulatorSpeed)
            {
                ShowDecimalDot = false;
                WindGaugeDisplay = count.ToString().PadLeft(4, ' ');
            }
            else
            {
                // When DisplaySimulatorSpeed is false, send countdown via receive port
                SendCountdown(count);
            }
            string msg = $"Acquisition Duration: {count} seconds remaining";
            Status = msg;
        }

        /// <summary>
        /// UpdateSimulatedWindGaugeDisplaySpeed()
        /// if DisplaySimulatorSpeed is checked then update display direct without sending
        /// Transmision is internally in the app in this case
        /// Note that currently the both serial ports must be connected though.
        /// 2Do unwind this. Other comands still need serial.
        /// </summary>
        public void UpdateSimulatedWindGaugeDisplaySpeed()
        {
            // Only update display if DisplaySimulatorSpeed is true
            if (DisplaySimulatorSpeed)
            {
                ShowDecimalDot = true;
                // Format without decimal point in the string (separator dot will be shown separately)
                int wholePart = (int)Math.Abs(WindGauge.WindSpeed);
                int wholePart10 = wholePart / 10;
                int wholePart1 = wholePart%10;
                int decimalPart = (int)(Math.Abs(WindGauge.WindSpeed * 10) % 10);
                string sign = WindGauge.WindSpeed < 0 ? "-" : "+"; // + for positive (value 11), "-" for negative (value 10)

                // Format as "XXX" with leading space for sign (no decimal point in string)
                WindGaugeDisplay = $"{sign}{wholePart10:0}{wholePart1:0}{decimalPart}";//UpdateUpdate
            }
            else
            {
                SendResult();
            }
            Status = $"Wind Speed: {WindGauge.WindSpeed}";


            // Notify that measurement is complete
            WindGaugeMeasurementComplete?.Invoke(this, EventArgs.Empty);
        }

        public TimeSpan LapContinueDelay
        {
            get => _lapContinueDelay;
            set => SetProperty(ref _lapContinueDelay, value);
        }

        public double LapContinueDelaySeconds
        {
            get => _lapContinueDelay.TotalSeconds;
            set => LapContinueDelay = TimeSpan.FromSeconds(value);
        }

        public event EventHandler? WindGaugeMeasurementComplete;
    }
}
