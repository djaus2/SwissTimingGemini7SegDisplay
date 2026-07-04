using System;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;

namespace SwissTimingDisplay.Services
{
    public sealed class SerialPortService : IDisposable
    {
        private SerialPort? _port;
        private CancellationTokenSource? _receiveCts;
        private Task? _receiveTask;

        public event Action<byte[]>? DataReceived;

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

            // Start receive loop
            _receiveCts = new CancellationTokenSource();
            _receiveTask = Task.Run(() => ReceiveLoopAsync(port, _receiveCts.Token));
        }

        public void Disconnect()
        {
            var port = _port;
            _port = null;

            // Stop receive loop
            var cts = _receiveCts;
            _receiveCts = null;
            if (cts is not null)
            {
                cts.Cancel();
                try
                {
                    _receiveTask?.Wait(1000);
                }
                catch (AggregateException)
                {
                    // Task was cancelled
                }
                _receiveTask = null;
            }

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
                try { port.Write(payload, 0, payload.Length); } catch (Exception) { }
            }, cancellationToken);
        }

        private async Task ReceiveLoopAsync(SerialPort port, CancellationToken cancellationToken)
        {
            var buffer = new byte[1024];

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    int bytesRead = await port.BaseStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
                    if (bytesRead > 0)
                    {
                        var receivedData = new byte[bytesRead];
                        Array.Copy(buffer, 0, receivedData, 0, bytesRead);
                        DataReceived?.Invoke(receivedData);
                    }
                }
                catch (OperationCanceledException)
                {
                    // Normal cancellation
                    break;
                }
                catch (Exception)
                {
                    // Ignore other errors and continue
                    await Task.Delay(100, cancellationToken);
                }
            }
        }

        public void Dispose()
        {
            Disconnect();
        }
    }
}
