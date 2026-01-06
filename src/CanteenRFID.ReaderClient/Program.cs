using System.Net.Http.Json;
using System.Text.Json;
using CanteenRFID.Core.Enums;

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

Console.WriteLine("CanteenRFID Reader Client gestartet. Modus Keyboard Wedge.");
Console.WriteLine("Scanne UID (ENTER zum bestätigen).");

var httpClient = new HttpClient
{
    BaseAddress = new Uri(settings.ServerUrl)
};
httpClient.DefaultRequestHeaders.Add("X-API-KEY", settings.ApiKey);

await FlushQueueAsync();

string? line;
while ((line = Console.ReadLine()) != null)
{
    line = line.Trim();
    if (string.IsNullOrEmpty(line)) continue;
    var stamp = new StampRequest
    {
        Uid = line,
        ReaderId = settings.ReaderId,
        TimestampUtc = DateTime.UtcNow
    };
    if (!await TrySendAsync(stamp))
    {
        await File.AppendAllTextAsync(queueFile, JsonSerializer.Serialize(stamp) + "\n");
        Console.WriteLine("Server offline, in Queue abgelegt.");
    }
}

async Task<bool> TrySendAsync(StampRequest request)
{
    try
    {
        var response = await httpClient.PostAsJsonAsync("/api/v1/stamps", request);
        if (response.IsSuccessStatusCode)
        {
            Console.WriteLine($"Gesendet: {request.Uid} -> {DateTime.Now}");
            return true;
        }
    }
    catch
    {
        // ignored
    }
    return false;
}

async Task FlushQueueAsync()
{
    if (!File.Exists(queueFile)) return;
    var lines = await File.ReadAllLinesAsync(queueFile);
    var remaining = new List<string>();
    foreach (var l in lines)
    {
        try
        {
            var stamp = JsonSerializer.Deserialize<StampRequest>(l);
            if (stamp != null && await TrySendAsync(stamp))
            {
                continue;
            }
        }
        catch
        {
        }
        remaining.Add(l);
    }
    await File.WriteAllLinesAsync(queueFile, remaining);
}

public record ReaderClientSettings
{
    public string ServerUrl { get; init; } = "http://localhost:5000";
    public string ApiKey { get; init; } = "CHANGE_ME";
    public string ReaderId { get; init; } = "READER-01";
}

public record StampRequest
{
    public string Uid { get; init; } = string.Empty;
    public string ReaderId { get; init; } = string.Empty;
    public DateTime? TimestampUtc { get; init; }
}
