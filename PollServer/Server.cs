using Microsoft.EntityFrameworkCore;
using PollServer.Data;
using PollServer.Model;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace PollServer
{
    public class Server
    {
        private TcpListener _listener;
        private PollContext _db;

        private ConcurrentDictionary<string, ClientConnection> _clients = new();
        private ConcurrentDictionary<string, PollCreationDraft> _creationDrafts = new();
        private ConcurrentDictionary<string, PollResponse> _responses = new();
        private SemaphoreSlim _dbLock = new(1, 1);
        private ConcurrentDictionary<string, SemaphoreSlim> _submissionLocks = new();

        private CancellationTokenSource _cts = new();
        private Poll? _activePoll;

        public Server(int port, PollContext db)
        {
            _listener = new TcpListener(IPAddress.Any, port);
            _db = db ?? throw new ArgumentNullException(nameof(db));
        }

        public async Task StartAsync()
        {
            _listener.Start();
            Console.WriteLine("Сервер запущено");

            try
            {
                await ReceiveConnectionsAsync(_cts.Token);
            }
            catch (OperationCanceledException)
            {

            }
            catch (SocketException) when (_cts.IsCancellationRequested)
            {

            }
        }

        private async Task ReceiveConnectionsAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                TcpClient tcpClient = await _listener.AcceptTcpClientAsync(cancellationToken);
                var client = new ClientConnection(tcpClient);

                if (!_clients.TryAdd(client.Id, client))
                {
                    client.Dispose();
                    continue;
                }

                Console.WriteLine($"Підключився клієнт {client.Id}");

                try
                {
                    await client.SendMessageAsync(PollProtocol.Welcome + "|ADMIN"); //!!

                    Poll? active = _activePoll;
                    if (active != null)
                        await SendPollAsync(client, active);

                    _ = HandleClientAsync(client, cancellationToken);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Не вдалося ініціалізувати клієнта: {ex.Message}");
                    RemoveClient(client);
                }
            }
        }

        private async Task HandleClientAsync(ClientConnection client, CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested && !client.IsClosed)
                {
                    string? message = await client.ReadMessageAsync();

                    if (message == null)
                        break;

                    if (message == PollProtocol.Exit)
                    {
                        await client.SendMessageAsync(PollProtocol.Bye);
                        break;
                    }

                    await ProcessMessageAsync(client, message);
                }
            }
            catch (OperationCanceledException)
            {

            }
            catch (Exception ex)
            {
                Console.WriteLine($"Помилка клієнта {client.Id}: {ex.Message}");
            }
            finally
            {
                RemoveClient(client);
            }
        }

        private void RemoveClient(ClientConnection client)
        {
            _clients.TryRemove(client.Id, out _);
            _creationDrafts.TryRemove(client.Id, out _);
            client.Dispose();
            Console.WriteLine($"Відключився клієнт {client.Id}");
        }

        private async Task ProcessMessageAsync(ClientConnection client, string message)
        {
            string[] parts = message.Split('|');

            switch (parts[0])
            {
                case PollProtocol.NewPoll:
                    await BeginPollCreationAsync(client, parts);
                    break;

                case PollProtocol.PollQuestion:
                    await ReceiveQuestionAsync(client, message);
                    break;

                case PollProtocol.PollOption:
                    await ReceiveOptionAsync(client, message);
                    break;

                case PollProtocol.EndPoll:
                    await FinishPollCreationAsync(client);
                    break;

                case PollProtocol.StartPoll:
                    await StartPollAsync(client, parts);
                    break;

                case PollProtocol.SubmitPoll:
                    await SubmitPollAsync(client, message);
                    break;

                default:
                    await client.SendMessageAsync($"{PollProtocol.Error}|UNKNOWN_COMMAND");
                    break;
            }
        }

        private async Task BeginPollCreationAsync(ClientConnection client, string[] parts)
        {
            if (parts.Length != 2 ||
                !int.TryParse(parts[1], out int questionCount) ||
                questionCount < 1 ||
                questionCount > 100)
            {
                await client.SendMessageAsync($"{PollProtocol.Error}|INVALID_QUESTION_COUNT");
                return;
            }

            _creationDrafts[client.Id] = new PollCreationDraft(questionCount);
            await client.SendMessageAsync(PollProtocol.CreateStarted);
        }

        private async Task ReceiveQuestionAsync(ClientConnection client, string message)
        {
            string[] parts = message.Split('|', 3);

            if (parts.Length != 3 ||
                !int.TryParse(parts[1], out int questionIndex) ||
                string.IsNullOrWhiteSpace(parts[2]))
            {
                await client.SendMessageAsync($"{PollProtocol.Error}|INVALID_QUESTION_FORMAT");
                return;
            }

            if (!_creationDrafts.TryGetValue(client.Id, out PollCreationDraft? draft))
            {
                await client.SendMessageAsync($"{PollProtocol.Error}|CREATE_NOT_STARTED");
                return;
            }

            if (questionIndex < 0 || questionIndex >= draft.ExpectedQuestionCount)
            {
                await client.SendMessageAsync($"{PollProtocol.Error}|INVALID_QUESTION_INDEX");
                return;
            }

            draft.Questions[questionIndex] = parts[2];

            if (!draft.Options.ContainsKey(questionIndex))
                draft.Options[questionIndex] = new List<string>();

            await client.SendMessageAsync($"{PollProtocol.QuestionReceived}|{questionIndex}");
        }

        private async Task ReceiveOptionAsync(ClientConnection client, string message)
        {
            string[] parts = message.Split('|', 3);

            if (parts.Length != 3 ||
                !int.TryParse(parts[1], out int questionIndex) ||
                string.IsNullOrWhiteSpace(parts[2]))
            {
                await client.SendMessageAsync($"{PollProtocol.Error}|INVALID_OPTION_FORMAT");
                return;
            }

            if (!_creationDrafts.TryGetValue(client.Id, out PollCreationDraft? draft))
            {
                await client.SendMessageAsync($"{PollProtocol.Error}|CREATE_NOT_STARTED");
                return;
            }

            if (questionIndex < 0 || questionIndex >= draft.ExpectedQuestionCount ||
                !draft.Questions.ContainsKey(questionIndex))
            {
                await client.SendMessageAsync($"{PollProtocol.Error}|QUESTION_NOT_FOUND");
                return;
            }

            if (!draft.Options.TryGetValue(questionIndex, out List<string>? options))
            {
                options = new List<string>();
                draft.Options[questionIndex] = options;
            }

            options.Add(parts[2]);
            await client.SendMessageAsync($"{PollProtocol.OptionReceived}|{questionIndex}");
        }

        private async Task FinishPollCreationAsync(ClientConnection client)
        {
            if (!_creationDrafts.TryGetValue(client.Id, out PollCreationDraft? draft))
            {
                await client.SendMessageAsync($"{PollProtocol.Error}|CREATE_NOT_STARTED");
                return;
            }

            var questions = new List<PollQuestion>();

            for (int i = 0; i < draft.ExpectedQuestionCount; i++)
            {
                if (!draft.Questions.TryGetValue(i, out string? questionText) ||
                    string.IsNullOrWhiteSpace(questionText))
                {
                    await client.SendMessageAsync($"{PollProtocol.Error}|MISSING_QUESTION|{i}");
                    return;
                }

                if (!draft.Options.TryGetValue(i, out List<string>? optionTexts) ||
                    optionTexts.Count < 2)
                {
                    await client.SendMessageAsync($"{PollProtocol.Error}|NOT_ENOUGH_OPTIONS|{i}");
                    return;
                }

                var question = new PollQuestion
                {
                    Text = questionText,
                    Options = optionTexts.Select(text => new PollOption { Text = text }).ToList()
                };

                questions.Add(question);
            }

            var poll = new Poll { Questions = questions };

            try
            {
                await _dbLock.WaitAsync();
                try
                {
                    _db.Polls.Add(poll);
                    await _db.SaveChangesAsync();
                }
                finally
                {
                    _dbLock.Release();
                }

                _creationDrafts.TryRemove(client.Id, out _);
                await client.SendMessageAsync($"{PollProtocol.PollCreated}|{poll.Id}");
                Console.WriteLine($"Опитування створено. ID: {poll.Id}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Помилка збереження опитування: {ex.Message}");
                await client.SendMessageAsync($"{PollProtocol.Error}|DATABASE_ERROR");
            }
        }

        private async Task StartPollAsync(ClientConnection client, string[] parts)
        {
            if (parts.Length != 2 || !int.TryParse(parts[1], out int pollId))
            {
                await client.SendMessageAsync($"{PollProtocol.Error}|INVALID_POLL_ID");
                return;
            }

            Poll? poll;

            try
            {
                await _dbLock.WaitAsync();
                try
                {
                    poll = await _db.Polls
                        .Include(p => p.Questions)
                        .ThenInclude(q => q.Options)
                        .FirstOrDefaultAsync(p => p.Id == pollId);
                }
                finally
                {
                    _dbLock.Release();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Помилка завантаження опитування: {ex.Message}");
                await client.SendMessageAsync($"{PollProtocol.Error}|DATABASE_ERROR");
                return;
            }

            if (poll == null)
            {
                await client.SendMessageAsync($"{PollProtocol.Error}|POLL_NOT_FOUND");
                return;
            }

            _activePoll = poll;

            foreach (ClientConnection connectedClient in _clients.Values)
            {
                if (!connectedClient.IsClosed)
                {
                    try
                    {
                        await SendPollAsync(connectedClient, poll);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Помилка відправки опитування клієнту {connectedClient.Id}: {ex.Message}");
                    }
                }
            }

            await client.SendMessageAsync($"{PollProtocol.PollStarted}|{poll.Id}");
        }

        private static async Task SendPollAsync(ClientConnection client, Poll poll)
        {
            await client.SendMessageAsync($"POLL|{poll.Id}");

            for (int i = 0; i < poll.Questions.Count; i++)
            {
                PollQuestion question = poll.Questions[i];
                await client.SendMessageAsync($"QUESTION|{i}|{question.Text}");

                for (int j = 0; j < question.Options.Count; j++)
                    await client.SendMessageAsync($"OPTION|{i}|{j}|{question.Options[j].Text}");
            }

            await client.SendMessageAsync("END_POLL");
        }

        private async Task SubmitPollAsync(ClientConnection client, string message)
        {
            string[] parts = message.Split('|', 3);

            if (parts.Length != 3 || !int.TryParse(parts[1], out int pollId))
            {
                await client.SendMessageAsync($"{PollProtocol.Result}|INVALID_FORMAT");
                return;
            }

            Poll? poll = _activePoll;
            if (poll == null || poll.Id != pollId)
            {
                await client.SendMessageAsync($"{PollProtocol.Result}|UNKNOWN_POLL");
                return;
            }

            var answers = new Dictionary<int, int>();

            foreach (string item in parts[2].Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                string[] pair = item.Split(':');

                if (pair.Length != 2 ||
                    !int.TryParse(pair[0], out int questionIndex) ||
                    !int.TryParse(pair[1], out int optionIndex))
                {
                    await client.SendMessageAsync($"{PollProtocol.Result}|INVALID_FORMAT");
                    return;
                }

                if (!answers.TryAdd(questionIndex, optionIndex))
                {
                    await client.SendMessageAsync($"{PollProtocol.Result}|DUPLICATE_QUESTION");
                    return;
                }

                if (questionIndex < 0 || questionIndex >= poll.Questions.Count)
                {
                    await client.SendMessageAsync($"{PollProtocol.Result}|INVALID_QUESTION");
                    return;
                }

                if (optionIndex < 0 || optionIndex >= poll.Questions[questionIndex].Options.Count)
                {
                    await client.SendMessageAsync($"{PollProtocol.Result}|INVALID_OPTION");
                    return;
                }
            }

            if (answers.Count != poll.Questions.Count)
            {
                await client.SendMessageAsync($"{PollProtocol.Result}|NOT_ALL_ANSWERED");
                return;
            }

            SemaphoreSlim submissionLock = _submissionLocks.GetOrAdd(
                $"{pollId}:{client.Id}", _ => new SemaphoreSlim(1, 1));

            await submissionLock.WaitAsync();
            try
            {
                foreach (var entry in answers)
                {
                    int questionIndex = entry.Key;
                    int optionIndex = entry.Value;
                    int questionId = poll.Questions[questionIndex].Id;
                    string responseKey = $"{pollId}:{questionId}:{client.Id}";

                    if (_responses.ContainsKey(responseKey))
                    {
                        await client.SendMessageAsync($"{PollProtocol.Result}|ALREADY_ANSWERED");
                        return;
                    }
                }

                foreach (var entry in answers)
                {
                    int questionIndex = entry.Key;
                    int optionIndex = entry.Value;
                    PollQuestion question = poll.Questions[questionIndex];
                    PollOption option = question.Options[optionIndex];

                    string responseKey = $"{pollId}:{question.Id}:{client.Id}";

                    var response = new PollResponse
                    {
                        PollId = pollId,
                        QuestionId = question.Id,
                        ClientId = client.Id,
                        OptionNumber = optionIndex,
                        Answer = option.Text,
                        ReceivedAt = DateTime.Now
                    };

                    _responses.TryAdd(responseKey, response);
                }

                await client.SendMessageAsync($"{PollProtocol.Result}|SUBMIT_ACCEPTED");
                Console.WriteLine($"Клієнт {client.Id} відправив відповіді на опитування {pollId}.");
            }
            finally
            {
                submissionLock.Release();
            }
        }

        public List<PollResponse> GetResponses()
        {
            return _responses.Values.ToList();
        }

        public async Task StopAsync()
        {
            if (_cts.IsCancellationRequested)
                return;

            _cts.Cancel();
            _listener.Stop();

            foreach (ClientConnection client in _clients.Values)
                client.Dispose();

            Console.WriteLine("Сервер зупинено");
            await Task.CompletedTask;
        }
    }
}
