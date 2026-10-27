using System;
using System.Threading.Tasks;

namespace PollClient
{
    public class AdminTools
    {
        private ClientConnection _connection;
        public AdminTools(ClientConnection connection) => _connection = connection;

        public async Task CreatePollAsync()
        {
            Console.Write("Кількість питань: ");
            if (!int.TryParse(Console.ReadLine(), out int count) || count < 1 || count > 100)
            {
                Console.WriteLine("Введіть число від 1 до 100.");
                return;
            }

            string reply = await SendAck($"NEW_POLL|{count}", s => s == "CREATE_STARTED");
            if (reply != "CREATE_STARTED") { Console.WriteLine(reply); return; }

            for (int i = 0; i < count; i++)
            {
                Console.Write($"Питання {i + 1}: ");
                string question = Console.ReadLine() ?? "";
                reply = await SendAck($"POLL_QUESTION|{i}|{question}",
                    s => s == $"QUESTION_RECEIVED|{i}");
                if (reply != $"QUESTION_RECEIVED|{i}") { Console.WriteLine(reply); return; }

                Console.Write("Кількість варіантів (мінімум 2): ");
                if (!int.TryParse(Console.ReadLine(), out int optionCount) || optionCount < 2)
                {
                    Console.WriteLine("Необхідно мінімум 2 варіанти.");
                    return;
                }

                for (int j = 0; j < optionCount; j++)
                {
                    Console.Write($"Варіант {j + 1}: ");
                    string option = Console.ReadLine() ?? "";
                    reply = await SendAck($"POLL_OPTION|{i}|{option}",
                        s => s == $"OPTION_RECEIVED|{i}");
                    if (reply != $"OPTION_RECEIVED|{i}") { Console.WriteLine(reply); return; }
                }
            }

            reply = await SendAck("END_POLL",
                s => s.StartsWith("POLL_CREATED|") || s.StartsWith("ERROR|"));
            Console.WriteLine(reply.StartsWith("POLL_CREATED|")
                ? $"Опитування створене. Збережіть ID: {reply.Split('|')[1]}"
                : $"Помилка створення: {reply}");
        }

        public async Task StartPollAsync()
        {
            Console.Write("ID опитування: ");
            if (!int.TryParse(Console.ReadLine(), out int id) || id <= 0)
            {
                Console.WriteLine("Некоректний ID.");
                return;
            }

            string reply = await SendAck($"START_POLL|{id}",
                s => s.StartsWith("POLL_STARTED|") || s.StartsWith("ERROR|"));
            Console.WriteLine(reply);
        }

        private Task<string> SendAck(string command, Func<string, bool> expected) =>
            _connection.SendAndWaitAsync(command, s => expected(s) || s.StartsWith("ERROR|"));
    }
}
