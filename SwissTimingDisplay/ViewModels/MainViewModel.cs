using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Diagnostics.Eventing.Reader;
using CommunityToolkit.Mvvm.ComponentModel;
using SwissTimingDisplay.Models;
using SwissTimingDisplay.Services;

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

    public sealed partial class MainViewModel : ObservableObject, IDisposable
    {
        private static MainViewModel? _sharedInstance;
        private static readonly string SettingsDirectoryPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SwissTimingDisplay");

        private static readonly string SettingsFilePath = Path.Combine(SettingsDirectoryPath, "settings.json");

        public static MainViewModel SharedInstance
        {
            get
            {
                if (_sharedInstance == null)
                {
                    _sharedInstance = new MainViewModel();
                }
                return _sharedInstance;
            }
        }

        public enum ActiveWindow
        {
            None,
            Display,
            WindGauge
        }

        private ActiveWindow _activeWindow = ActiveWindow.None;

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
        private bool _showWindGaugeWindow = false;
        private bool _isLoadingSettings = false;
        private bool _isShuttingDown = false;
        private bool _isSwitchingWindows = false;

        private SerialPort? _receivePort;
        private CancellationTokenSource? _receiveCts;
        private Task? _receiveTask;

        private List<byte> _sendPortReceiveBuffer = new List<byte>();
        private bool _displaySimulatorSpeed = false;
        private bool _hideSimulator = false;

        private string? _pendingPersistedSendPortName;
        private string? _pendingPersistedReceivePortName;
        private bool _pendingPersistedSendPortConnected = false;
        private bool _pendingPersistedReceivePortConnected = false;
        private bool _pendingPersistedWindGaugeSendPortConnected = false;
        private bool _pendingPersistedWindGaugeReceiveConnected = false;
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

            RollerTcpCommands = new ObservableCollection<TcpCommand>(Enum.GetValues<TcpCommand>()
                .Where(c => c.ToString().StartsWith("Roller")));
            WindGaugeTcpCommands = new ObservableCollection<TcpCommand>(Enum.GetValues<TcpCommand>()
                .Where(c => c.ToString().StartsWith("WindGauge")));

            _sendPortsViewSource.Source = Ports;
            _sendPortsViewSource.Filter += SendPortsViewSourceOnFilter;

            _receivePortsViewSource.Source = Ports;
            _receivePortsViewSource.Filter += ReceivePortsViewSourceOnFilter;

            _serialPortService.DataReceived += OnSendPortDataReceived;

            _isLoadingSettings = true;
            LoadPersistedPortNames();
            RefreshPorts();
            AutoConnectIfNeeded();
            _isLoadingSettings = false;
            
            // Save once after initialization to persist connection state
            SavePersistedPortNames();
        }

        protected override void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);
            
            // Auto-save settings when any property changes (except during loading, shutdown, or window switching)
            if (!_isLoadingSettings && !_isShuttingDown && !_isSwitchingWindows)
            {
                SavePersistedPortNames();
            }
        }

        public void BeginShutdown()
        {
            _isShuttingDown = true;
        }

        public ObservableCollection<SerialPortInfo> Ports { get; } = new ObservableCollection<SerialPortInfo>();

        public ObservableCollection<SerialPortInfo> SendPorts { get; } = new ObservableCollection<SerialPortInfo>();

        public ObservableCollection<SerialPortInfo> ReceivePorts { get; } = new ObservableCollection<SerialPortInfo>();

        public ICollectionView SendPortsView => _sendPortsViewSource.View;

        public ICollectionView ReceivePortsView => _receivePortsViewSource.View;

        public ObservableCollection<TcpCommand> TcpCommands { get; }

        public ObservableCollection<TcpCommand> RollerTcpCommands { get; }

        public ObservableCollection<TcpCommand> WindGaugeTcpCommands { get; }

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

                if (SetProperty(ref _selectedPort, value))
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

                if (SetProperty(ref _selectedReceivePort, value))
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
                // Only enforce mutual exclusion for Display window (not Wind Gauge)
                if (!ShowWindGaugeWindow
                    && !string.IsNullOrWhiteSpace(value)
                    && !string.IsNullOrWhiteSpace(SelectedReceivePortName)
                    && string.Equals(value, SelectedReceivePortName, StringComparison.OrdinalIgnoreCase))
                {
                    value = null;
                }

                if (SetProperty(ref _selectedSendPortName, value))
                {
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
                // Only enforce mutual exclusion for Display window (not Wind Gauge)
                if (!ShowWindGaugeWindow
                    && !string.IsNullOrWhiteSpace(value)
                    && !string.IsNullOrWhiteSpace(SelectedSendPortName)
                    && string.Equals(value, SelectedSendPortName, StringComparison.OrdinalIgnoreCase))
                {
                    value = null;
                }

                if (SetProperty(ref _selectedReceivePortName, value))
                {
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
            if (payload.Length >= 3 && payload[0] == 0x02 && payload[1] == 0x42)
            {
                _sentClearCommand = true;
            }

            return _serialPortService.SendAsync(payload);
        }

        public Task SendRawAsyncReceive(byte[] payload)
        {
            if (_receivePort is null || !_receivePort.IsOpen)
            {
                return Task.CompletedTask;
            }

            return Task.Run(() =>
            {
                _receivePort.Write(payload, 0, payload.Length);
            });
        }

        public TcpCommand SelectedTcpCommand
        {
            get => _selectedTcpCommand;
            set => SetProperty(ref _selectedTcpCommand, value);
        }

        public bool OnlyProlific
        {
            get => _onlyProlific;
            set
            {
                if (SetProperty(ref _onlyProlific, value))
                {
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
                if (SetProperty(ref _isReceiveConnected, value))
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
            private set => SetProperty(ref _connectedReceivePortName, value);
        }

        public bool DisplaySimulatorSpeed
        {
            get => _hideSimulator ? false : _displaySimulatorSpeed;
            set
            {
                if (_displaySimulatorSpeed != value)
                {
                    _displaySimulatorSpeed = value;
                    OnPropertyChanged(nameof(DisplaySimulatorSpeed));
                }
            }
        }

        public bool HideSimulator
        {
            get => _hideSimulator;
            set
            {
                if (SetProperty(ref _hideSimulator, value))
                {
                    if (value)
                    {
                        // Disconnect receive port when hiding simulator
                        DisconnectReceive();
                        // Set DisplaySimulatorSpeed to false when hiding simulator
                        if (_displaySimulatorSpeed)
                        {
                            _displaySimulatorSpeed = false;
                            OnPropertyChanged(nameof(DisplaySimulatorSpeed));
                        }
                    }
                    OnPropertyChanged(nameof(HideSimulator));
                }
            }
        }

        public bool ShowWindGaugeWindow
        {
            get => _showWindGaugeWindow;
            set
            {
                // Only proceed if value is actually changing
                if (_showWindGaugeWindow == value)
                {
                    return;
                }

                if (SetProperty(ref _showWindGaugeWindow, value))
                {
                    if (!_isLoadingSettings)
                    {
                        _isSwitchingWindows = true;
                        try
                        {
                            // Switch to the new window's ports
                            SwitchToWindowPorts(value);
                        }
                        finally
                        {
                            _isSwitchingWindows = false;
                        }
                    }
                }
            }
        }

        public string TimeInput
        {
            get => _timeInput;
            set
            {
                if (SetProperty(ref _timeInput, value))
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
                if (SetProperty(ref _timeInputIn, value))
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
                if (SetProperty(ref _cosmetic, value))
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
                if (SetProperty(ref _displayMode, value))
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
                if (SetProperty(ref _useWallClockTimeOfDay, value))
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
                if (SetProperty(ref _anchorDisplay, value))
                {
                }
            }
        }

        public int NumDigits
        {
            get => _numDigits;
            set
            {
                var normalized = value == 9 ? 9 : 6;
                if (SetProperty(ref _numDigits, normalized))
                {
                    OnPropertyChanged(nameof(IsNumDigits9));
                    OnPropertyChanged(nameof(NumDigitsLabel));
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
                if (SetProperty(ref _lapCountMode, value))
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
            set => SetProperty(ref _lapContinueDelay, value);
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
                if (SetProperty(ref _isRaceRunning, value))
                {
                    OnPropertyChanged(nameof(DisplayTime));
                }
            }
        }

        public bool RaceHasStartedSinceReset
        {
            get => _raceHasStartedSinceReset;
            set => SetProperty(ref _raceHasStartedSinceReset, value);
        }

        public ActiveWindow CurrentWindow
        {
            get => _activeWindow;
            set
            {
                if (SetProperty(ref _activeWindow, value))
                {
                    // Disconnect ports when switching windows
                    if (value == ActiveWindow.Display)
                    {
                        // Ensure only Display ports can be connected
                    }
                    else if (value == ActiveWindow.WindGauge)
                    {
                        // Ensure only WindGauge ports can be connected
                    }
                }
            }
        }

        public bool StartAtFinish
        {
            get => _startAtFinish;
            set => SetProperty(ref _startAtFinish, value);
        }

        public RaceDistance RaceDistance
        {
            get => _raceDistance;
            set
            {
                if (SetProperty(ref _raceDistance, value))
                {
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
                if (SetProperty(ref _bibNo, value))
                {
                    if (string.IsNullOrWhiteSpace(value))
                    {
                        SetProperty(ref _bibNoInt, -1, nameof(BibNoInt));
                    }
                    else if (int.TryParse(value, out var n) && n >= 0 && n <= 999)
                    {
                        SetProperty(ref _bibNoInt, n, nameof(BibNoInt));
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
                if (SetProperty(ref _recvBibNoStr, value))
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
                if (SetProperty(ref _bibNoInt, clamped, nameof(BibNoInt)))
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
            set => SetProperty(ref _status, value);
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

        public void AutoConnectIfNeeded()
        {
            var sendPortAvailable = !string.IsNullOrWhiteSpace(SelectedSendPortName)
                && Ports.Any(p => string.Equals(p.PortName, SelectedSendPortName, StringComparison.OrdinalIgnoreCase));

            var receivePortAvailable = !string.IsNullOrWhiteSpace(SelectedReceivePortName)
                && Ports.Any(p => string.Equals(p.PortName, SelectedReceivePortName, StringComparison.OrdinalIgnoreCase));

            if (ShowWindGaugeWindow)
            {
                // Auto-connect WindGauge ports (send and receive if it was connected)
                if (sendPortAvailable && _pendingPersistedWindGaugeSendPortConnected)
                {
                    try
                    {
                        Connect();
                        RaiseCommandStates();
                        Status = $"Auto-connected to {SelectedSendPortName} (send).";
                    }
                    catch (Exception ex)
                    {
                        Status = $"Auto-connect failed: {ex.Message}";
                    }
                }

                if (receivePortAvailable && _pendingPersistedWindGaugeReceiveConnected)
                {
                    try
                    {
                        ConnectReceive(SelectedReceivePortName);
                        RaiseCommandStates();
                        Status = $"Auto-connected to {SelectedSendPortName} (send) and {SelectedReceivePortName} (receive).";
                    }
                    catch (Exception ex)
                    {
                        Status = $"Auto-connect receive failed: {ex.Message}";
                    }
                }
            }
            else
            {
                // Auto-connect MainWindow ports (send and receive if it was connected)
                if (sendPortAvailable && _pendingPersistedSendPortConnected)
                {
                    try
                    {
                        Connect();
                        RaiseCommandStates();
                        Status = $"Auto-connected to {SelectedSendPortName} (send).";
                    }
                    catch (Exception ex)
                    {
                        Status = $"Auto-connect failed: {ex.Message}";
                    }
                }

                if (receivePortAvailable && _pendingPersistedReceivePortConnected)
                {
                    try
                    {
                        ConnectReceive(SelectedReceivePortName);
                        RaiseCommandStates();
                        Status = $"Auto-connected to {SelectedSendPortName} (send) and {SelectedReceivePortName} (receive).";
                    }
                    catch (Exception ex)
                    {
                        Status = $"Auto-connect receive failed: {ex.Message}";
                    }
                }
            }

            // Clear pending connection states after use
            _pendingPersistedSendPortConnected = false;
            _pendingPersistedReceivePortConnected = false;
            _pendingPersistedWindGaugeSendPortConnected = false;
            _pendingPersistedWindGaugeReceiveConnected = false;
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

        public void DisconnectReceive()
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
                    //CWS,CWR and CWO are of length 6
                    if (buffer.Count >6  && buffer[4] !=(byte)'I')
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
                                tcpCommand = TcpCommand.Roller_Time_Mode_Clear;
                                Application.Current?.Dispatcher?.BeginInvoke(() => ProcessReceivedDisplayFrame((TcpCommand)tcpCommand, new List<char>() { }));
                                break;
                            case (byte)'I':
                                // Handle case for 'I'
                                tcpCommand = TcpCommand.Roller_Time_of_Day_or_Running_Time_;
                                var chars = buffer.Skip(2).Select(b => (char)b).ToList();
                                Application.Current?.Dispatcher?.BeginInvoke(() => ProcessReceivedDisplayFrame((TcpCommand)tcpCommand,chars));
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
                                string durationStr = duration.ToString("D2");
                                List<char> chars = new List<char>() { durationStr[0], durationStr[1] };
                                Application.Current?.Dispatcher?.BeginInvoke(() => ProcessReceivedWindGaugeFrame((TcpCommand)tcpCommand, chars));
                                break;
                            case (byte)'S':
                                // Handle case for 'S'
                                tcpCommand = TcpCommand.WindGauge_Start_of_Measurement;
                                Application.Current?.Dispatcher?.BeginInvoke(() => ProcessReceivedWindGaugeFrame((TcpCommand)tcpCommand, new List<char>() { }));
                                break;
                            case (byte)'R':
                                // Handle case for 'R'
                                tcpCommand = TcpCommand.WindGauge_Reset_Stop_Clear;
                                Application.Current?.Dispatcher?.BeginInvoke(() => ProcessReceivedWindGaugeFrame((TcpCommand)tcpCommand, new List<char>() { }));
                                break;
                            case (byte)'O':
                                // Handle case for 'O'
                                tcpCommand = TcpCommand.WindGauge_Resend_Latest;
                                Application.Current?.Dispatcher?.BeginInvoke(() => ProcessReceivedWindGaugeFrame((TcpCommand)tcpCommand, new List<char>() { }));
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
        

        private void OnSendPortDataReceived(byte[] data)
        {
            foreach (byte b in data)
            {
                _sendPortReceiveBuffer.Add(b);

                // Check if buffer exceeds expected size for WindGauge_Output
                var windGaugeOutputCmd = TcpCommandDefinitions.Commands[TcpCommand.WindGauge_Output];
                if (_sendPortReceiveBuffer.Count > windGaugeOutputCmd.Count())
                {
                    _sendPortReceiveBuffer.Clear();
                    continue;
                }

                // Check if we have a complete frame (starts with SOH and ends with EOT)
                if (_sendPortReceiveBuffer.Count >= 1)
                {
                    if (_sendPortReceiveBuffer[0] != 0x01) // SOH
                    {
                        _sendPortReceiveBuffer.Clear();
                        continue;
                    }
                    if (_sendPortReceiveBuffer.Count == windGaugeOutputCmd.Count())
                    {
                        string csv = string.Join(",", _sendPortReceiveBuffer.Select(b => CharCommandToString((CharCommand)b)));
                        Status += $"  Received: {csv}";
                        byte lastByte = _sendPortReceiveBuffer[_sendPortReceiveBuffer.Count - 1];
                        if (lastByte == 0x04) // EOT only
                        {
                            // Complete frame received, process it
                            ProcessSendPortFrame(_sendPortReceiveBuffer.ToArray());
                            _sendPortReceiveBuffer.Clear();
                        }
                        else
                        {
                            _sendPortReceiveBuffer.Clear();
                            continue;
                        }
                    }
                }
            }
        }

        private void ProcessSendPortFrame(byte[] frame)
        {
            // Check if this is a WindGauge_Output command (starts with SOH, then 'W')
            if (frame.Length >= 3 && frame[0] == 0x01 && frame[3] == (byte)'W')
            {

                // Validate against expected structure
                var expectedCmd = TcpCommandDefinitions.Commands[TcpCommand.WindGauge_Output];
                if (frame.Length != expectedCmd.Count())
                {
                    // Length mismatch, ignore
                    return;
                }
                //Check here
                // Validate each byte against expected command, except speed data positions
                // Speed data is at positions 10 (sign), 11 (digit1), 12 (digit2), 14 (digit3)
                for (int i = 0; i < frame.Length; i++)
                {
                    if (i == 10 || i == 11 || i == 12 || i == 14)
                    {
                        // Skip speed data positions
                        continue;
                    }
                    if (frame[i] != (byte)expectedCmd.ElementAt(i))
                    {
                        // Byte mismatch, ignore frame
                        return;
                    }
                }

                // Extract speed data from the command
                // Format: STX, W, ..., sign, digit1, digit2, ., digit3, ..., EOT/ETX
                // The speed is at positions based on the WindGauge_Output command definition
                // According to SendResult, the speed data starts at index 10
                if (frame.Length > 14)
                {
                    char signChar = (char)frame[10];
                    char digit1 = (char)frame[11];
                    char digit2 = (char)frame[12];
                    char digit3 = (char)frame[14]; // Skip the dot at index 13

                    // Validate that these are digit characters
                    if (char.IsDigit(digit1) && char.IsDigit(digit2) && char.IsDigit(digit3))
                    {
                        double speed = (digit1 - '0') * 10 + (digit2 - '0') + (digit3 - '0') / 10.0;
                        if (signChar == '-')
                        {
                            speed = -speed;
                        }

                        // Update the wind gauge display with the received speed only if not displaying simulator speed
                        if (!DisplaySimulatorSpeed)
                        {
                            UpdateInAppWindGaugeDisplayFromReceivedData(speed);
                        }
                    }
                }
            }
        }

        private void UpdateInAppWindGaugeDisplayFromReceivedData(double speed)
        {
            ShowDecimalDot = true;
            int wholePart = (int)Math.Abs(speed);
            int decimalPart = (int)(Math.Abs(speed * 10) % 10);
            string sign = speed < 0 ? "-" : " ";

            WindGaugeDisplay = $"{sign}{wholePart:00}{decimalPart}";
            Status = $"Received Wind Speed: {speed}";
        }


        private void ProcessReceivedDisplayFrame(TcpCommand tcpCommand, List<char> chars )
        {

            if (tcpCommand == TcpCommand.Roller_Time_of_Day_or_Running_Time_)
            {
                
                RollerTimeofDayorRunningTime(chars);
            }
            else if (tcpCommand == TcpCommand.Roller_Time_Mode_Clear)
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
            DisconnectReceive();
            _serialPortService.DataReceived -= OnSendPortDataReceived;
            _serialPortService.Dispose();
        }

        private sealed partial class PersistedSettings : ObservableObject
        {
            [ObservableProperty] private string? _displaySendPortName;
            [ObservableProperty] private bool _displaySendPortConnected = false;
            [ObservableProperty] private string? _displayReceivePortName;
            [ObservableProperty] private bool _displayReceivePortConnected = false;
            [ObservableProperty] private string? _windGaugeSendPortName;
            [ObservableProperty] private bool _windGaugeSendPortConnected = false;
            [ObservableProperty] private string? _windGaugeReceivePortName;
            [ObservableProperty] private bool _windGaugeReceiveConnected = false;
            [ObservableProperty] private bool _onlyProlific = true;
            [ObservableProperty] private bool _anchorDisplay;
            [ObservableProperty] private int _numDigits = 6;
            [ObservableProperty] private RaceDistance _raceDistance = RaceDistance.Distance600m;
            [ObservableProperty] private bool _displaySimulatorSpeed = false;
            [ObservableProperty] private bool _hideSimulator = false;
            [ObservableProperty] private string? _windGaugeCaptureCountdown;
            [ObservableProperty] private bool _showWindGaugeWindow = false;
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
                DisplaySimulatorSpeed = settings.DisplaySimulatorSpeed;
                HideSimulator = settings.HideSimulator;
                WindGaugeCaptureCountdown = settings.WindGaugeCaptureCountdown ?? "10";
                ShowWindGaugeWindow = settings.ShowWindGaugeWindow;

                // Load the appropriate ports based on which window should be shown
                if (ShowWindGaugeWindow)
                {
                    // Load WindGauge ports
                    if (!string.IsNullOrWhiteSpace(settings.WindGaugeSendPortName))
                    {
                        _pendingPersistedSendPortName = settings.WindGaugeSendPortName;
                    }
                    if (!string.IsNullOrWhiteSpace(settings.WindGaugeReceivePortName))
                    {
                        _pendingPersistedReceivePortName = settings.WindGaugeReceivePortName;
                    }
                    _pendingPersistedWindGaugeSendPortConnected = settings.WindGaugeSendPortConnected;
                    _pendingPersistedWindGaugeReceiveConnected = settings.WindGaugeReceiveConnected;
                }
                else
                {
                    // Load MainWindow ports
                    if (!string.IsNullOrWhiteSpace(settings.DisplaySendPortName))
                    {
                        _pendingPersistedSendPortName = settings.DisplaySendPortName;
                    }
                    if (!string.IsNullOrWhiteSpace(settings.DisplayReceivePortName))
                    {
                        _pendingPersistedReceivePortName = settings.DisplayReceivePortName;
                    }
                    _pendingPersistedSendPortConnected = settings.DisplaySendPortConnected;
                    _pendingPersistedReceivePortConnected = settings.DisplayReceivePortConnected;
                }
            }
            catch
            {
            }
        }

        public void SavePersistedPortNames()
        {
            try
            {
                Directory.CreateDirectory(SettingsDirectoryPath);
                var settings = new PersistedSettings
                {
                    DisplaySendPortName = SelectedSendPortName,
                    DisplayReceivePortName = SelectedReceivePortName,
                    OnlyProlific = OnlyProlific,
                    AnchorDisplay = AnchorDisplay,
                    NumDigits = NumDigits,
                    RaceDistance = RaceDistance,
                    DisplaySimulatorSpeed = _displaySimulatorSpeed,
                    HideSimulator = _hideSimulator,
                    WindGaugeCaptureCountdown = WindGaugeCaptureCountdown,
                    ShowWindGaugeWindow = _showWindGaugeWindow,
                };

                // Save to window-specific properties based on current window
                if (ShowWindGaugeWindow)
                {
                    settings.WindGaugeSendPortName = SelectedSendPortName;
                    settings.WindGaugeSendPortConnected = IsConnected;
                    settings.WindGaugeReceivePortName = SelectedReceivePortName;
                    settings.WindGaugeReceiveConnected = IsReceiveConnected;
                }
                else
                {
                    settings.DisplaySendPortName = SelectedSendPortName;
                    settings.DisplaySendPortConnected = IsConnected;
                    settings.DisplayReceivePortName = SelectedReceivePortName;
                    settings.DisplayReceivePortConnected = IsReceiveConnected;
                }

                var json = JsonSerializer.Serialize(settings);
                File.WriteAllText(SettingsFilePath, json);
                
                // Copy settings file path to clipboard
                Clipboard.SetText(SettingsFilePath);
            }
            catch
            {
            }
        }

        public void SetIsLoadingSettings(bool value)
        {
            _isLoadingSettings = value;
        }

        private void SwitchToWindowPorts(bool showWindGaugeWindow)
        {
            // Disconnect current ports before switching
            Disconnect();
            DisconnectReceive();

            // Load current settings to get the window-specific ports
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

                _isLoadingSettings = true;

                if (showWindGaugeWindow)
                {
                    // Switch to WindGauge ports
                    SelectedSendPortName = settings.WindGaugeSendPortName;
                    SelectedReceivePortName = settings.WindGaugeReceivePortName;
                }
                else
                {
                    // Switch to MainWindow ports
                    SelectedSendPortName = settings.DisplaySendPortName;
                    SelectedReceivePortName = null; // MainWindow doesn't use receive port
                }

                _isLoadingSettings = false;

                // Auto-connect based on the new ports
                AutoConnectIfNeeded();
            }
            catch
            {
                _isLoadingSettings = false;
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

                // Only enforce mutual exclusion for Display window (not Wind Gauge)
                if (!ShowWindGaugeWindow
                    && !string.IsNullOrWhiteSpace(SelectedSendPortName)
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
    }
}
