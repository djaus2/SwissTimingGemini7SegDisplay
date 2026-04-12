using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Threading;

using SwissTimingDisplay.Models;
using SwissTimingDisplay.ViewModels;

namespace SwissTimingDisplay
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _vm = new MainViewModel();

        private readonly DispatcherTimer _wallClockTimer;
        private readonly DispatcherTimer _raceTimer;
        private bool _raceIsRunning = false;
        private TimeSpan _raceElapsed = TimeSpan.Zero;
        private bool _sendWallClockWhileRunning = false;
        private bool _raceHasStartedSinceReset = false;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = _vm;

            _vm.PropertyChanged += VmOnPropertyChanged;

            _wallClockTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1),
            };
            _wallClockTimer.Tick += (_, _) =>
            {
                if (_vm.UseWallClockTimeOfDay)
                {
                    _vm.TimeInput = DateTime.Now.ToString("HH:mm:ss");
                }
            };
            _wallClockTimer.Start();

            _raceTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1),
            };
            _raceTimer.Tick += async (_, _) =>
            {
                if (!_raceIsRunning)
                {
                    return;
                }

                if (_sendWallClockWhileRunning)
                {
                    _vm.TimeInput = DateTime.Now.ToString("HH:mm:ss");
                }
                else
                {
                    _raceElapsed = _raceElapsed.Add(TimeSpan.FromSeconds(1));
                    UpdateTimeInputFromRaceElapsed();
                }

                if (!_vm.IsConnected)
                {
                    return;
                }

                if (!TcpCommandDefinitions.Commands.TryGetValue(_vm.SelectedTcpCommand, out var charCommands))
                {
                    return;
                }

                var payload = BuildExpandedPayload(_vm.SelectedTcpCommand, charCommands, _sendWallClockWhileRunning);
                await _vm.SendRawAsync(payload);
                _vm.Status = $"Auto-sent {payload.Length} byte(s).";
            };
            _raceTimer.Start();

            UpdateRaceTimerButtonContent();
            UpdateWallClockEnabledState();
            UpdateSendEnabledState();
            UpdateRaceTimerEnabledState();
        }

        private void VmOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainViewModel.IsConnected)
                || e.PropertyName == nameof(MainViewModel.SelectedTcpCommand))
            {
                UpdateRaceTimerEnabledState();
                UpdateSendEnabledState();
            }
        }

        private void UseWallClock_Checked(object sender, RoutedEventArgs e)
        {
            _vm.TimeInput = DateTime.Now.ToString("HH:mm:ss");
            UpdateWallClockEnabledState();
            UpdateSendEnabledState();
        }

        private void UseWallClock_Unchecked(object sender, RoutedEventArgs e)
        {
            UpdateWallClockEnabledState();
            UpdateSendEnabledState();
        }

        protected override void OnClosed(EventArgs e)
        {
            _wallClockTimer.Stop();
            _raceTimer.Stop();
            _vm.PropertyChanged -= VmOnPropertyChanged;
            _vm.Dispose();
            base.OnClosed(e);
        }

        private void RaceTimerButton_Click(object sender, RoutedEventArgs e)
        {
            if (_raceIsRunning)
            {
                _raceIsRunning = false;
                _vm.Status = "Race timer stopped.";

                UpdateRaceTimerButtonContent();
                UpdateWallClockEnabledState();
                UpdateSendEnabledState();
                return;
            }

            if (_raceHasStartedSinceReset)
            {
                _raceElapsed = TimeSpan.Zero;
                _sendWallClockWhileRunning = false;
                _raceHasStartedSinceReset = false;
                UpdateTimeInputFromRaceElapsed();
                _vm.Status = "Race timer reset.";
                UpdateRaceTimerButtonContent();
                UpdateWallClockEnabledState();
                UpdateSendEnabledState();

                return;
            }

            _raceIsRunning = true;
            _sendWallClockWhileRunning = _vm.UseWallClockTimeOfDay;
            _raceHasStartedSinceReset = true;
            UpdateTimeInputFromRaceElapsed();
            _vm.Status = "Race timer started.";
            UpdateRaceTimerButtonContent();
            UpdateWallClockEnabledState();
            UpdateSendEnabledState();
        }

        private void StopRaceTimer()
        {
            if (!_raceIsRunning)
            {
                return;
            }

            _raceIsRunning = false;
            UpdateRaceTimerButtonContent();
            UpdateWallClockEnabledState();
            UpdateSendEnabledState();
        }

        private void UpdateRaceTimerButtonContent()
        {
            if (btnRaceTimer is null)
            {
                return;
            }

            if (_raceIsRunning)
            {
                btnRaceTimer.Content = "Stop";
                return;
            }

            btnRaceTimer.Content = _raceHasStartedSinceReset ? "Reset" : "Start";
        }

        private void UpdateWallClockEnabledState()
        {
            if (cbUseWallClock is null)
            {
                return;
            }

            if (btnRaceTimer?.Content is not string content)
            {
                cbUseWallClock.IsEnabled = false;
                return;
            }

            // Enabled only when race timer button is showing "Start".
            cbUseWallClock.IsEnabled = content == "Start";
        }

        private void UpdateSendEnabledState()
        {
            if (btnSend is null)
            {
                return;
            }

            // Disable Send after Start has been pressed until Reset returns to Start state.
            btnSend.IsEnabled = _vm.IsConnected && !_raceIsRunning && !_raceHasStartedSinceReset;
        }

        private void UpdateRaceTimerEnabledState()
        {
            if (btnRaceTimer is null)
            {
                return;
            }

            var enabled = _vm.IsConnected && _vm.SelectedTcpCommand == TcpCommand.RollerTimeofDayorRunningTime;
            btnRaceTimer.IsEnabled = enabled;

            if (!enabled && (_raceIsRunning || _raceHasStartedSinceReset))
            {
                ResetRaceTimerState();
            }
        }

        private void ResetRaceTimerState()
        {
            _raceIsRunning = false;
            _raceElapsed = TimeSpan.Zero;
            _sendWallClockWhileRunning = false;
            _raceHasStartedSinceReset = false;
            UpdateTimeInputFromRaceElapsed();
            UpdateRaceTimerButtonContent();
            UpdateWallClockEnabledState();
            UpdateSendEnabledState();
        }

        private void UpdateTimeInputFromRaceElapsed()
        {
            var hours = (int)_raceElapsed.TotalHours;
            var minutes = _raceElapsed.Minutes;
            var seconds = _raceElapsed.Seconds;

            if (hours > 0)
            {
                if (hours > 9)
                {
                    hours = 9;
                }

                _vm.TimeInput = $"{hours}:{minutes:00}:{seconds:00}.0";
                return;
            }

            var totalMinutes = (int)_raceElapsed.TotalMinutes;
            if (totalMinutes > 99)
            {
                totalMinutes = 99;
            }

            _vm.TimeInput = $"{totalMinutes:00}:{seconds:00}.00";
        }

        private byte[] BuildExpandedPayload(TcpCommand cmd, System.Collections.Generic.IReadOnlyList<CharCommand> charCommands, bool useWallClockTimeOfDay)
        {
            var needsTime = charCommands.Any(c => c == CharCommand.TIME);
            string timeDigits = string.Empty;

            if (needsTime)
            {
                if (useWallClockTimeOfDay)
                {
                    timeDigits = DateTime.Now.ToString("HHmmss");
                }
                else
                {
                    timeDigits = TimeStringHelper.GetSixDigitsOnly(_vm.TimeInput);
                }
            }

            var bibDigits = BibNoHelper.ToThreeDigits(_vm.BibNo);

            return charCommands.SelectMany((c, idx) =>
            {
                if (cmd == TcpCommand.RollerTimeofDayorRunningTime && idx == 3 && c == CharCommand.NNN)
                {
                    return bibDigits.Select(d => (byte)d);
                }

                if (c != CharCommand.TIME)
                {
                    return new[] { (byte)c };
                }

                return timeDigits.Select(d => (byte)d);
            }).ToArray();
        }

        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var cmd = _vm.SelectedTcpCommand;

                if (!TcpCommandDefinitions.Commands.TryGetValue(cmd, out var charCommands))
                {
                    MessageBox.Show(
                        $"No definition for '{cmd}'.",
                        "Payload",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                var originalLiterals = string.Join(", ", charCommands.Select(c => c.ToString()));

                var needsTime = charCommands.Any(c => c == CharCommand.TIME);
                TimeStringHelper.ParsedTime parsed = default;
                string timeDigits = string.Empty;
                string usedTimeStandard = string.Empty;
                var timeKind = TimeStringHelper.TimeKind.TimeOfDay;

                if (needsTime)
                {
                    if (_vm.UseWallClockTimeOfDay)
                    {
                        timeDigits = DateTime.Now.ToString("HHmmss");
                        usedTimeStandard = TimeStringHelper.ToTimeOfDayStandard(timeDigits);
                        timeKind = TimeStringHelper.TimeKind.TimeOfDay;
                    }
                    else
                    {
                        parsed = TimeStringHelper.ParseTimeInput(_vm.TimeInput);
                        timeDigits = parsed.SixDigits;
                        usedTimeStandard = parsed.Standard;
                        timeKind = parsed.Kind;
                    }
                }
                var bibDigits = BibNoHelper.ToThreeDigits(_vm.BibNo);

                var expandedLiterals = charCommands.SelectMany(c =>
                {
                    if (c != CharCommand.TIME)
                    {
                        return new[] { c.ToString() };
                    }

                    var label = timeKind == TimeStringHelper.TimeKind.TimeOfDay ? "TIME(HHMMSS)" : "TIME(mmsshh)";
                    return new[] { $"{label}={timeDigits}" };
                });

                var expandedBytes = BuildExpandedPayload(cmd, charCommands, _vm.UseWallClockTimeOfDay);

                if (cmd == TcpCommand.RollerTimeofDayorRunningTime
                    && charCommands.Count > 3
                    && charCommands[3] == CharCommand.NNN)
                {
                    expandedLiterals = charCommands.SelectMany((c, idx) =>
                    {
                        if (c == CharCommand.TIME)
                        {
                            var label = timeKind == TimeStringHelper.TimeKind.TimeOfDay ? "TIME(HHMMSS)" : "TIME(mmsshh)";
                            return new[] { $"{label}={timeDigits}" };
                        }

                        if (idx == 3 && c == CharCommand.NNN)
                        {
                            return new[] { $"NNN={bibDigits}" };
                        }

                        return new[] { c.ToString() };
                    });
                }

                var literals = string.Join(", ", expandedLiterals);
                var hex = BitConverter.ToString(expandedBytes);

                var timeLine = needsTime ? $"Time: {timeDigits} ({usedTimeStandard})\n\n" : string.Empty;

                var previewResult = MessageBox.Show(
                    $"{cmd}\n\n{timeLine}Original CharCommands:\n{originalLiterals}\n\nExpanded CharCommands:\n{literals}\n\nBytes:\n{hex}",
                    "Payload",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                if (_vm.IsConnected)
                {
                    var portName = _vm.ConnectedPortName ?? "(unknown)";
                    var send = MessageBox.Show(
                        $"Send {expandedBytes.Length} byte(s) to {portName}?",
                        "Send",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (send == MessageBoxResult.Yes)
                    {
                        await _vm.SendRawAsync(expandedBytes);
                        _vm.Status = $"Sent {expandedBytes.Length} byte(s) to {portName}.";
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }
}