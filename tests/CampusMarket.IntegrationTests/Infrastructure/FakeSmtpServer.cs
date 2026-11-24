using System.Net;
using System.Net.Sockets;
using System.Text;

namespace CampusMarket.IntegrationTests.Infrastructure;

// A minimal plain-text SMTP server on 127.0.0.1 and a free port: accepts one connection, answers the basic commands and records what it got.
public sealed class FakeSmtpServer : IAsyncDisposable
{
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource _stop = new();
    private readonly Task _dialog;

    public FakeSmtpServer()
    {
        _listener.Start();
        Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        _dialog = RunAsync(_stop.Token);
    }

    public int Port { get; }

    public string? Username { get; private set; }

    public string? Password { get; private set; }

    public string? MailFrom { get; private set; }

    public List<string> RcptTo { get; } = new();

    public string? Data { get; private set; }

    // Completes when the client has closed the dialog (QUIT or end of the connection).
    public Task Completion => _dialog;

    private async Task RunAsync(CancellationToken ct)
    {
        using var client = await _listener.AcceptTcpClientAsync(ct);
        await using var stream = client.GetStream();
        using var reader = new StreamReader(stream, Encoding.ASCII);
        await using var writer = new StreamWriter(stream, Encoding.ASCII) { NewLine = "\r\n", AutoFlush = true };

        await writer.WriteLineAsync("220 localhost fake SMTP ready");

        while (await reader.ReadLineAsync(ct) is { } line)
        {
            if (line.StartsWith("EHLO", StringComparison.OrdinalIgnoreCase) || line.StartsWith("HELO", StringComparison.OrdinalIgnoreCase))
            {
                await writer.WriteLineAsync("250-localhost");
                await writer.WriteLineAsync("250 AUTH LOGIN");
            }
            else if (line.StartsWith("AUTH LOGIN", StringComparison.OrdinalIgnoreCase))
            {
                await AuthenticateAsync(line, reader, writer, ct);
            }
            else if (line.StartsWith("MAIL FROM", StringComparison.OrdinalIgnoreCase))
            {
                MailFrom = line;
                await writer.WriteLineAsync("250 OK");
            }
            else if (line.StartsWith("RCPT TO", StringComparison.OrdinalIgnoreCase))
            {
                RcptTo.Add(line);
                await writer.WriteLineAsync("250 OK");
            }
            else if (string.Equals(line, "DATA", StringComparison.OrdinalIgnoreCase))
            {
                await writer.WriteLineAsync("354 End data with <CR><LF>.<CR><LF>");
                Data = await ReadDataAsync(reader, ct);
                await writer.WriteLineAsync("250 OK");
            }
            else if (string.Equals(line, "QUIT", StringComparison.OrdinalIgnoreCase))
            {
                await writer.WriteLineAsync("221 Bye");
                return;
            }
            else
            {
                await writer.WriteLineAsync("250 OK");
            }
        }
    }

    // AUTH LOGIN, with the username either on the same line or after a 334 prompt, then the password.
    private async Task AuthenticateAsync(string line, StreamReader reader, StreamWriter writer, CancellationToken ct)
    {
        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        string? encodedUsername;
        if (parts.Length > 2)
        {
            encodedUsername = parts[2];
        }
        else
        {
            await writer.WriteLineAsync("334 VXNlcm5hbWU6");
            encodedUsername = await reader.ReadLineAsync(ct);
        }

        await writer.WriteLineAsync("334 UGFzc3dvcmQ6");
        var encodedPassword = await reader.ReadLineAsync(ct);

        Username = Decode(encodedUsername);
        Password = Decode(encodedPassword);
        await writer.WriteLineAsync("235 2.7.0 Authentication successful");
    }

    private static async Task<string> ReadDataAsync(StreamReader reader, CancellationToken ct)
    {
        var data = new StringBuilder();
        while (await reader.ReadLineAsync(ct) is { } dataLine && dataLine != ".")
            data.AppendLine(dataLine);

        return data.ToString();
    }

    private static string Decode(string? base64) =>
        Encoding.UTF8.GetString(Convert.FromBase64String(base64 ?? ""));

    public async ValueTask DisposeAsync()
    {
        await _stop.CancelAsync();
        _listener.Stop();
        try
        {
            await _dialog;
        }
        catch (OperationCanceledException)
        {
            // The test ended before a client connected; nothing to clean up.
        }

        _stop.Dispose();
    }
}
