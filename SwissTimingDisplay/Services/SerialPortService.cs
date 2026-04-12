using System;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;

namespace SwissTimingDisplay.Services
{
    public sealed class SerialPortService : IDisposable
    {
        private SerialPort? _port;

        public bool IsConnected => _port?.IsOpen == true;
        public string? ConnectedPortName => _port?.PortName;

        public void Connect(string portName, int baudRate = 9600)
        {
            if (IsConnected)
            {
                Disconnect();
            }

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
            _port = port;
        }

        public void Disconnect()
        {
            var port = _port;
            _port = null;

            if (port is null)
            {
                return;
            }

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

        public Task SendAsync(byte[] payload, CancellationToken cancellationToken = default)
        {
            var port = _port;
            if (port is null || !port.IsOpen)
            {
                throw new InvalidOperationException("Serial port is not connected.");
            }

            return Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                port.Write(payload, 0, payload.Length);
            }, cancellationToken);
        }

        public void Dispose()
        {
            Disconnect();
        }
    }
}
