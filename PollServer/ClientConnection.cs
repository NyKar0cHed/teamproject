using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PollServer
{
    public class ClientConnection : IDisposable
    {
        private MessageReader _reader;
        private SemaphoreSlim _sendLock = new SemaphoreSlim(1, 1);
        private int _isClosed;

        public string Id { get; } = Guid.NewGuid().ToString();
        public TcpClient TcpClient { get; }
        public NetworkStream Stream { get; }
        public bool IsClosed => Volatile.Read(ref _isClosed) == 1;

        public ClientConnection(TcpClient tcpClient)
        {
            TcpClient = tcpClient ?? throw new ArgumentNullException(nameof(tcpClient));
            Stream = tcpClient.GetStream();
            _reader = new MessageReader(Stream);
        }

        public async Task SendMessageAsync(string message)
        {
            if (IsClosed)
                return;

            byte[] bytes = Encoding.UTF8.GetBytes(message + "<|END|>");
            await _sendLock.WaitAsync();

            try
            {
                if (!IsClosed)
                    await Stream.WriteAsync(bytes, 0, bytes.Length);
            }
            finally
            {
                _sendLock.Release();
            }
        }

        public Task<string?> ReadMessageAsync()
        {
            if (IsClosed)
                return Task.FromResult<string?>(null);

            return _reader.ReadMessageAsync();
        }

        public void Close()
        {
            if (Interlocked.Exchange(ref _isClosed, 1) == 1)
                return;

            try { Stream.Close(); } catch { }
            try { TcpClient.Close(); } catch { }
        }

        public void Dispose()
        {
            Close();
            _sendLock.Dispose();
        }
    }
}
