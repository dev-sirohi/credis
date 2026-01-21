namespace Credis;

internal sealed class Logger
{
    public static readonly Logger Instance = new();

    private Logger() { }

    public async Task WriteAsync(string message)
    {
        Console.WriteLine(message);
    }

    public async Task WriteAsync(Exception ex)
    {
        Console.WriteLine(ex.Message);
    }
}