using PollServer;
using PollServer.Data;
using PollServer.Model;
using System.Text;

Console.OutputEncoding = Encoding.UTF8;

PollContext db = new PollContext();

//db.Database.EnsureDeleted();
db.Database.EnsureCreated();

Server server = new Server(5000, db);

try
{
    await server.StartAsync();
}
catch (OperationCanceledException)
{

}
