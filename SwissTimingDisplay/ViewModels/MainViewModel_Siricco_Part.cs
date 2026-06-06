using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using SwissTimingDisplay.Models;

namespace SwissTimingDisplay.ViewModels
{
    public sealed partial class MainViewModel
    {
        private CancellationTokenSource? _siriccoReceiveCts;
        private Task? _siriccoReceiveTask;
        private readonly StringBuilder _siriccoLineBuffer = new StringBuilder();
        private DispatcherTimer? _simulatedSiriccoTimer;
        private readonly Random _simulatedRandom = new Random();

        public Action? WindGaugeStart;
        public Action? WindGaugeStop;

        public event Action<SiriccoData.SiriccoResult?>? SiriccoDataReceived;

        [ObservableProperty] private bool _siriccoIsRunning = false;

        [ObservableProperty] private bool _simulatedSiriccoWindGaugeRunning = false;

        public event Action? SiriccoWindGaugePeriodChanged;

        public TimeSpan SiriccoWindGaugePeriodSec
        {
            get => TimeSpan.FromSeconds(1.0 / WindGaugeCaptureCountsPerSec);
        }

        public int SiriccoWindGaugePeriodMs
        {
            get => (int) TimeSpan.FromSeconds(1.0 / WindGaugeCaptureCountsPerSec).TotalMilliseconds;
        }

        partial void OnSimulatedSiriccoWindGaugeRunningChanged(bool value)
        {
            if (value)
            {
                //if (!_vm.IsConnected)
                //{
                //    return;
                //}
                StartSimulatedSiriccoWindGauge();
            }
            else
            {
                StopSimulatedSiriccoWindGauge();
            }
        }

        void StartSimulatedSiriccoWindGauge()
        {
            //SimulatedSiriccoWindGaugeRunning = true;
            SendStatus = "Simulated Wind Gauge Started";
            WindGaugeStart?.Invoke();
        }


        void StopSimulatedSiriccoWindGauge()
        {
            //SimulatedSiriccoWindGaugeRunning = false;
            SendStatus = "Simulated Wind GaugeStopped";
            WindGaugeStop?.Invoke();
        }


        /// <summary>
        /// Connect receive port for Siricco mode using line-based reading
        /// </summary>
        private void ConnectReceiveSiricco(string portName, int baudRate = 9600)
        {
            Debug.WriteLine($"ConnectReceiveSiricco called with port: {portName}");
            
            DisconnectReceive();
            StopSiriccoReceiveLoop();
            _siriccoLineBuffer.Clear();

            var port = new SerialPort(portName, baudRate)
            {
                Parity = Parity.None,
                DataBits = 8,
                StopBits = StopBits.One,
                Handshake = Handshake.None,
                ReadTimeout = 100,
                WriteTimeout = 1000,
                NewLine = "\r\n"  // Ensure CR-LF line termination
            };

            port.Open();
            _receivePort = port;
            IsReceiveConnected = true;
            ConnectedReceivePortName = portName;
            OnPropertyChanged(nameof(DisplayTime));

            // Start the Siricco line-based receive loop
            _ = StartSiriccoReceiveLoopAsync(port);
        }

        /// <summary>
        /// Start the Siricco line-based receive loop using ReadLine() for CR-LF terminated strings
        /// </summary>
        private async Task StartSiriccoReceiveLoopAsync(SerialPort port)
        {
            _siriccoReceiveCts = new CancellationTokenSource();
            _siriccoReceiveTask = Task.Run(() => SiriccoReceiveLoopAsync(port, _siriccoReceiveCts.Token), _siriccoReceiveCts.Token);
        }

        int currentCount
        {
            get;
            set;
        } = 0;
        int capturesPerSec { get => WindGaugeCaptureCountsPerSec; }
        int AcquisitionPeriod { get => WindGaugeCaptureCountdownPeriodSecs; }

        public int CountDownToGoSecs()
        {
            if (currentCount <= 0)
            {
                return 0;
            }
            int toGoSecs = (currentCount + capturesPerSec / 2) / capturesPerSec;
            currentCount--;
            UpdateSimulatedWindGaugeDisplayCount(toGoSecs);
            return toGoSecs;
        }

