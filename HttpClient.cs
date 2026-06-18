/*
    HttpClient.cs — a tiny, readable quickstart client. It walks through one of every
    command so you can see the wire protocol in action without reading the parser.

    Like Test.cs it carries its own entry point and is kept out of the server build.
    Run the server first, then:

        dotnet run HttpClient.cs
*/

using System.Buffers.Binary;
using System.Net.Sockets;
using System.Text;

const string LOCALHOST = "127.0.0.1";
const int PORT = 5050;

using var client = new TcpClient(LOCALHOST, PORT);
using var stream = client.GetStream();

// (command sent, reply received) for a small tour of the API.
await Run("set\nuser:1\nalice\n");      // -> alice
await Run("get\nuser:1\n");             // -> alice
await Run("exists\nuser:1\n");          // -> 1

await Run("increment\nvisits\n1\n");    // -> 1
await Run("increment\nvisits\n10\n");   // -> 11
await Run("decrement\nvisits\n4\n");    // -> 7

await Run("expire\nuser:1\n60\n");      // -> 1
await Run("ttl\nuser:1\n");             // -> ~60

await Run("delete\nuser:1\n");          // -> OK
await Run("get\nuser:1\n");             // -> NULL

async Task Run(string payload)
{
    byte[] payloadBytes = Encoding.UTF8.GetBytes(payload);

    byte[] lenBytes = new byte[4];
    BinaryPrimitives.WriteInt32BigEndian(lenBytes, payloadBytes.Length);

    await stream.WriteAsync(lenBytes);
    await stream.WriteAsync(payloadBytes);

    byte[] buffer = new byte[2 << 12];
    int bytesRead = await stream.ReadAsync(buffer);

    string request = payload.Replace("\n", " ").TrimEnd();
    string response = Encoding.UTF8.GetString(buffer, 0, bytesRead).TrimEnd();
    Console.WriteLine($"{request,-28} -> {response}");
}
