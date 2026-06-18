namespace Credis;

public class Program
{
    public static async Task Main(string[] args)
    {
        Server? server = null;
        CancellationTokenSource _cntSource = new CancellationTokenSource();
        CancellationToken _cnt = _cntSource.Token;

        // Ctrl+C asks the server to wind down instead of killing the process outright,
        // so the accept loop can break and the listener can be released cleanly.
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            Console.WriteLine();
            Console.WriteLine("Shutdown requested, stopping...");
            _cntSource.Cancel();
        };

        try
        {
            Console.WriteLine("===Credis Mini Server===");
            server = new Server(new Server.ServerConfig
            {

            });
            var startTask = server.StartAsync(_cnt).GetAwaiter();
            Console.WriteLine("Listening");
            while (!startTask.IsCompleted)
            {
                Console.Write(".");
                await Task.Delay(1000);
            }
            if (server.GetStatus() == Server.ServerStatus.FAULTED)
            {
                throw new Exception(server.GetFaultMessage());
            }
            if (server.GetStatus() == Server.ServerStatus.CANCELLED)
            {
                Console.WriteLine("Server stopped.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine();
            Console.WriteLine($"Server initialization failed::{ex.Message}");
        }
        finally
        {
            _cntSource.Cancel();
            _cntSource.Dispose();
        }
    }
}
