using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;

Console.WriteLine("CanteenRFID Reader Client gestartet (Keyboard Wedge)");
var configPath = Path.Combine(AppContext.BaseDirectory, "readerclientsettings.json");
if (!File.Exists(configPath))
{
    var sample = new ReaderClientSettings();
    File.WriteAllText(configPath, JsonSerializer.Serialize(sample, new JsonSerializerOptions { WriteIndented = true }));
    Console.WriteLine($"Config erzeugt unter {configPath}. Bitte anpassen.");
}

var settings = JsonSerializer.Deserialize<ReaderClientSettings>(File.ReadAllText(configPath)) ?? new ReaderClientSettings();
Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, "queue"));
var queueFile = Path.Combine(AppContext.BaseDirectory, "queue", "queue.jsonl");

var httpClient = new HttpClient { BaseAddress = new Uri(settings.ServerUrl) };
httpClient.DefaultRequestHeaders.Add("X-API-KEY", settings.ApiKey);

var queue = new StampQueue(queueFile);
var sender = new StampSender(httpClient);
var flushLock = new SemaphoreSlim(1, 1);
var flushCts = new CancellationTokenSource();

async Task FlushQueueAsync()
{
    if (!await flushLock.WaitAsync(0))
    {
        return;
    }

    try
    {
        await queue.FlushAsync(async stamp => await sender.TrySendAsync(stamp));
    }
    finally
    {
        flushLock.Release();
    }
}

var pingInterval = Math.Max(5, settings.PingIntervalSeconds);
var pingCts = new CancellationTokenSource();
var pingTask = Task.Run(async () =>
{
    var timer = new PeriodicTimer(TimeSpan.FromSeconds(pingInterval));
    while (await timer.WaitForNextTickAsync(pingCts.Token))
    {
        var pinged = await sender.TryPingAsync(settings.ReaderId);
        if (pinged)
        {
            await FlushQueueAsync();
        }
    }
}, pingCts.Token);

var flushTask = Task.Run(async () =>
{
    var timer = new PeriodicTimer(TimeSpan.FromSeconds(10));
    while (await timer.WaitForNextTickAsync(flushCts.Token))
    {
        if (File.Exists(queueFile) && new FileInfo(queueFile).Length > 0)
        {
            await FlushQueueAsync();
        }
    }
}, flushCts.Token);

    try
    {
        await queue.FlushAsync(async stamp => await sender.TrySendAsync(stamp));
    }
    finally
    {
        flushLock.Release();
    }
}

Console.WriteLine("Bereit. UID einscannen und mit ENTER bestätigen (Keyboard-Wedge-Modus).");
IUidSource uidSource = new KeyboardWedgeSource(settings.Terminator);

await foreach (var uid in uidSource.ReadAsync())
{
    if (string.IsNullOrWhiteSpace(uid)) continue;
    var stamp = new StampRequest
    {
        Uid = uid.Trim(),
        ReaderId = settings.ReaderId,
        TimestampUtc = DateTime.UtcNow,
        Meta = new Dictionary<string, string> { { "source", "keyboardWedge" } }
    };

    var sent = await sender.TrySendAsync(stamp);
    if (!sent)
    {
        await queue.EnqueueAsync(stamp);
        Console.WriteLine("Server offline, in Queue abgelegt.");
    }
    else
    {
        await FlushQueueAsync();
    }
}

pingCts.Cancel();
flushCts.Cancel();
try
{
    await pingTask;
}
catch (TaskCanceledException)
{
}

try
{
    await flushTask;
}
catch (TaskCanceledException)
{
}

public interface IUidSource
{
    IAsyncEnumerable<string> ReadAsync();
}

public class KeyboardWedgeSource : IUidSource
{
    private readonly string _terminator;

    public KeyboardWedgeSource(string terminator)
    {
        _terminator = terminator;
    }

    public async IAsyncEnumerable<string> ReadAsync()
    {
        while (true)
        {
            var line = await Task.Run(Console.ReadLine);
            if (line == null)
            {
                yield break;
            }

            if (line.EndsWith(_terminator))
            {
                yield return line[..^_terminator.Length];
            }
            else
            {
                yield return line;
            }
        }
    }
}

public class StampSender
{
    private readonly HttpClient _client;

    public StampSender(HttpClient client)
    {
        _client = client;
    }

    public async Task<bool> TrySendAsync(StampRequest request)
    {
        try
        {
            var response = await _client.PostAsJsonAsync("/api/v1/stamps", request);
            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine($"Gesendet: {request.Uid} [{request.ReaderId}] {DateTime.Now}");
                return true;
            }
            Console.WriteLine($"Fehler beim Senden: {response.StatusCode}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Sendeproblem: {ex.Message}");
        }
        return false;
    }

    public async Task<bool> TryPingAsync(string readerId)
    {
        try
        {
            var response = await _client.PostAsJsonAsync("/api/v1/readers/ping", new ReaderPingRequest(readerId));
            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine($"Ping gesendet [{readerId}] {DateTime.Now}");
                return true;
            }
            Console.WriteLine($"Ping fehlgeschlagen: {response.StatusCode}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ping-Problem: {ex.Message}");
        }
        return false;
    }
}

public class StampQueue
{
    private readonly string _filePath;
    private readonly JsonSerializerOptions _options = new() { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

    public StampQueue(string filePath)
    {
        _filePath = filePath;
    }

    public async Task EnqueueAsync(StampRequest request)
    {
        await File.AppendAllTextAsync(_filePath, JsonSerializer.Serialize(request, _options) + "\n");
    }

    public async Task FlushAsync(Func<StampRequest, Task<bool>> sender)
    {
        if (!File.Exists(_filePath)) return;
        var lines = await File.ReadAllLinesAsync(_filePath);
        var remaining = new List<string>();
        foreach (var line in lines)
        {
            try
            {
                var stamp = JsonSerializer.Deserialize<StampRequest>(line, _options);
                if (stamp != null && await sender(stamp))
                {
                    continue;
                }
            }
            catch
            {
                // ignore malformed line
            }
            remaining.Add(line);
        }
        await File.WriteAllLinesAsync(_filePath, remaining);
    }
}

public record ReaderClientSettings
{
    public string ServerUrl { get; init; } = "http://localhost:5000";
    public string ApiKey { get; init; } = "CHANGE_ME";
    public string ReaderId { get; init; } = "READER-01";
    public string Terminator { get; init; } = "";
    public int PingIntervalSeconds { get; init; } = 60;
}

public record StampRequest
{
    public string Uid { get; init; } = string.Empty;
    public string ReaderId { get; init; } = string.Empty;
    public DateTime? TimestampUtc { get; init; }
    public Dictionary<string, string>? Meta { get; init; }
}

public record ReaderPingRequest(string ReaderId);
