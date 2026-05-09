using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using SwissTimingDisplay.Models;
using SwissTimingDisplay.Services;
using System.Diagnostics.Eventing.Reader;

namespace SwissTimingDisplay.ViewModels
{
    public enum DisplayMode
    {
        MMSSDD,
        HHMMSS,
    }

    public enum LapCountMode
    {
        None,
        UpCount,
        DownCount,
    }

    public enum RaceDistance
    {
        Distance600m,
        Distance1K,
        Distance1500m,
        Distance2K,
        Distance3K,
        Distance5K,
        Distance10K,
        Other,
    }

    public sealed class MainViewModel : INotifyPropertyChanged, IDisposable
    {
        private static readonly string SettingsDirectoryPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SwissTimingDisplay");

        private static readonly string SettingsFilePath = Path.Combine(SettingsDirectoryPath, "settings.json");

        public static int GetDistanceInMetres(RaceDistance distance)
        {
            return distance switch
            {
                RaceDistance.Distance600m => 600,
                RaceDistance.Distance1K => 1000,
                RaceDistance.Distance1500m => 1500,
                RaceDistance.Distance2K => 2000,
                RaceDistance.Distance3K => 3000,
                RaceDistance.Distance5K => 5000,
                RaceDistance.Distance10K => 10000,
                RaceDistance.Other => 0,
                _ => 0,
            };
        }

        public static bool IsStartAtFinish(RaceDistance distance)
        {
            var metres = GetDistanceInMetres(distance);
            if (metres == 0)
            {
                return false;
            }
            return metres % 400 == 0;
        }

        private readonly SerialPortDiscoveryService _discoveryService = new SerialPortDiscoveryService();
        private readonly SerialPortService _serialPortService = new SerialPortService();

        private SerialPortInfo? _selectedPort;
        private SerialPortInfo? _selectedReceivePort;
        private string? _selectedSendPortName;
        private string? _selectedReceivePortName;
        private TcpCommand _selectedTcpCommand;
        private bool _onlyProlific = true;
        private string _timeInput = "";
        private string _timeInputIn = "";
        private bool _useWallClockTimeOfDay = false;
        private bool _anchorDisplay = false;
        private string _bibNo = "";
        private int _bibNoInt = -1;
        private string _recvBibNoStr = "   ";
        private int _recvBibNoInt = -1;
        private string _status = "";
        private DisplayMode _displayMode = DisplayMode.MMSSDD;
        private bool _cosmetic = false;
        private int _numDigits = 6;
        private bool _isReceiveConnected;
        private string? _connectedReceivePortName;
        private bool _isUpdatingPortLists;
        private bool _isSyncingSelectedPorts;
        private LapCountMode _lapCountMode = LapCountMode.None;
        private TimeSpan _lapContinueDelay = TimeSpan.FromSeconds(5);
        private bool _isRaceRunning = false;
        private bool _raceHasStartedSinceReset = false;
        private bool _startAtFinish = true;
        private RaceDistance _raceDistance = RaceDistance.Distance600m;

        private SerialPort? _receivePort;
        private CancellationTokenSource? _receiveCts;
        private Task? _receiveTask;

        private string? _pendingPersistedSendPortName;
        private string? _pendingPersistedReceivePortName;
        private bool _sentClearCommand = false;

        private readonly CollectionViewSource _sendPortsViewSource = new CollectionViewSource();
        private readonly CollectionViewSource _receivePortsViewSource = new CollectionViewSource();

