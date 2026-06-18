/*
    Test.cs — a single-file client script that exercises Credis and measures how fast
    it is. This is the "where was it used?" part of the project: a standalone program
    that speaks the wire protocol, proves every command behaves, and then hammers the
    server to report throughput and per-operation latency.

    It is a file-based app (it carries its own entry point) so it stays out of the
    server build. Run it on its own:

        Terminal 1:  dotnet run
        Terminal 2:  dotnet run Test.cs                  (defaults: 127.0.0.1 5050 50000)
                     dotnet run Test.cs 127.0.0.1 5050 100000
*/

using System.Buffers.Binary;
using System.Diagnostics;
using System.Net.Sockets;
using System.Text;

const string DEFAULT_HOST = "127.0.0.1";
const int DEFAULT_PORT = 5050;
const int DEFAULT_OPS = 50000;

Console.OutputEncoding = Encoding.UTF8;

string host = args.Length > 0 ? args[0] : DEFAULT_HOST;
int port = args.Length > 1 && Int32.TryParse(args[1], out int parsedPort) ? parsedPort : DEFAULT_PORT;
int ops = args.Length > 2 && Int32.TryParse(args[2], out int parsedOps) ? parsedOps : DEFAULT_OPS;

TcpClient client;
try
{
    client = new TcpClient(host, port);
}
catch (SocketException ex)
{
    Console.WriteLine($"Could not reach Credis at {host}:{port} :: {ex.Message}");
    Console.WriteLine("Start the server first with `dotnet run`, then run this script again.");
    return 1;
}

using (client)
using (NetworkStream stream = client.GetStream())
{
    byte[] readBuffer = new byte[2 << 12];

    Console.WriteLine("=== Credis Benchmark ===");
    Console.WriteLine($"Target {host}:{port}  |  {ops:N0} operations per stage");
    Console.WriteLine();

    bool ok = await SmokeTestAsync(stream, readBuffer);
    if (!ok)
    {
        Console.WriteLine();
        Console.WriteLine("Smoke test failed, skipping the benchmark.");
        return 1;
    }

    await BenchmarkAsync(stream, readBuffer, ops);
}

return 0;

/* Correctness pass: run every command once and confirm it answers as advertised. */
async Task<bool> SmokeTestAsync(NetworkStream stream, byte[] buffer)
{
    Console.WriteLine("-- Smoke test (correctness) --");

    int passed = 0;
    int total = 0;

    // The server keeps state between runs, so start from a known-clean slate.
    foreach (string key in new[] { "name", "hits", "session", "flash" })
    {
        await Send(stream, buffer, $"delete\n{key}\n");
    }

    void Check(string label, string actual, string expected)
    {
        total++;
        bool pass = actual == expected;
        if (pass)
        {
            passed++;
        }
        Console.WriteLine($"  [{(pass ? "PASS" : "FAIL")}] {label,-28} -> \"{actual}\"" + (pass ? "" : $"  (expected \"{expected}\")"));
    }

    Check("set name alice", await Send(stream, buffer, "set\nname\nalice\n"), "alice");
    Check("get name", await Send(stream, buffer, "get\nname\n"), "alice");
    Check("exists name", await Send(stream, buffer, "exists\nname\n"), "1");

    Check("increment hits by 5", await Send(stream, buffer, "increment\nhits\n5\n"), "5");
    Check("increment hits by 3", await Send(stream, buffer, "increment\nhits\n3\n"), "8");
    Check("decrement hits by 2", await Send(stream, buffer, "decrement\nhits\n2\n"), "6");

    Check("delete name", await Send(stream, buffer, "delete\nname\n"), "OK");
    Check("get name after delete", await Send(stream, buffer, "get\nname\n"), "NULL");
    Check("exists name after delete", await Send(stream, buffer, "exists\nname\n"), "0");
    Check("delete missing key", await Send(stream, buffer, "delete\nname\n"), "NULL");

    await Send(stream, buffer, "set\nsession\nxyz\n");
    Check("ttl without expiry", await Send(stream, buffer, "ttl\nsession\n"), "-1");
    Check("expire session 100s", await Send(stream, buffer, "expire\nsession\n100\n"), "1");
    Check("ttl missing key", await Send(stream, buffer, "ttl\nnope\n"), "-2");
    Check("expire missing key", await Send(stream, buffer, "expire\nnope\n100\n"), "0");

    string remaining = await Send(stream, buffer, "ttl\nsession\n");
    bool ttlSane = Int64.TryParse(remaining, out long ttlValue) && ttlValue > 0 && ttlValue <= 100;
    Check("ttl counts down", ttlSane ? "in-range" : remaining, "in-range");

    // A short, real expiry so we can watch a key actually disappear.
    await Send(stream, buffer, "set\nflash\nboom\n");
    await Send(stream, buffer, "expire\nflash\n1\n");
    Console.WriteLine("  ...waiting 1.2s for 'flash' to expire...");
    await Task.Delay(1200);
    Check("get expired key", await Send(stream, buffer, "get\nflash\n"), "NULL");

    Console.WriteLine($"  {passed}/{total} checks passed.");
    return passed == total;
}

/* Throughput pass: time each command over a single, long-lived connection. */
async Task BenchmarkAsync(NetworkStream stream, byte[] buffer, int count)
{
    Console.WriteLine();
    Console.WriteLine("-- Throughput (single connection, synchronous round-trips) --");

    // Warm the JIT and the TCP path so the first timed stage is not penalised.
    for (int i = 0; i < 1000; i++)
    {
        await Send(stream, buffer, $"set\nwarmup:{i}\n{i}\n");
    }

    async Task RunStage(string name, Func<int, string> payload)
    {
        Stopwatch sw = Stopwatch.StartNew();
        for (int i = 0; i < count; i++)
        {
            await Send(stream, buffer, payload(i));
        }
        sw.Stop();

        double seconds = sw.Elapsed.TotalSeconds;
        double opsPerSec = count / seconds;
        double avgMicros = sw.Elapsed.TotalMicroseconds / count;
        Console.WriteLine($"  {name,-10} {count,9:N0} ops  in {seconds,7:F3}s   {opsPerSec,12:N0} ops/sec   {avgMicros,8:F2} us/op");
    }

    await RunStage("SET", i => $"set\nkey:{i}\nvalue:{i}\n");
    await RunStage("GET", i => $"get\nkey:{i}\n");
    await RunStage("INCREMENT", _ => "increment\ncounter\n1\n");
    await RunStage("EXISTS", i => $"exists\nkey:{i}\n");
    await RunStage("DELETE", i => $"delete\nkey:{i}\n");

    Console.WriteLine();
    Console.WriteLine("Done.");
}

/* Send one request and return the server's single-line reply (without the trailing '\n'). */
async Task<string> Send(NetworkStream stream, byte[] buffer, string payload)
{
    byte[] payloadBytes = Encoding.UTF8.GetBytes(payload);

    byte[] lenBytes = new byte[4];
    BinaryPrimitives.WriteInt32BigEndian(lenBytes, payloadBytes.Length);

    await stream.WriteAsync(lenBytes);
    await stream.WriteAsync(payloadBytes);

    int total = 0;
    while (true)
    {
        int read = await stream.ReadAsync(buffer.AsMemory(total, buffer.Length - total));
        if (read == 0)
        {
            break;
        }
        total += read;
        if (Array.IndexOf(buffer, (byte)'\n', 0, total) >= 0)
        {
            break;
        }
    }

    int newline = Array.IndexOf(buffer, (byte)'\n', 0, total);
    int end = newline >= 0 ? newline : total;
    return Encoding.UTF8.GetString(buffer, 0, end);
}
