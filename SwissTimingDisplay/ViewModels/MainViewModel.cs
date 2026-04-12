using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using SwissTimingDisplay.Models;
using SwissTimingDisplay.Services;

namespace SwissTimingDisplay.ViewModels
{
    public sealed class MainViewModel : INotifyPropertyChanged, IDisposable
    {
        private readonly SerialPortDiscoveryService _discoveryService = new SerialPortDiscoveryService();
        private readonly SerialPortService _serialPortService = new SerialPortService();

        private SerialPortInfo? _selectedPort;
        private TcpCommand _selectedTcpCommand;
        private bool _onlyProlific = true;
        private string _timeInput = "";
        private bool _useWallClockTimeOfDay = false;
        private bool _anchorDisplay = false;
        private string _bibNo = "";
        private string _status = "";

        public MainViewModel()
        {
            RefreshPortsCommand = new RelayCommand(RefreshPorts);
            ConnectCommand = new RelayCommand(Connect, () => SelectedPort is not null && !IsConnected);
            DisconnectCommand = new RelayCommand(Disconnect, () => IsConnected);
            SendCommand = new RelayCommand(async () => await SendAsync(), () => IsConnected);

            TcpCommands = new ObservableCollection<TcpCommand>(Enum.GetValues<TcpCommand>());
            SelectedTcpCommand = TcpCommands.FirstOrDefault();

            RefreshPorts();
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public ObservableCollection<SerialPortInfo> Ports { get; } = new ObservableCollection<SerialPortInfo>();

        public ObservableCollection<TcpCommand> TcpCommands { get; }

        public SerialPortInfo? SelectedPort
        {
            get => _selectedPort;
            set
            {
                if (Set(ref _selectedPort, value))
                {
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

        public string TimeInput
        {
            get => _timeInput;
            set => Set(ref _timeInput, value);
        }

        public bool UseWallClockTimeOfDay
        {
            get => _useWallClockTimeOfDay;
            set => Set(ref _useWallClockTimeOfDay, value);
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

                if (SelectedPort is null || Ports.All(p => p.PortName != SelectedPort.PortName))
                {
                    SelectedPort = Ports.FirstOrDefault();
                }

                Status = $"Found {Ports.Count} port(s).";
            }
            catch (Exception ex)
            {
                Status = ex.Message;
            }
        }

        public void Connect()
        {
            if (SelectedPort is null)
            {
                Status = "Select a COM port.";
                return;
            }

            try
            {
                _serialPortService.Connect(SelectedPort.PortName);
                Status = $"Connected to {SelectedPort.PortName}.";
                OnPropertyChanged(nameof(IsConnected));
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
                RaiseCommandStates();
            }
            catch (Exception ex)
            {
                Status = ex.Message;
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
            _serialPortService.Dispose();
        }

        private void RaiseCommandStates()
        {
            ConnectCommand.RaiseCanExecuteChanged();
            DisconnectCommand.RaiseCanExecuteChanged();
            SendCommand.RaiseCanExecuteChanged();
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