        public MainViewModel()
        {
            RefreshPortsCommand = new RelayCommand(RefreshPorts);
            ConnectCommand = new RelayCommand(Connect, () => !string.IsNullOrWhiteSpace(SelectedSendPortName) && !IsConnected);
            DisconnectCommand = new RelayCommand(Disconnect, () => IsConnected);
            SendCommand = new RelayCommand(async () => await SendAsync(), () => IsConnected);

            SendConnectToggleCommand = new RelayCommand(ToggleSendConnection, () => (!string.IsNullOrWhiteSpace(SelectedSendPortName) && !IsConnected) || IsConnected);
            ReceiveConnectToggleCommand = new RelayCommand(ToggleReceiveConnection, () => (!string.IsNullOrWhiteSpace(SelectedReceivePortName) && !IsReceiveConnected) || IsReceiveConnected);

            BibNoDecrementCommand = new RelayCommand(() => BibNoInt--, () => BibNoInt > 0);
            BibNoIncrementCommand = new RelayCommand(() => BibNoInt++, () => BibNoInt < 999);
            BibNoClearCommand = new RelayCommand(() => BibNo = string.Empty, () => BibNoInt >= 0);

            TcpCommands = new ObservableCollection<TcpCommand>(Enum.GetValues<TcpCommand>());
            SelectedTcpCommand = TcpCommands.FirstOrDefault();

            _sendPortsViewSource.Source = Ports;
            _sendPortsViewSource.Filter += SendPortsViewSourceOnFilter;

            _receivePortsViewSource.Source = Ports;
            _receivePortsViewSource.Filter += ReceivePortsViewSourceOnFilter;

            LoadPersistedPortNames();
            RefreshPorts();
            AutoConnectIfNeeded();
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public ObservableCollection<SerialPortInfo> Ports { get; } = new ObservableCollection<SerialPortInfo>();

        public ObservableCollection<SerialPortInfo> SendPorts { get; } = new ObservableCollection<SerialPortInfo>();

        public ObservableCollection<SerialPortInfo> ReceivePorts { get; } = new ObservableCollection<SerialPortInfo>();

        public ICollectionView SendPortsView => _sendPortsViewSource.View;

        public ICollectionView ReceivePortsView => _receivePortsViewSource.View;

        public ObservableCollection<TcpCommand> TcpCommands { get; }

        public SerialPortInfo? SelectedPort
        {
            get => _selectedPort;
            set
            {
                if (_isUpdatingPortLists && value is null)
                {
                    return;
                }

                if (value is null
                    && _selectedPort is not null
                    && Ports.Any(p => string.Equals(p.PortName, _selectedPort.PortName, StringComparison.OrdinalIgnoreCase)))
                {
                    return;
                }

                if (Set(ref _selectedPort, value))
                {
                    if (value is not null)
                    {
                        SelectedSendPortName = value.PortName;
                    }

                    if (!_isSyncingSelectedPorts)
                    {
                        EnforceMutualExclusionAndSelectionValidity();
                        RaiseCommandStates();
                    }
                }
            }
        }

        public SerialPortInfo? SelectedReceivePort
        {
            get => _selectedReceivePort;
            set
            {
                if (_isUpdatingPortLists && value is null)
                {
                    return;
                }

                if (value is null
                    && _selectedReceivePort is not null
                    && Ports.Any(p => string.Equals(p.PortName, _selectedReceivePort.PortName, StringComparison.OrdinalIgnoreCase)))
                {
                    return;
                }

                if (Set(ref _selectedReceivePort, value))
                {
                    if (value is not null)
                    {
                        SelectedReceivePortName = value.PortName;
                    }

                    if (!_isSyncingSelectedPorts)
                    {
                        EnforceMutualExclusionAndSelectionValidity();
                        RaiseCommandStates();
                    }
                }
            }
        }

        public string? SelectedSendPortName
        {
            get => _selectedSendPortName;
            set
            {
                if (!string.IsNullOrWhiteSpace(value)
                    && !string.IsNullOrWhiteSpace(SelectedReceivePortName)
                    && string.Equals(value, SelectedReceivePortName, StringComparison.OrdinalIgnoreCase))
                {
                    value = null;
                }

                if (Set(ref _selectedSendPortName, value))
                {
                    SavePersistedPortNames();
                    RefreshPortViews();

                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        var port = Ports.FirstOrDefault(p => string.Equals(p.PortName, value, StringComparison.OrdinalIgnoreCase));
                        if (port is not null && !ReferenceEquals(port, SelectedPort))
                        {
                            _isSyncingSelectedPorts = true;
                            SelectedPort = port;
                            _isSyncingSelectedPorts = false;
                        }
                    }

                    EnforceMutualExclusionAndSelectionValidity();
                    RaiseCommandStates();
                }
            }
        }

        public string? SelectedReceivePortName
        {
            get => _selectedReceivePortName;
            set
            {
                if (!string.IsNullOrWhiteSpace(value)
                    && !string.IsNullOrWhiteSpace(SelectedSendPortName)
                    && string.Equals(value, SelectedSendPortName, StringComparison.OrdinalIgnoreCase))
                {
                    value = null;
                }

                if (Set(ref _selectedReceivePortName, value))
                {
                    SavePersistedPortNames();
                    RefreshPortViews();

                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        var port = Ports.FirstOrDefault(p => string.Equals(p.PortName, value, StringComparison.OrdinalIgnoreCase));
                        if (port is not null && !ReferenceEquals(port, SelectedReceivePort))
                        {
                            _isSyncingSelectedPorts = true;
                            SelectedReceivePort = port;
                            _isSyncingSelectedPorts = false;
                        }
                    }

                    EnforceMutualExclusionAndSelectionValidity();
                    RaiseCommandStates();
                }
            }
        }

        public Task SendRawAsync(byte[] payload)
        {
            // Check if this is a clear command (STX + B + EOT/ETX)
            // The payload contains the full frame including STX and EOT/ETX
            if (payload.Length == 3 && payload[0] == (byte)CharCommand.STX && payload[1] == (byte)CharCommand.B)
            {
                _sentClearCommand = true;
            }

            return _serialPortService.SendAsync(payload);
        }

