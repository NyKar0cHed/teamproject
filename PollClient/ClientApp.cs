using PollClient.Polls;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace PollClient
{
    public class ClientApp
    {
        private ClientConnection _connection;
        private AdminTools _admin;
        private object _pollLock = new();
        private CancellationTokenSource _cts = new();
        private PollSession? _incoming;
        private PollSession? _current;

        public ClientApp(ClientConnection connection)
        {
            _connection = connection;
            _admin = new AdminTools(connection);
            _connection.MessageReceived += HandleMessage;
            _connection.Disconnected += () => Console.WriteLine("З'єднання закрито.");
        }

        public async Task RunAsync()
        {
            _ = _connection.StartReceivingAsync(_cts.Token);
            bool running = true;

            while (running && !_connection.IsClosed)
            {
                Console.WriteLine("\n=== Меню опитувань ===");
                Console.WriteLine("1. Пройти отримане опитування");
                Console.WriteLine("2. Створити опитування");
                Console.WriteLine("3. Запустити опитування по ID");
                Console.WriteLine("0. Вихід");
                Console.Write("Вибір: ");

                string choice = (Console.ReadLine() ?? "").Trim();
                try
                {
                    switch (choice)
                    {
                        case "1":
                            PollSession? poll;
                            lock (_pollLock) poll = _current;
                            if (poll == null || !poll.IsComplete)
                                Console.WriteLine("Опитування ще не отримано.");
                            else await new PollNavigator(_connection, poll).RunAsync();
                            break;
                        case "2":
                            await _admin.CreatePollAsync();
                            break;
                        case "3":
                            await _admin.StartPollAsync();
                            break;
                        case "0":
                            running = false;
                            break;
                        default:
                            Console.WriteLine("Невідомий пункт.");
                            break;
                    }
                }
                catch (Exception ex) { Console.WriteLine($"Помилка: {ex.Message}"); }
            }

            try { if (!_connection.IsClosed) await _connection.SendMessageAsync(PollProtocol.Exit); }
            catch { }
            _cts.Cancel();
            _connection.Close();
        }

        private void HandleMessage(string message)
        {
            string[] parts = message.Split('|');
            switch (parts[0])
            {
                case "WELCOME":
                    Console.WriteLine("Підключено до серверу.");
                    break;

                case "POLL":
                    if (parts.Length == 2 && int.TryParse(parts[1], out int id))
                        lock (_pollLock) _incoming = new PollSession(id);
                    break;

                case "QUESTION":
                    {
                        string[] p = message.Split('|', 3);
                        if (p.Length == 3 && int.TryParse(p[1], out int index) && index >= 0)
                            lock (_pollLock)
                                if (_incoming != null) _incoming.GetOrCreateQuestion(index).Text = p[2];
                        break;
                    }

                case "OPTION":
                    {
                        string[] p = message.Split('|', 4);
                        if (p.Length == 4 && int.TryParse(p[1], out int qi) &&
                            int.TryParse(p[2], out int oi) && qi >= 0 && oi >= 0)
                        {
                            lock (_pollLock)
                            {
                                if (_incoming != null)
                                {
                                    var q = _incoming.GetOrCreateQuestion(qi);
                                    while (q.Options.Count <= oi) q.Options.Add("");
                                    q.Options[oi] = p[3];
                                }
                            }
                        }
                        break;
                    }

                case "END_POLL":
                    lock (_pollLock)
                    {
                        if (_incoming != null)
                        {
                            _incoming.IsComplete = true;
                            _current = _incoming;
                            Console.WriteLine($"Отримане опитування №{_current.Id}, питань: {_current.Questions.Count}.");
                            _incoming = null;
                        }
                    }
                    break;

                case "ERROR":
                    Console.WriteLine($"Помилка сервера: {message}");
                    break;
            }
        }
    }
}
