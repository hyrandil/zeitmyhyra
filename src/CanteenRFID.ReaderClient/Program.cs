using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CanteenRFID.ReaderClient;

public class Program
{
    public static async Task Main(string[] args)
    {
        using var host = Host.CreateDefaultBuilder(args)
            .UseWindowsService()
            .ConfigureServices(services =>
            {
                services.AddHostedService<ReaderClientService>();
            })
            .Build();

        await host.RunAsync();
    }
}

public sealed class ReaderClientService : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
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

        async Task FlushQueueAsync()
        {
            if (!await flushLock.WaitAsync(0, stoppingToken))
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
        using var pingTimer = new PeriodicTimer(TimeSpan.FromSeconds(pingInterval));
        using var flushTimer = new PeriodicTimer(TimeSpan.FromSeconds(10));

        await FlushQueueAsync();

        Console.WriteLine("Bereit. UID einscannen und mit ENTER bestätigen (Keyboard-Wedge-Modus).");
        var canUseGlobalHook = settings.UseGlobalKeyboardHook && OperatingSystem.IsWindows();
        if (settings.UseGlobalKeyboardHook && !OperatingSystem.IsWindows())
        {
            Console.WriteLine("Globaler Tastatur-Hook ist nur unter Windows verfügbar. Fallback auf STDIN.");
        }
        IUidSource uidSource = canUseGlobalHook
            ? new GlobalKeyboardSource(settings.Terminator)
            : new KeyboardWedgeSource(settings.Terminator);

        var readTask = Task.Run(async () =>
        {
            await foreach (var uid in uidSource.ReadAsync().WithCancellation(stoppingToken))
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
        }, stoppingToken);

        var pingTask = Task.Run(async () =>
        {
            while (await pingTimer.WaitForNextTickAsync(stoppingToken))
            {
                var pinged = await sender.TryPingAsync(settings.ReaderId);
                if (pinged)
                {
                    await FlushQueueAsync();
                }
            }
        }, stoppingToken);

        var flushTask = Task.Run(async () =>
        {
            while (await flushTimer.WaitForNextTickAsync(stoppingToken))
            {
                if (File.Exists(queueFile) && new FileInfo(queueFile).Length > 0)
                {
                    await FlushQueueAsync();
                }
            }
        }, stoppingToken);

        await Task.WhenAll(readTask, pingTask, flushTask);
    }
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

public sealed class GlobalKeyboardSource : IUidSource, IDisposable
{
    private const int WhKeyboardLl = 13;
    private const int WmKeydown = 0x0100;
    private const int WmSysKeydown = 0x0104;

    private readonly string _terminator;
    private readonly StringBuilder _buffer = new();
    private readonly BlockingCollection<string> _queue = new();
    private readonly LowLevelKeyboardProc _proc;
    private readonly Thread _hookThread;
    private IntPtr _hookId = IntPtr.Zero;

    public GlobalKeyboardSource(string terminator)
    {
        _terminator = string.IsNullOrEmpty(terminator) ? "\r" : terminator;
        _proc = HookCallback;
        _hookThread = new Thread(RunHookLoop)
        {
            IsBackground = true
        };
        _hookThread.Start();
    }

    public async IAsyncEnumerable<string> ReadAsync()
    {
        while (true)
        {
            string item;
            try
            {
                item = _queue.Take();
            }
            catch (InvalidOperationException)
            {
                yield break;
            }
            yield return item;
            await Task.Yield();
        }
    }

    private void RunHookLoop()
    {
        _hookId = SetWindowsHookEx(WhKeyboardLl, _proc, IntPtr.Zero, 0);
        while (GetMessage(out var msg, IntPtr.Zero, 0, 0))
        {
            TranslateMessage(ref msg);
            DispatchMessage(ref msg);
        }
        if (_hookId != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hookId);
        }
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && (wParam == (IntPtr)WmKeydown || wParam == (IntPtr)WmSysKeydown))
        {
            var vkCode = Marshal.ReadInt32(lParam);
            var ch = VkToChar(vkCode);
            if (ch != null)
            {
                _buffer.Append(ch);
                if (_buffer.ToString().EndsWith(_terminator, StringComparison.Ordinal))
                {
                    var value = _buffer.ToString();
                    var trimmed = value[..^_terminator.Length];
                    _buffer.Clear();
                    if (!string.IsNullOrWhiteSpace(trimmed))
                    {
                        _queue.Add(trimmed);
                    }
                }
            }
        }
        return CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    private static string? VkToChar(int vkCode)
    {
        var keyboardState = new byte[256];
        if (!GetKeyboardState(keyboardState))
        {
            return null;
        }

        var scanCode = MapVirtualKey((uint)vkCode, 0);
        var buffer = new StringBuilder(2);
        var result = ToUnicode((uint)vkCode, scanCode, keyboardState, buffer, buffer.Capacity, 0);
        if (result > 0)
        {
            return buffer.ToString();
        }
        return null;
    }

    public void Dispose()
    {
        _queue.CompleteAdding();
        PostThreadMessage(GetCurrentThreadId(), 0x0012, IntPtr.Zero, IntPtr.Zero);
    }

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll")]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool GetMessage(out Msg lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [DllImport("user32.dll")]
    private static extern bool TranslateMessage(ref Msg lpMsg);

    [DllImport("user32.dll")]
    private static extern IntPtr DispatchMessage(ref Msg lpMsg);

    [DllImport("user32.dll")]
    private static extern bool GetKeyboardState(byte[] lpKeyState);

    [DllImport("user32.dll")]
    private static extern uint MapVirtualKey(uint uCode, uint uMapType);

    [DllImport("user32.dll")]
    private static extern int ToUnicode(uint wVirtKey, uint wScanCode, byte[] lpKeyState, StringBuilder pwszBuff, int cchBuff, uint wFlags);

    [DllImport("user32.dll")]
    private static extern bool PostThreadMessage(uint idThread, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    private struct Msg
    {
        public IntPtr hwnd;
        public uint message;
        public IntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public Point pt;
    }

    private struct Point
    {
        public int x;
        public int y;
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
    public bool UseGlobalKeyboardHook { get; init; } = true;
}

public record StampRequest
{
    public string Uid { get; init; } = string.Empty;
    public string ReaderId { get; init; } = string.Empty;
    public DateTime? TimestampUtc { get; init; }
    public Dictionary<string, string>? Meta { get; init; }
}

public record ReaderPingRequest(string ReaderId);