        public TcpCommand SelectedTcpCommand
        {
            get => _selectedTcpCommand;
            set => Set(ref _selectedTcpCommand, value);
        }

        public bool OnlyProlific
        {
            get => _onlyProlific;
            set
            {
                if (Set(ref _onlyProlific, value))
                {
                    SavePersistedPortNames();
                    RefreshPorts();
                }
            }
        }

        public bool IsConnected => _serialPortService.IsConnected;

        public string? ConnectedPortName => _serialPortService.ConnectedPortName;

        public bool IsReceiveConnected
        {
            get => _isReceiveConnected;
            private set
            {
                if (Set(ref _isReceiveConnected, value))
                {
                    OnPropertyChanged(nameof(ShowCosmeticOptions));
                    OnPropertyChanged(nameof(ShowDisplayModeOptions));
                    OnPropertyChanged(nameof(DisplayTime));
                    OnPropertyChanged(nameof(BibNoInputForDisplay));
                }
            }
        }

        public string? ConnectedReceivePortName
        {
            get => _connectedReceivePortName;
            private set => Set(ref _connectedReceivePortName, value);
        }

        public string TimeInput
        {
            get => _timeInput;
            set
            {
                if (Set(ref _timeInput, value))
                {
                    OnPropertyChanged(nameof(DisplayTime));
                }
            }
        }

        public string TimeInputIn
        {
            get => _timeInputIn;
            set
            {
                if (Set(ref _timeInputIn, value))
                {
                    OnPropertyChanged(nameof(DisplayTime));
                }
            }
        }

        public string DisplayTime
        {
            get
            {
                var raw = IsReceiveConnected ? (TimeInputIn ?? string.Empty) : (TimeInput ?? string.Empty);
                var digits = new string(raw.Where(char.IsDigit).ToArray());

                if (!Cosmetic)
                {
                    if (UseWallClockTimeOfDay)
                    {
                        return raw;
                    }

                    return digits;
                }

                if (digits.Length < 6)
                {
                    return raw;
                }

                digits = digits[..6];

                var effectiveDisplayMode = UseWallClockTimeOfDay ? DisplayMode.HHMMSS : DisplayMode;

                // LLMMSS format when lap counting is enabled (not None), 6 digits, not wall clock, and race is running
                if (LapCountMode != LapCountMode.None && NumDigits == 6 && !UseWallClockTimeOfDay && IsRaceRunning)
                {
                    var lapCounter = BibNoInt >= 0 ? BibNoInt.ToString("D2") : "00";
                    var mmss = effectiveDisplayMode == DisplayMode.HHMMSS ? digits.Substring(2, 4) : digits.Substring(0, 4);
                    return $"{lapCounter}:{mmss.Substring(0, 2)}:{mmss.Substring(2, 2)}";
                }

                if (effectiveDisplayMode == DisplayMode.HHMMSS)
                {
                    return $"{digits.Substring(0, 2)}:{digits.Substring(2, 2)}:{digits.Substring(4, 2)}";
                }

                return $"{digits.Substring(0, 2)}:{digits.Substring(2, 2)}.{digits.Substring(4, 2)}";
            }
        }

        public bool Cosmetic
        {
            get => _cosmetic;
            set
            {
                if (Set(ref _cosmetic, value))
                {
                    OnPropertyChanged(nameof(ShowDisplayModeOptions));
                    OnPropertyChanged(nameof(DisplayTime));
                }
            }
        }

        public bool ShowCosmeticOptions => true;

        public bool ShowDisplayModeOptions => Cosmetic;

        public DisplayMode DisplayMode
        {
            get => _displayMode;
            set
            {
                if (Set(ref _displayMode, value))
                {
                    OnPropertyChanged(nameof(IsDisplayModeHHMMSS));
                    OnPropertyChanged(nameof(DisplayModeLabel));
                    OnPropertyChanged(nameof(DisplayTime));
                }
            }
        }

        public string DisplayModeLabel => DisplayMode == DisplayMode.HHMMSS ? "HH:MM:SS" : "MM:SS.DD";

        public bool IsDisplayModeHHMMSS
        {
            get => DisplayMode == DisplayMode.HHMMSS;
            set => DisplayMode = value ? DisplayMode.HHMMSS : DisplayMode.MMSSDD;
        }

        public bool UseWallClockTimeOfDay
        {
            get => _useWallClockTimeOfDay;
            set
            {
                if (Set(ref _useWallClockTimeOfDay, value))
                {
                    if (value)
                    {
                        DisplayMode = DisplayMode.HHMMSS;
                    }

                    OnPropertyChanged(nameof(DisplayTime));
                    OnPropertyChanged(nameof(DisplayModeLabel));
                    OnPropertyChanged(nameof(IsDisplayModeHHMMSS));
                }
            }
        }

