using PollClient;
using System;
using System.Text;
using System.Threading.Tasks;

Console.OutputEncoding = Encoding.UTF8;
    
try
{
    using ClientConnection connection = await ClientConnection.ConnectAsync("127.0.0.1", 5000);
    await new ClientApp(connection).RunAsync();
}
catch (Exception ex)
{
    Console.WriteLine($"Не вдалося підключитися до 127.0.0.1:5000 : {ex.Message}");
}