using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO.Ports;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using SwissTimingDisplay.Models;
using SwissTimingDisplay.Services;

namespace SwissTimingDisplay.ViewModels
{
    public enum DisplayMode
    {
        MMSSDD,
        HHMMSS,
    }

    public sealed class MainViewModel : INotifyPropertyChanged, IDisposable
    {
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
        private string _status = "";
        private DisplayMode _displayMode = DisplayMode.MMSSDD;
        private bool _cosmetic = false;
        private bool _isReceiveConnected;
        private string? _connectedReceivePortName;
        private bool _isUpdatingPortLists;
        private bool _isSyncingSelectedPorts;

        private SerialPort? _receivePort;
        private CancellationTokenSource? _receiveCts;
        private Task? _receiveTask;

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

            TcpCommands = new ObservableCollection<TcpCommand>(Enum.GetValues<TcpCommand>());
            SelectedTcpCommand = TcpCommands.FirstOrDefault();

            _sendPortsViewSource.Source = Ports;
            _sendPortsViewSource.Filter += SendPortsViewSourceOnFilter;

            _receivePortsViewSource.Source = Ports;
            _receivePortsViewSource.Filter += ReceivePortsViewSourceOnFilter;

            RefreshPorts();
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
                    RefreshPorts();
                }
            }
        }

        public bool IsConnected => _serialPortService.IsConnected;

        public string? ConnectedPortName => _serialPortService.ConnectedPortName;

        public bool IsReceiveConnected
        {
            get => _isReceiveConnected;
            private set => Set(ref _isReceiveConnected, value);
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
                if (!IsReceiveConnected)
                {
                    return TimeInput;
                }

                if (!Cosmetic)
                {
                    return TimeInputIn;
                }

                var raw = TimeInputIn ?? string.Empty;
                var digits = new string(raw.Where(char.IsDigit).ToArray());
                if (digits.Length < 6)
                {
                    return raw;
                }

                digits = digits[..6];

                if (DisplayMode == DisplayMode.HHMMSS)
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
                    OnPropertyChanged(nameof(DisplayTime));
                }
            }
        }

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
                }
            }
        }

        public bool AnchorDisplay
        {
            get => _anchorDisplay;
            set => Set(ref _anchorDisplay, value);
        }

        public string BibNo
        {
            get => _bibNo;
            set => Set(ref _bibNo, value);
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

        private async Task ReceiveLoopAsync(SerialPort port, CancellationToken cancellationToken)
        {
            var buffer = new byte[256];
            var frame = new List<byte>(256);

            // We dispatch frames as: [cmd, payload...]
            // where cmd is 'I' or 'B'. STX and terminator (EOT/ETX) are not included.
            var inFrame = false;
            var gotCmd = false;

            while (!cancellationToken.IsCancellationRequested)
            {
                int read;
                try
                {
                    read = await port.BaseStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch
                {
                    break;
                }

                if (read <= 0)
                {
                    continue;
                }

                for (var i = 0; i < read; i++)
                {
                    var b = buffer[i];

                    if (!inFrame)
                    {
                        if (b == (byte)CharCommand.STX)
                        {
                            frame.Clear();
                            inFrame = true;
                            gotCmd = false;
                        }

                        continue;
                    }

                    // In frame: first byte must be command
                    if (!gotCmd)
                    {
                        if (b == (byte)CharCommand.I || b == (byte)CharCommand.B)
                        {
                            frame.Add(b);
                            gotCmd = true;
                            continue;
                        }

                        // Invalid cmd - reset and hunt for next STX
                        frame.Clear();
                        inFrame = false;
                        gotCmd = false;
                        continue;
                    }

                    // End-of-frame: accept EOT (per spec) and ETX (current definitions)
                    if (b == (byte)CharCommand.EOT || b == (byte)CharCommand.ETX)
                    {
                        var completed = frame.ToArray();
                        frame.Clear();
                        inFrame = false;
                        gotCmd = false;

                        Application.Current?.Dispatcher?.BeginInvoke(() => ProcessReceivedFrame(completed));
                        continue;
                    }

                    frame.Add(b);
                }
            }
        }

        private void ProcessReceivedFrame(byte[] frameBytes)
        {
            if (frameBytes.Length == 0)
            {
                return;
            }

            var cmd = (CharCommand)frameBytes[0];

            if (cmd == CharCommand.B)
            {
                RollerTimeofDayorRunningTimeClear();
                return;
            }

            if (cmd == CharCommand.I)
            {
                var chars = frameBytes.Skip(1).Select(b => (char)b).ToList();
                RollerTimeofDayorRunningTime(chars);
            }
        }

        private void RollerTimeofDayorRunningTimeClear()
        {
            TimeInputIn = string.Empty;
        }

        private void RollerTimeofDayorRunningTime(IReadOnlyList<char> chars)
        {
            TimeInputIn = new string(chars.ToArray());
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
            _serialPortService.Dispose();
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
