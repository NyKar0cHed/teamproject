using PollClient.Polls;
using System;
using System.Threading.Tasks;

namespace PollClient.Polls
{
    public class PollNavigator
    {
        private ClientConnection _connection;
        private PollSession _poll;

        public PollNavigator(ClientConnection connection, PollSession poll)
        {
            _connection = connection;
            _poll = poll;
        }

        public async Task RunAsync()
        {
            if (!_poll.IsComplete || _poll.Questions.Count == 0)
            {
                Console.WriteLine("Опитування ще не отримане або не містить питань.");
                return;
            }

            int current = 0;
            while (true)
            {
                Console.Clear();
                PollQuestion q = _poll.Questions[current];
                Console.WriteLine($"Опитування №{_poll.Id} — питання {current + 1}/{_poll.Questions.Count}");
                Console.WriteLine(q.Text);
                for (int i = 0; i < q.Options.Count; i++)
                {
                    string mark = _poll.TryGetAnswer(current, out int chosen) && chosen == i
                        ? " [обрано]" : "";
                    Console.WriteLine($"{i + 1}. {q.Options[i]}{mark}");
                }

                Console.WriteLine("\nВведіть номер варіанту, N — далі, P — назад, M — меню, F — відправити.");
                string input = (Console.ReadLine() ?? "").Trim();

                if (int.TryParse(input, out int number))
                {
                    if (number < 1 || number > q.Options.Count)
                    {
                        Console.WriteLine("Некоректний варіант. Enter...");
                        Console.ReadLine();
                    }
                    else _poll.SetAnswer(current, number - 1);
                    continue;
                }

                switch (input.ToUpperInvariant())
                {
                    case "N":
                        if (current < _poll.Questions.Count - 1) current++;
                        break;
                    case "P":
                        if (current > 0) current--;
                        break;
                    case "M":
                        return;
                    case "F":
                        if (!_poll.HasAllAnswers)
                        {
                            Console.WriteLine("Дайте відповіді на всі питання. Enter...");
                            Console.ReadLine();
                            break;
                        }
                        try
                        {
                            string response = await _connection.SendAndWaitAsync(
                                $"SUBMIT_POLL|{_poll.Id}|{_poll.BuildSubmission()}",
                                s => s.StartsWith("RESULT|") || s.StartsWith("ERROR|"));
                            Console.WriteLine($"Відповідь сервера: {response}");
                        }
                        catch (Exception ex) { Console.WriteLine(ex.Message); }
                        Console.WriteLine("Enter для повернення у меню.");
                        Console.ReadLine();
                        return;
                }
            }
        }
    }
}