        public bool AnchorDisplay
        {
            get => _anchorDisplay;
            set
            {
                if (Set(ref _anchorDisplay, value))
                {
                    SavePersistedPortNames();
                }
            }
        }

        public int NumDigits
        {
            get => _numDigits;
            set
            {
                var normalized = value == 9 ? 9 : 6;
                if (Set(ref _numDigits, normalized))
                {
                    OnPropertyChanged(nameof(IsNumDigits9));
                    OnPropertyChanged(nameof(NumDigitsLabel));
                    SavePersistedPortNames();
                }
            }
        }

        public bool IsNumDigits9
        {
            get => NumDigits == 9;
            set => NumDigits = value ? 9 : 6;
        }

        public string NumDigitsLabel => $"Digits: {NumDigits}";

        public LapCountMode LapCountMode
        {
            get => _lapCountMode;
            set
            {
                if (Set(ref _lapCountMode, value))
                {
                    OnPropertyChanged(nameof(DisplayTime));
                    // If switching to DownCount mode, apply IsStartAtFinish based on current RaceDistance
                    if (value == LapCountMode.DownCount)
                    {
                        StartAtFinish = IsStartAtFinish(RaceDistance);
                    }
                }
            }
        }

        public TimeSpan LapContinueDelay
        {
            get => _lapContinueDelay;
            set => Set(ref _lapContinueDelay, value);
        }

        public double LapContinueDelaySeconds
        {
            get => _lapContinueDelay.TotalSeconds;
            set => LapContinueDelay = TimeSpan.FromSeconds(value);
        }

        public bool IsRaceRunning
        {
            get => _isRaceRunning;
            set
            {
                if (Set(ref _isRaceRunning, value))
                {
                    OnPropertyChanged(nameof(DisplayTime));
                }
            }
        }

        public bool RaceHasStartedSinceReset
        {
            get => _raceHasStartedSinceReset;
            set => Set(ref _raceHasStartedSinceReset, value);
        }

        public bool StartAtFinish
        {
            get => _startAtFinish;
            set => Set(ref _startAtFinish, value);
        }

        public RaceDistance RaceDistance
        {
            get => _raceDistance;
            set
            {
                if (Set(ref _raceDistance, value))
                {
                    SavePersistedPortNames();
                    // If in DownCount mode, apply IsStartAtFinish to StartAtFinish
                    if (LapCountMode == LapCountMode.DownCount)
                    {
                        StartAtFinish = IsStartAtFinish(value);
                    }
                    // Set BibNo to calculated laps when race distance is selected (except Other)
                    if (value != RaceDistance.Other)
                    {
                        var metres = GetDistanceInMetres(value);
                        var laps = metres / 400;
                        BibNo = laps.ToString();
                    }
                }
            }
        }

        public string BibNo
        {
            get => _bibNo;
            set
            {
                if (Set(ref _bibNo, value))
                {
                    if (string.IsNullOrWhiteSpace(value))
                    {
                        Set(ref _bibNoInt, -1, nameof(BibNoInt));
                    }
                    else if (int.TryParse(value, out var n) && n >= 0 && n <= 999)
                    {
                        Set(ref _bibNoInt, n, nameof(BibNoInt));
                    }

                    OnPropertyChanged(nameof(BibNoDisplay));
                }
            }
        }

        public string BibNoDisplay => _bibNoInt < 0 ? string.Empty : _bibNoInt.ToString("D3") + ".";

        public string BibNoInputForDisplay
        {
            get
            {
                if (IsReceiveConnected)
                {
                    return _recvBibNoInt >= 0 ? _recvBibNoInt.ToString("D3") + "." : string.Empty;
                }
                return BibNoDisplay;
            }
        }

        public string RecvBibNoStr
        {
            get => _recvBibNoStr;
            set
            {
                if (Set(ref _recvBibNoStr, value))
                {
                    if (string.IsNullOrWhiteSpace(value) || value.Trim().Length == 0)
                    {
                        _recvBibNoInt = -1;
                        OnPropertyChanged(nameof(RecvBibNoInt));
                        OnPropertyChanged(nameof(BibNoInputForDisplay));
                    }
                    else if (int.TryParse(value.Trim(), out var n) && n >= 0 && n <= 999)
                    {
                        _recvBibNoInt = n;
                        OnPropertyChanged(nameof(RecvBibNoInt));
                        OnPropertyChanged(nameof(BibNoInputForDisplay));
                    }
                }
            }
        }

        public int RecvBibNoInt
        {
            get => _recvBibNoInt;
        }