        public void StartCountDown(int maxLoops)
        {
            //int AcquistionPeriod = WindGaugeCaptureCountdownPeriodSecs;
            //int capturesPerSec = WindGaugeCaptureCountsPerSec;
            //int MaxLoops = AcquistionPeriod * capturesPerSec; ;
            currentCount = maxLoops;
        }

        /// <summary>
        /// Siricco receive loop that reads lines terminated by CR-LF
        /// </summary>
        private async Task SiriccoReceiveLoopAsync(SerialPort port, CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested && port.IsOpen)
                {
                    try
                    {
                        // Check if data is available before reading
                        if (port.BytesToRead == 0)
                        {
                            await Task.Delay(10, cancellationToken);
                            continue;
                        }

                        // Read a single byte
                        int b = port.ReadByte();
                        if (b == -1)
                        {
                            await Task.Delay(10, cancellationToken);
                            continue;
                        }

                        char c = (char)b;
                        _siriccoLineBuffer.Append(c);
                        var length = _siriccoLineBuffer.Length;

                        // Check for CR-LF line terminator
                        if (c == '\n' && _siriccoLineBuffer.Length > 1 && _siriccoLineBuffer[_siriccoLineBuffer.Length - 2] == '\r')
                        {
                            // Remove the CR-LF
                            string csv = _siriccoLineBuffer.ToString();
                            string completeline = string.Join("_", csv.Select(b => MainViewModel.CharCommandToString((CharCommand)b)));
                            string shrunkLine = completeline.Replace("_", "");
                            

                            string line = _siriccoLineBuffer.ToString(0, _siriccoLineBuffer.Length - 2);
                            _siriccoLineBuffer.Clear();

                            if (string.IsNullOrWhiteSpace(line))
                            {
                                continue;
                            }
                            if (!SiriccoIsRunning)
                            {
                                if(!RecvStatus.Contains("Receiving but not processing speed data"))
                                    RecvStatus += "     Receiving but not processing speed data.";
                                continue;
                            }
                            System.Diagnostics.Debug.WriteLine($"\t\t\t\t\t\\t\t\t\t\t\t\t\t\t\t\t\tCountDownToGoSecs: left2Do={CountDownToGoSecs()} Count{currentCount}");

                            RecvStatus = "Received: " + shrunkLine.Replace(" ", "<sPC>");
                            //return;
                            // Validate the line using SiriccoData
                            var siriccoDataList = SiriccoData.ParseLines(line);

                            foreach (var siriccoData in siriccoDataList)
                            {
                                if (siriccoData.IsValid)
                                {
                                    var result = SiriccoData.Siricco_Data;
                                    // Valid Siricco message received
                                    // Raise SiriccoDataReceived event on UI thread
                                    Application.Current?.Dispatcher?.BeginInvoke(() => SiriccoDataReceived?.Invoke(result));
                                    Debug.WriteLine($"{line}");
                                    Debug.WriteLine($"Valid Siricco data:  Value1={siriccoData.Value1}, Value2={siriccoData.Value2}, Value3={siriccoData.Value3}, Value4={siriccoData.Value4}, SpeedUnit={siriccoData.SpeedUnit}");
                                }
                                else
                                {
                                    Debug.WriteLine($"Invalid Siricco data: {siriccoData.ErrorMessage}");
                                }
                            }
                        }
                    }
                    catch (TimeoutException)
                    {
                        // Timeout is expected when no data is available, continue
                        continue;
                    }
                    catch (OperationCanceledException)
                    {
                        // Cancellation requested
                        break;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error in Siricco receive loop: {ex.Message}");
                        await Task.Delay(100, cancellationToken); // Brief delay before retrying
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Expected on shutdown
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Siricco receive loop error: {ex.Message}");
            }
        }

        /// <summary>
        /// Stop the Siricco receive loop
        /// </summary>
        private void StopSiriccoReceiveLoop()
        {
            _siriccoReceiveCts?.Cancel();
            try
            {
                _siriccoReceiveTask?.Wait(TimeSpan.FromSeconds(1));
            }
            catch (Exception)
            {
                // Ignore timeout
            }
            _siriccoReceiveCts?.Dispose();
            _siriccoReceiveCts = null;
            _siriccoReceiveTask = null;
        }
    }
}
