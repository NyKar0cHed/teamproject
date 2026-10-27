using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PollClient
{
    public class ClientConnection : IDisposable
    {
        private MessageReader _reader;
        private SemaphoreSlim _sendLock = new(1, 1);
        private object _waitLock = new();
        private List<Waiter> _waiters = new();
        private int _closed;

        public event Action<string>? MessageReceived;
        public event Action? Disconnected;
        public bool IsClosed => Volatile.Read(ref _closed) == 1;
        public TcpClient TcpClient { get; }
        public NetworkStream Stream { get; }

        private ClientConnection(TcpClient tcp)
        {
            TcpClient = tcp;
            Stream = tcp.GetStream();
            _reader = new MessageReader(Stream);
        }

        public static async Task<ClientConnection> ConnectAsync(string host, int port)
        {
            var tcp = new TcpClient();
            await tcp.ConnectAsync(host, port);
            return new ClientConnection(tcp);
        }

        public async Task SendMessageAsync(string message)
        {
            if (IsClosed) throw new InvalidOperationException("З'єднання закрито.");
            byte[] data = Encoding.UTF8.GetBytes(message + "<|END|>");
            await _sendLock.WaitAsync();
            try
            {
                if (IsClosed) throw new InvalidOperationException("З'єднання закрито.");
                await Stream.WriteAsync(data, 0, data.Length);
            }
            finally { _sendLock.Release(); }
        }

        public async Task<string> SendAndWaitAsync(string command, Func<string, bool> predicate, int timeoutMs = 15000)
        {
            var waiter = new Waiter(predicate);
            lock (_waitLock) _waiters.Add(waiter);
            try
            {
                await SendMessageAsync(command);
                return await waiter.Source.Task.WaitAsync(TimeSpan.FromMilliseconds(timeoutMs));
            }
            catch
            {
                lock (_waitLock) _waiters.Remove(waiter);
                throw;
            }
        }

        public async Task StartReceivingAsync(CancellationToken token = default)
        {
            try
            {
                while (!token.IsCancellationRequested && !IsClosed)
                {
                    string? message = await _reader.ReadMessageAsync();
                    if (message == null) break;
                    ResolveWaiter(message);
                    MessageReceived?.Invoke(message);
                }
            }
            catch (Exception ex) when (IsClosed || token.IsCancellationRequested) { }
            catch (Exception ex) { Console.WriteLine($"Помилка з'єднання: {ex.Message}"); }
            finally { Close(); Disconnected?.Invoke(); }
        }

        private void ResolveWaiter(string message)
        {
            Waiter? found;
            lock (_waitLock)
            {
                found = _waiters.FirstOrDefault(w => w.Predicate(message));
                if (found != null) _waiters.Remove(found);
            }
            found?.Source.TrySetResult(message);
        }

        public void Close()
        {
            if (Interlocked.Exchange(ref _closed, 1) == 1) return;
            try { Stream.Close(); } catch { }
            try { TcpClient.Close(); } catch { }
            lock (_waitLock)
            {
                foreach (var waiter in _waiters)
                    waiter.Source.TrySetException(new InvalidOperationException("З'єднання закрито."));
                _waiters.Clear();
            }
        }

        public void Dispose()
        {
            Close();
            _sendLock.Dispose();
        }

        private class Waiter
        {
            public Func<string, bool> Predicate { get; }
            public TaskCompletionSource<string> Source { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
            public Waiter(Func<string, bool> predicate) => Predicate = predicate;
        }
    }
}
