using Credis;
using Credis.Utils;
using System.Text;

var server = new Server(true);
server.Env = App.Constants.Env.TEST; // Test server

var cancellationTokenSource = new CancellationTokenSource();
var cancellationToken = cancellationTokenSource.Token;

Console.WriteLine("===Welcome to Credis===");
Console.WriteLine("===Server config===");
if (server.Env == App.Constants.Env.TEST)
{
    Console.WriteLine("Ip Address: LOCAL");
}
else
{
    Console.WriteLine("Ip Address: " + server.IpAddress);
}
Console.WriteLine("Port: " + server.Port);

Console.WriteLine(App.Constants.Terminal.Text.LOOP_BASIC);

bool sessionKeepAlive = true;
while (sessionKeepAlive)
{
    string? userInput = Console.ReadLine();
    if (!string.IsNullOrWhiteSpace(userInput))
    {
        switch (userInput.Trim().ToLower())
        {
            case "y":
                try
                {
                    GlobalVariables.Initialize(Encoding.UTF8);
                    await server.Init(cancellationToken);
                    Console.WriteLine($"{App.Constants.Terminal.Text.SERVER_INITIALIZED}{server.IpAddress}:{server.Port}");
                }
                catch (Exception ex)
                {
                    if (ex.InnerException is ApplicationException)
                    {
                        Console.WriteLine($"" + ex.Message);
                    }
                    else if (ex.InnerException is ArgumentException)
                    {
                        Console.WriteLine($"" + ex.Message);
                    }
                    else if (ex.InnerException is ArgumentNullException)
                    {
                        Console.WriteLine($"" + ex.Message);
                    }
                    else if (ex.InnerException is ArgumentOutOfRangeException)
                    {
                        Console.WriteLine($"" + ex.Message);
                    }
                    else
                    {
                        Console.WriteLine(ex.Message);
                    }
                }
                break;
            case "n":
            case "exit":
                sessionKeepAlive = false;
                break;
            case "change config":
                break;
            default:
                Console.WriteLine(App.Constants.Terminal.Text.INVALID_INPUT);
                return;
        }
    }
}