        public int BibNoInt
        {
            get => _bibNoInt;
            set
            {
                var clamped = value < -1 ? -1 : (value > 999 ? 999 : value);
                if (Set(ref _bibNoInt, clamped, nameof(BibNoInt)))
                {
                    _bibNo = clamped == -1 ? string.Empty : clamped.ToString();
                    OnPropertyChanged(nameof(BibNo));
                    OnPropertyChanged(nameof(BibNoDisplay));
                    OnPropertyChanged(nameof(BibNoInputForDisplay));
                    OnPropertyChanged(nameof(DisplayTime));
                    BibNoDecrementCommand.RaiseCanExecuteChanged();
                    BibNoIncrementCommand.RaiseCanExecuteChanged();
                    BibNoClearCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public string Status
        {
            get => _status;
            set => Set(ref _status, value);
        }

        public RelayCommand RefreshPortsCommand { get; }
        public RelayCommand ConnectCommand { get; }
        public RelayCommand DisconnectCommand { get; }
        public RelayCommand SendCommand { get; }

        public RelayCommand SendConnectToggleCommand { get; }

        public RelayCommand ReceiveConnectToggleCommand { get; }

        public RelayCommand BibNoDecrementCommand { get; }
        public RelayCommand BibNoIncrementCommand { get; }
        public RelayCommand BibNoClearCommand { get; }

        public void RefreshPorts()
        {
            try
            {
                var ports = _discoveryService.GetSerialPorts(OnlyProlific);

                Ports.Clear();
                foreach (var p in ports)
                {
                    Ports.Add(p);
                }

                if (!string.IsNullOrWhiteSpace(_pendingPersistedSendPortName)
                    && Ports.Any(p => string.Equals(p.PortName, _pendingPersistedSendPortName, StringComparison.OrdinalIgnoreCase)))
                {
                    SelectedSendPortName = _pendingPersistedSendPortName;
                }

                if (!string.IsNullOrWhiteSpace(_pendingPersistedReceivePortName)
                    && Ports.Any(p => string.Equals(p.PortName, _pendingPersistedReceivePortName, StringComparison.OrdinalIgnoreCase)))
                {
                    SelectedReceivePortName = _pendingPersistedReceivePortName;
                }

                _pendingPersistedSendPortName = null;
                _pendingPersistedReceivePortName = null;

                RefreshPortViews();

                if (SelectedPort is null && !string.IsNullOrWhiteSpace(SelectedSendPortName))
                {
                    SelectedPort = Ports.FirstOrDefault(p => string.Equals(p.PortName, SelectedSendPortName, StringComparison.OrdinalIgnoreCase));
                }

                if (SelectedReceivePort is null && !string.IsNullOrWhiteSpace(SelectedReceivePortName))
                {
                    SelectedReceivePort = Ports.FirstOrDefault(p => string.Equals(p.PortName, SelectedReceivePortName, StringComparison.OrdinalIgnoreCase));
                }

                EnforceMutualExclusionAndSelectionValidity();

                Status = $"Found {Ports.Count} port(s).";
            }
            catch (Exception ex)
            {
                Status = ex.Message;
            }
        }

        private void AutoConnectIfNeeded()
        {
            var sendPortAvailable = !string.IsNullOrWhiteSpace(SelectedSendPortName)
                && Ports.Any(p => string.Equals(p.PortName, SelectedSendPortName, StringComparison.OrdinalIgnoreCase));

            var receivePortAvailable = !string.IsNullOrWhiteSpace(SelectedReceivePortName)
                && Ports.Any(p => string.Equals(p.PortName, SelectedReceivePortName, StringComparison.OrdinalIgnoreCase));

            if (sendPortAvailable && receivePortAvailable)
            {
                try
                {
                    Connect();
                    ConnectReceive(SelectedReceivePortName);
                    RaiseCommandStates();
                    Status = $"Auto-connected to {SelectedSendPortName} (send) and {SelectedReceivePortName} (receive).";
                }
                catch (Exception ex)
                {
                    Status = $"Auto-connect failed: {ex.Message}";
                }
            }
        }

        public void Connect()
        {
            if (string.IsNullOrWhiteSpace(SelectedSendPortName))
            {
                Status = "Select a COM port.";
                return;
            }

            try
            {
                _serialPortService.Connect(SelectedSendPortName);
                Status = $"Connected to {SelectedSendPortName}.";
                OnPropertyChanged(nameof(IsConnected));
                OnPropertyChanged(nameof(ConnectedPortName));
                RaiseCommandStates();
            }
            catch (Exception ex)
            {
                Status = ex.Message;
            }
        }

        public void Disconnect()
        {
            try
            {
                _serialPortService.Disconnect();
                Status = "Disconnected.";
                OnPropertyChanged(nameof(IsConnected));
                OnPropertyChanged(nameof(ConnectedPortName));
                RaiseCommandStates();
            }
            catch (Exception ex)
            {
                Status = ex.Message;
            }
        }

        private void ToggleSendConnection()
        {
            if (IsConnected)
            {
                Disconnect();
            }
            else
            {
                Connect();
            }
        }

        private void ToggleReceiveConnection()
        {
            if (IsReceiveConnected)
            {
                DisconnectReceive();
                RaiseCommandStates();
                return;
            }

            if (string.IsNullOrWhiteSpace(SelectedReceivePortName))
            {
                Status = "Select a receive COM port.";
                RaiseCommandStates();
                return;
            }

            try
            {
                ConnectReceive(SelectedReceivePortName);
                Status = $"Receive connected to {SelectedReceivePortName}.";
            }
            catch (Exception ex)
            {
                Status = ex.Message;
            }

            OnPropertyChanged(nameof(DisplayTime));
            RaiseCommandStates();
        }

        private void ConnectReceive(string portName, int baudRate = 9600)
        {
            DisconnectReceive();

            var port = new SerialPort(portName, baudRate)
            {
                Parity = Parity.None,
                DataBits = 8,
                StopBits = StopBits.One,
                Handshake = Handshake.None,
                ReadTimeout = 1000,
                WriteTimeout = 1000,
            };

            port.Open();
            _receivePort = port;
            IsReceiveConnected = true;
            ConnectedReceivePortName = portName;
            OnPropertyChanged(nameof(DisplayTime));

            _receiveCts = new CancellationTokenSource();
            _receiveTask = Task.Run(() => ReceiveLoopAsync(port, _receiveCts.Token));
        }

        private void DisconnectReceive()
        {
            var cts = _receiveCts;
            _receiveCts = null;
            if (cts is not null)
            {
                try
                {
                    cts.Cancel();
                }
                finally
                {
                    cts.Dispose();
                }
            }

            var port = _receivePort;
            _receivePort = null;

            if (port is not null)
            {
                try
                {
                    if (port.IsOpen)
                    {
                        port.Close();
                    }
                }
                finally
                {
                    port.Dispose();
                }
            }

            _receiveTask = null;
            IsReceiveConnected = false;
            ConnectedReceivePortName = null;
            OnPropertyChanged(nameof(DisplayTime));
        }

        public enum OpMode { display,windgauge, unknown}



        private async Task<Tuple<byte[], OpMode>?> ReadSerialMessageAsync(System.IO.Ports.SerialPort port, CancellationToken cancellationToken)
        {
            const byte SOH = 0x01; // Start of Header
            const byte STX = 0x02; // Start of Text
            const byte ETX = 0x03; // End of Text
            const byte EOT = 0x04; // End of Transmission
            const byte DC3 = 0x13; // Device Control 3

            OpMode mode = OpMode.unknown;

            var buffer = new List<byte>();
            byte[] singleByte = new byte[1];

            // Read until SOH or STX is found
            while (!cancellationToken.IsCancellationRequested)
            {
                int bytesRead = await port.BaseStream.ReadAsync(singleByte, 0, 1, cancellationToken);
                if (bytesRead == 0)
                {
                    return null; // End of stream
                }

                if (singleByte[0] == SOH || singleByte[0] == STX)
                {
                    buffer.Add(singleByte[0]); // Add the start character
                    break;
                }
            }

            // Read until ETX or EOT is found
            while (!cancellationToken.IsCancellationRequested)
            {
                int bytesRead = await port.BaseStream.ReadAsync(singleByte, 0, 1, cancellationToken);
                if (bytesRead == 0)
                {
                    return null; // Return null if stream ends
                }

                buffer.Add(singleByte[0]); // Add the character

                // Validate based on start character
                if (buffer[0] == STX)
                {
                    if (buffer.Count > 14)
                        return null;
                    if (buffer.Count >= 2 && buffer[1] != 'B' && buffer[1] != 'I')
                        return null;
                    if (buffer.Count > 3 && buffer[1] == 'B' )
                        return null;
                    if (buffer.Count > 14 && buffer[1] == 'I' )
                        return null;
                    mode = OpMode.display;
                }
                else if (buffer[0] == SOH)
                {
                    if (buffer.Count > 9)
                        return null;
                    if (buffer.Count >= 2 && buffer[1] != DC3)
                        return null;
                    if (buffer.Count >= 3 && buffer[2] != 'C')
                        return null;
                    if (buffer.Count >= 4 && buffer[3] != 'W')
                        return null;
                    if (buffer.Count >= 5 && new List<byte> { (byte) 'I',(byte)'S',(byte)'R',(byte)'O'}.Contains(buffer[4]) == false)
                        return null;
                    //CWS,CWR and CWO are of length 4
                    if (buffer.Count >4  && buffer[4] !=(byte)'I')
                        return null;
                    if (buffer.Count==8)
                    {
                        // Other should be of length 
                        if (buffer[4] != (byte)'I')
                            return null;
                    }
                    mode = OpMode.windgauge; ;
                }

                if (singleByte[0] == ETX || singleByte[0] == EOT)
                {
                    break; // End of message
                }
            }

            return new Tuple<byte[],OpMode>  ( buffer.ToArray(), mode );
        }


        private async Task ReceiveLoopAsync(SerialPort port, CancellationToken cancellationToken)
        {

            while (!cancellationToken.IsCancellationRequested)
            {
                var buff = await ReadSerialMessageAsync(port, cancellationToken);
                if (buff == null) continue;
                byte[] buffer = buff.Item1;
                OpMode mode = buff.Item2;
                TcpCommand? tcpCommand = null;

                switch (mode)
                {
                    case OpMode.display:
                        switch (buffer[1])
                        {
                            case (byte)'B':
                                // Handle case for 'B'
                                tcpCommand = TcpCommand.RollerTimeModeClear;
                                Application.Current?.Dispatcher?.BeginInvoke(() => ProcessReceivedFrame((TcpCommand)tcpCommand, new List<char>() { }));
                                break;
                            case (byte)'I':
                                // Handle case for 'I'
                                tcpCommand = TcpCommand.RollerTimeofDayorRunningTime;
                                var chars = buffer.Skip(2).Select(b => (char)b).ToList();
                                Application.Current?.Dispatcher?.BeginInvoke(() => ProcessReceivedFrame((TcpCommand)tcpCommand,chars));

                                break;
                            default:
                                continue; // Invalid frame, skip
                        }
                        break;
                    case OpMode.windgauge:
                        switch (buffer[4])
                        {
                            case (byte)'I':
                                // Handle case for 'I'
                                tcpCommand = TcpCommand.WindGauge_Acquisition_Duration;
                                int duration = 10;
                                int durationTens = buffer[6] - (byte)'0';
                                int durationUnits = buffer[7] - (byte)'0';
                                if ((durationTens >= 0) &&
                                    (durationTens < 10) &&
                                    (durationUnits >= 0) &&
                                    (durationTens < 10))
                                {
                                    duration = durationTens * 10 + durationUnits;
                                }
                                break;
                            case (byte)'S':
                                // Handle case for 'S'
                                tcpCommand = TcpCommand.WindGauge_Start_of_Measurement;
                                break;
                            case (byte)'R':
                                // Handle case for 'R'
                                tcpCommand = TcpCommand.WindGauge_Resend_Latest;
                                break;
                            case (byte)'O':
                                // Handle case for 'O'
                                tcpCommand = TcpCommand.WindGauge_Resend_Latest;
                                break;
                            default:
                                continue; // Invalid frame, skip
                        }
                        break;
                    default:
                        continue; // Unknown mode, skip
                }
                //Application.Current?.Dispatcher?.BeginInvoke(() => ProcessReceivedFrame(completed));
                continue;
            }
        }
        

        private void ProcessReceivedFrame(TcpCommand tcpCommand, List<char> chars )
        {

            if (tcpCommand == TcpCommand.RollerTimeofDayorRunningTime)
            {
                
                RollerTimeofDayorRunningTime(chars);
            }
            else if (tcpCommand == TcpCommand.RollerTimeofDayorRunningTime)
            {
                // Only process clear command on receive side if both ports are connected
                // and the clear command was sent locally (to prevent external clears)
                if (IsConnected && IsReceiveConnected)
                {
                    if (_sentClearCommand)
                    {
                        _sentClearCommand = false;
                        RollerTimeofDayorRunningTimeClear();
                    }
                    return;
                }

                RollerTimeofDayorRunningTimeClear();
            }
        }

        private void RollerTimeofDayorRunningTimeClear()
        {
            TimeInputIn = string.Empty;
        }

        private void RollerTimeofDayorRunningTime(IReadOnlyList<char> chars)
        {
            TimeInputIn = new string(chars.ToArray());

            // Extract BibNo (NNN) - should be at indices 6-8 after TIME (6 bytes)
            if (chars.Count >= 9)
            {
                var bibNoChars = chars.Skip(6).Take(3).ToArray();
                RecvBibNoStr = new string(bibNoChars);
                TimeInputIn = new string(chars.Skip(0).Take(6).ToArray());
            }
        }

        public async Task SendAsync()
        {
            try
            {
                var payload = TcpCommandDefinitions.GetPayloadBytes(SelectedTcpCommand).ToArray();
                var hex = BitConverter.ToString(payload);

                MessageBox.Show(
                    $"{SelectedTcpCommand}\n\n{hex}",
                    "Payload Bytes",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                Status = $"Prepared {payload.Length} byte(s): {hex}";

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                Status = ex.Message;
            }
        }

        public void Dispose()
        {
            SavePersistedPortNames();
            DisconnectReceive();
            _serialPortService.Dispose();
        }

        private sealed class PersistedSettings
        {
            public string? SendPortName { get; set; }
            public string? ReceivePortName { get; set; }
            public bool OnlyProlific { get; set; } = true;
            public bool AnchorDisplay { get; set; }
            public int NumDigits { get; set; } = 6;
            public RaceDistance RaceDistance { get; set; } = RaceDistance.Distance600m;
        }

        private void LoadPersistedPortNames()
        {
            try
            {
                if (!File.Exists(SettingsFilePath))
                {
                    return;
                }

                var json = File.ReadAllText(SettingsFilePath);
                var settings = JsonSerializer.Deserialize<PersistedSettings>(json);
                if (settings is null)
                {
                    return;
                }

                OnlyProlific = settings.OnlyProlific;
                AnchorDisplay = settings.AnchorDisplay;
                NumDigits = settings.NumDigits;
                RaceDistance = settings.RaceDistance;

                if (!string.IsNullOrWhiteSpace(settings.SendPortName))
                {
                    _pendingPersistedSendPortName = settings.SendPortName;
                }

                if (!string.IsNullOrWhiteSpace(settings.ReceivePortName))
                {
                    _pendingPersistedReceivePortName = settings.ReceivePortName;
                }
            }
            catch
            {
            }
        }

        private void SavePersistedPortNames()
        {
            try
            {
                Directory.CreateDirectory(SettingsDirectoryPath);
                var settings = new PersistedSettings
                {
                    SendPortName = SelectedSendPortName,
                    ReceivePortName = SelectedReceivePortName,
                    OnlyProlific = OnlyProlific,
                    AnchorDisplay = AnchorDisplay,
                    NumDigits = NumDigits,
                    RaceDistance = RaceDistance,
                };

                var json = JsonSerializer.Serialize(settings);
                File.WriteAllText(SettingsFilePath, json);
            }
            catch
            {
            }
        }

        private void RaiseCommandStates()
        {
            ConnectCommand.RaiseCanExecuteChanged();
            DisconnectCommand.RaiseCanExecuteChanged();
            SendCommand.RaiseCanExecuteChanged();
            SendConnectToggleCommand.RaiseCanExecuteChanged();
            ReceiveConnectToggleCommand.RaiseCanExecuteChanged();
        }

        private void RefreshPortViews()
        {
            _sendPortsViewSource.View?.Refresh();
            _receivePortsViewSource.View?.Refresh();
        }

        private void SendPortsViewSourceOnFilter(object sender, FilterEventArgs e)
        {
            if (e.Item is not SerialPortInfo p)
            {
                e.Accepted = false;
                return;
            }

            if (!string.IsNullOrWhiteSpace(SelectedReceivePortName)
                && string.Equals(p.PortName, SelectedReceivePortName, StringComparison.OrdinalIgnoreCase))
            {
                e.Accepted = false;
                return;
            }

            e.Accepted = true;
        }

        private void ReceivePortsViewSourceOnFilter(object sender, FilterEventArgs e)
        {
            if (e.Item is not SerialPortInfo p)
            {
                e.Accepted = false;
                return;
            }

            if (!string.IsNullOrWhiteSpace(SelectedSendPortName)
                && string.Equals(p.PortName, SelectedSendPortName, StringComparison.OrdinalIgnoreCase))
            {
                e.Accepted = false;
                return;
            }

            e.Accepted = true;
        }

        private void EnforceMutualExclusionAndSelectionValidity()
        {
            if (_isUpdatingPortLists)
            {
                return;
            }

            _isUpdatingPortLists = true;

            try
            {
                if (!string.IsNullOrWhiteSpace(SelectedSendPortName)
                    && Ports.All(p => !string.Equals(p.PortName, SelectedSendPortName, StringComparison.OrdinalIgnoreCase)))
                {
                    SelectedSendPortName = null;
                    SelectedPort = null;
                }

                if (!string.IsNullOrWhiteSpace(SelectedReceivePortName)
                    && Ports.All(p => !string.Equals(p.PortName, SelectedReceivePortName, StringComparison.OrdinalIgnoreCase)))
                {
                    SelectedReceivePortName = null;
                    SelectedReceivePort = null;
                }

                if (!string.IsNullOrWhiteSpace(SelectedSendPortName)
                    && !string.IsNullOrWhiteSpace(SelectedReceivePortName)
                    && string.Equals(SelectedSendPortName, SelectedReceivePortName, StringComparison.OrdinalIgnoreCase))
                {
                    SelectedReceivePortName = null;
                    SelectedReceivePort = null;
                }

                RefreshPortViews();
            }
            catch
            {
            }

            _isUpdatingPortLists = false;
        }

        private bool Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, value))
            {
                return false;
            }

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
