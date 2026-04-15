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

namespace SwissTimingDisplay
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _vm = new MainViewModel();

        private readonly DispatcherTimer _raceTimer;
        private readonly Stopwatch _raceStopwatch = new Stopwatch();
        private bool _raceIsRunning = false;
        private TimeSpan _raceElapsed = TimeSpan.Zero;
        private bool _sendWallClockWhileRunning = false;
        private bool _raceHasStartedSinceReset = false;
        private bool _autoSendInProgress = false;
        private static readonly TimeSpan RaceTimerInterval = TimeSpan.FromMilliseconds(200);

        public MainWindow()
        {
            InitializeComponent();
            DataContext = _vm;

            _vm.PropertyChanged += VmOnPropertyChanged;

            Loaded += (_, _) => ApplyAnchorLayout();
            SizeChanged += (_, _) => ApplyAnchorLayout();

            _raceTimer = new DispatcherTimer
            {
                Interval = RaceTimerInterval,
            };
            _raceTimer.Tick += (_, _) =>
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
                    _raceElapsed = _raceStopwatch.Elapsed;
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
                BeginAutoSend(payload);
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

            if (e.PropertyName == nameof(MainViewModel.AnchorDisplay))
            {
                ApplyAnchorLayout();
            }
        }

        private void ApplyAnchorLayout()
        {
            if (rightDisplayColumn is null
                || rightSevenSegBorder is null
                || rightSevenSegViewbox is null
                || rightSevenSegContent is null
                || bottomSevenSegBorder is null
                || bottomSevenSegViewbox is null
                || bottomSevenSegContent is null)
            {
                return;
            }

            if (!_vm.AnchorDisplay)
            {
                rightDisplayColumn.Width = new GridLength(0);
                rightSevenSegBorder.Visibility = Visibility.Collapsed;

                bottomSevenSegBorder.Visibility = Visibility.Visible;
                bottomSevenSegBorder.HorizontalAlignment = HorizontalAlignment.Left;
                bottomSevenSegBorder.VerticalAlignment = VerticalAlignment.Top;
                bottomSevenSegViewbox.HorizontalAlignment = HorizontalAlignment.Left;
                bottomSevenSegViewbox.VerticalAlignment = VerticalAlignment.Center;
                bottomSevenSegViewbox.Stretch = Stretch.None;
                bottomSevenSegViewbox.StretchDirection = StretchDirection.Both;
                return;
            }

            Dispatcher.BeginInvoke(() =>
            {
                if (!_vm.AnchorDisplay)
                {
                    return;
                }

                var scaleRight = EvaluateRightAnchorScale();
                var scaleBottom = EvaluateBottomAnchorScale();

                if (scaleRight >= scaleBottom)
                {
                    ShowRightAnchored();
                }
                else
                {
                    ShowBottomAnchored();
                }
            }, DispatcherPriority.Loaded);
        }

        private double EvaluateRightAnchorScale()
        {
            ShowRightAnchored();
            UpdateLayout();
            return ComputeUniformScale(rightSevenSegViewbox, rightSevenSegContent);
        }

        private double EvaluateBottomAnchorScale()
        {
            ShowBottomAnchored();
            UpdateLayout();
            return ComputeUniformScale(bottomSevenSegViewbox, bottomSevenSegContent);
        }

        private void ShowRightAnchored()
        {
            rightDisplayColumn.Width = GridLength.Auto;
            rightSevenSegBorder.Visibility = Visibility.Visible;
            rightSevenSegViewbox.Stretch = Stretch.Uniform;
            rightSevenSegViewbox.StretchDirection = StretchDirection.Both;

            bottomSevenSegBorder.Visibility = Visibility.Collapsed;
        }

        private void ShowBottomAnchored()
        {
            rightDisplayColumn.Width = new GridLength(0);
            rightSevenSegBorder.Visibility = Visibility.Collapsed;

            bottomSevenSegBorder.Visibility = Visibility.Visible;
            bottomSevenSegBorder.HorizontalAlignment = HorizontalAlignment.Stretch;
            bottomSevenSegBorder.VerticalAlignment = VerticalAlignment.Top;
            bottomSevenSegViewbox.HorizontalAlignment = HorizontalAlignment.Center;
            bottomSevenSegViewbox.VerticalAlignment = VerticalAlignment.Top;
            bottomSevenSegViewbox.Stretch = Stretch.Uniform;
            bottomSevenSegViewbox.StretchDirection = StretchDirection.Both;
        }

        private static double ComputeUniformScale(Viewbox viewbox, FrameworkElement child)
        {
            if (viewbox.ActualWidth <= 0 || viewbox.ActualHeight <= 0)
            {
                return 0;
            }

            child.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            var desired = child.DesiredSize;
            if (desired.Width <= 0 || desired.Height <= 0)
            {
                return 0;
            }

            return Math.Min(viewbox.ActualWidth / desired.Width, viewbox.ActualHeight / desired.Height);
        }

        private void UseWallClock_Checked(object sender, RoutedEventArgs e)
        {
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
                _raceStopwatch.Stop();
                if (!_sendWallClockWhileRunning)
                {
                    _raceElapsed = _raceStopwatch.Elapsed;
                    UpdateTimeInputFromRaceElapsed();
                }
                _vm.Status = "Race timer stopped.";

                UpdateRaceTimerButtonContent();
                UpdateWallClockEnabledState();
                UpdateSendEnabledState();

                return;
            }

            if (_raceHasStartedSinceReset)
            {
                _raceStopwatch.Reset();
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
            _raceStopwatch.Start();
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
            _raceStopwatch.Stop();
            if (!_sendWallClockWhileRunning)
            {
                _raceElapsed = _raceStopwatch.Elapsed;
                UpdateTimeInputFromRaceElapsed();
            }
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
            _raceStopwatch.Reset();
            _raceElapsed = TimeSpan.Zero;
            _sendWallClockWhileRunning = false;
            _raceHasStartedSinceReset = false;
            UpdateTimeInputFromRaceElapsed();

            UpdateRaceTimerButtonContent();
            UpdateWallClockEnabledState();
            UpdateSendEnabledState();
        }

        private async void BeginAutoSend(byte[] payload)
        {
            if (_autoSendInProgress)
            {
                return;
            }

            _autoSendInProgress = true;
            try
            {
                await _vm.SendRawAsync(payload);
            }
            catch (Exception ex)
            {
                _vm.Status = ex.Message;
            }
            finally
            {
                _autoSendInProgress = false;
            }
        }

        private void UpdateTimeInputFromRaceElapsed()
        {
            var hours = (int)_raceElapsed.TotalHours;
            var minutes = _raceElapsed.Minutes;
            var seconds = _raceElapsed.Seconds;
            var tenths = _raceElapsed.Milliseconds / 100;
            var hundredths = (_raceElapsed.Milliseconds / 10) % 100;

            if (hours > 0)
            {
                if (hours > 9)
                {
                    hours = 9;
                }

                _vm.TimeInput = $"{hours}:{minutes:00}:{seconds:00}.{tenths}";
                return;
            }

            var totalMinutes = (int)_raceElapsed.TotalMinutes;
            if (totalMinutes > 99)
            {
                totalMinutes = 99;
            }

            _vm.TimeInput = $"{totalMinutes:00}:{seconds:00}.{hundredths:00}";
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
                    var send = MessageBoxResult.Yes;
                    //MessageBox.Show(
                    //    $"Send {expandedBytes.Length} byte(s) to {portName}?",
                    //    "Send",
                    //    MessageBoxButton.YesNo,
                    //    MessageBoxImage.Question);

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