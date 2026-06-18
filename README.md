# Credis

A tiny Redis-style in-memory database written from scratch in C# (.NET 10). It speaks
a small binary line protocol over raw TCP, stores everything in memory, supports
per-key time-to-live (TTL), and serves many connections concurrently.

This is a learning project: the goal was to hand-roll the networking, the buffer
management, and the request parser without leaning on a framework, and to keep the code
small enough to read in one sitting.

## Features

- **Raw TCP server** built on `TcpListener`, one task per connection.
- **Hand-written parser** that reads a length-prefixed binary frame out of a fixed-size
  buffer — no `StreamReader`, no allocations per line beyond the value itself.
- **Eight commands**: `set`, `get`, `delete`, `exists`, `increment`, `decrement`,
  `expire`, `ttl`.
- **Per-key TTL** with lazy expiration (a key is reclaimed the moment it is read after
  expiring).
- **Thread-safe store** backed by `ConcurrentDictionary`.
- **A single-file benchmark** (`Test.cs`) that proves correctness and measures
  throughput.

## Project layout

| File | Role |
| --- | --- |
| `Program.cs` | Entry point; boots the server. |
| `Server.cs` | TCP listener and connection accept loop. |
| `Parser.cs` | Reads the wire protocol and dispatches commands. |
| `DataStructures.cs` | The in-memory store, counters, and TTL logic. |
| `HttpClient.cs` | A small quickstart client — one of every command. |
| `Test.cs` | Correctness smoke test + performance benchmark. |

`HttpClient.cs` and `Test.cs` are standalone single-file scripts (each has its own entry
point). They are excluded from the server build and run with `dotnet run --file`.

## Wire protocol

Every request is a 4-byte big-endian `Int32` length prefix, followed by that many bytes
of payload. The payload is a sequence of `\n`-terminated lines: the command name, then
its arguments.

```
[4 bytes] payload length (the bytes after this prefix)
cmd\n
arg1\n
arg2\n        (only for commands that take a second argument)
```

The server replies with exactly one `\n`-terminated line carrying the result.

## Commands

| Command | Arguments | Reply | Notes |
| --- | --- | --- | --- |
| `set` | key, value | the value | Overwrites and clears any existing TTL. |
| `get` | key | value, or `NULL` | |
| `delete` | key | `OK`, or `NULL` | `NULL` if the key was absent/expired. |
| `exists` | key | `1` / `0` | |
| `increment` | key, amount | new value | Missing key starts at 0. Keeps existing TTL. |
| `decrement` | key, amount | new value | Missing key starts at 0. Keeps existing TTL. |
| `expire` | key, seconds | `1` / `0` | `0` if the key does not exist. |
| `ttl` | key | seconds, `-1`, or `-2` | `-1` = no expiry set, `-2` = key missing. |

## Running it

You need the .NET 10 SDK.

**1. Start the server** (listens on `127.0.0.1:5050`):

```
dotnet run
```

**2. Run the quickstart demo** in another terminal — issues one of every command and
prints the replies:

```
dotnet run --file HttpClient.cs
```

**3. Run the benchmark** — a correctness pass followed by a throughput pass. Optional
arguments are `host port operations-per-stage` (defaults `127.0.0.1 5050 50000`):

```
dotnet run --file Test.cs
dotnet run --file Test.cs 127.0.0.1 5050 100000
```

> The `--file` flag forces .NET to run the single `.cs` file as a script even though the
> directory also contains the server project.

### Sample benchmark output

Single connection, synchronous request/response over loopback (numbers vary by machine):

```
-- Throughput (single connection, synchronous round-trips) --
  SET           50,000 ops  in   2.157s         23,179 ops/sec      43.14 us/op
  GET           50,000 ops  in   2.054s         24,348 ops/sec      41.07 us/op
  INCREMENT     50,000 ops  in   2.076s         24,080 ops/sec      41.53 us/op
  EXISTS        50,000 ops  in   1.986s         25,179 ops/sec      39.72 us/op
  DELETE        50,000 ops  in   1.839s         27,187 ops/sec      36.78 us/op
```

Throughput here is dominated by the TCP round-trip per command, not by the store itself
— the benchmark waits for each reply before sending the next request.

## Docker

```
docker build -t credis .
docker run -p 5050:5050 credis
```

## Design notes

- **One store, one slot per key.** Each key maps to an `Entry` that bundles the value
  and its expiry timestamp, so the value and its TTL always move together as a single
  atomic unit in the `ConcurrentDictionary`.
- **Lazy expiration.** There is no background sweeper. A key past its expiry is treated
  as gone on the next read and removed then, which keeps the hot path free of timers.
- **Fixed 8 KB buffer.** An in-memory cache is meant to be fast, not to hold large blobs,
  so the per-connection buffer is capped (`MAX_BUFFER_SIZE`). Once a connection drains its
  buffer the read/write pointers rewind to the front, so a long-lived connection reuses
  the same buffer indefinitely.

## Limitations / possible next steps

- No persistence — everything is lost on restart (it is an *in-memory* database).
- TTL is second-granularity and expiration is lazy only.
- The protocol assumes one in-flight request per connection (no pipelining).
- No authentication; intended for localhost / trusted networks.
