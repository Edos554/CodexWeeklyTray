using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace CodexWeeklyTray;

internal sealed class CodexAppServerClient
{
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan CliCandidateTimeout = TimeSpan.FromSeconds(3);

    public async Task<RateLimitSnapshot> ReadWeeklyRateLimitAsync(CancellationToken cancellationToken)
    {
        using var process = CreateProcess();

        try
        {
            if (!process.Start())
                throw new InvalidOperationException("Codex CLIを起動できませんでした。");
        }
        catch (Exception ex)
        {
            AppLog.Error("app-server-start", "Failed to start codex CLI.", ex);
            throw new InvalidOperationException(
                "Codex CLIが見つかりません。コマンドプロンプトで 'codex --version' を確認してください。", ex);
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(RequestTimeout);
        var token = timeoutCts.Token;
        StderrDrain? stderrDrain = null;

        try
        {
            stderrDrain = StderrDrain.Start(process.StandardError, token);
            AppLog.Info("app-server", "Starting codex app-server request.");
            await SendAsync(process, new
            {
                jsonrpc = "2.0",
                method = "initialize",
                id = 1,
                @params = new
                {
                    clientInfo = new
                    {
                        name = "codex_weekly_tray",
                        title = "Codex Weekly Tray",
                        version = "0.1.0"
                    }
                }
            }, token);

            await WaitForResponseAsync(process, 1, token, stderrDrain);

            await SendAsync(process, new
            {
                jsonrpc = "2.0",
                method = "initialized",
                @params = new { }
            }, token);

            await SendAsync(process, new
            {
                jsonrpc = "2.0",
                method = "account/rateLimits/read",
                id = 2
            }, token);

            using JsonDocument response = await WaitForResponseAsync(process, 2, token, stderrDrain);
            return ParseWeeklySnapshot(response.RootElement);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            AppLog.Error("app-server", "Request timed out.");
            throw new TimeoutException("Codexの利用状況取得がタイムアウトしました。");
        }
        catch (Exception ex)
        {
            AppLog.Error("app-server", "Request failed.", ex);
            throw;
        }
        finally
        {
            TryStop(process);
            stderrDrain?.WaitBriefly();
        }
    }

    private static Process CreateProcess()
    {
        string codexCliPath = ResolveCodexCliPath();
        AppLog.Info("app-server-start", $"Using Codex CLI: {codexCliPath}");

        return new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = codexCliPath,
                Arguments = "app-server --listen stdio://",
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            },
            EnableRaisingEvents = true
        };
    }

    internal static string ResolveCodexCliPath()
    {
        if (TryResolveVerifiedCodexCliPath(out string codexCliPath))
        {
            return codexCliPath;
        }

        AppLog.Info("cli-fallback", "No valid LocalAppData candidate; using PATH fallback.");
        return "codex";
    }

    internal static bool TryResolveVerifiedCodexCliPath(out string codexCliPath)
    {
        codexCliPath = string.Empty;
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrWhiteSpace(localAppData))
        {
            string codexBinRoot = Path.Combine(localAppData, "OpenAI", "Codex", "bin");
            if (Directory.Exists(codexBinRoot))
            {
                try
                {
                    IEnumerable<string> candidates = Directory
                        .EnumerateFiles(codexBinRoot, "codex.exe", SearchOption.AllDirectories)
                        .Select(path => new FileInfo(path))
                        .Where(file => file.Exists)
                        .Where(file => !IsWindowsAppsPath(file.FullName))
                        .OrderByDescending(file => file.LastWriteTimeUtc)
                        .Select(file => file.FullName);

                    foreach (string candidate in candidates)
                    {
                        AppLog.Info("cli-candidate", $"Testing candidate: {candidate}");
                        if (IsValidCodexCliCandidate(candidate))
                        {
                        AppLog.Info("cli-candidate", $"Accepted candidate: {candidate}");
                            codexCliPath = candidate;
                            return true;
                        }
                    }
                }
                catch (Exception ex)
                {
                    AppLog.Error("codex-cli-resolve", "Failed to search LocalAppData Codex CLI candidates.", ex);
                }
            }
        }

        return false;
    }

    private static bool IsValidCodexCliCandidate(string candidate)
    {
        if (!File.Exists(candidate) || IsWindowsAppsPath(candidate))
        {
            return false;
        }

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = candidate,
                Arguments = "--version",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };

        try
        {
            if (!process.Start())
            {
                AppLog.Info("cli-candidate", "Candidate failed: Start returned false");
                return false;
            }

            Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync();
            Task<string> stderrTask = process.StandardError.ReadToEndAsync();

            if (!process.WaitForExit((int)CliCandidateTimeout.TotalMilliseconds))
            {
                AppLog.Info("cli-candidate", "Candidate timed out.");
                TryKill(process);
                _ = Task.WhenAll(stdoutTask, stderrTask).Wait(millisecondsTimeout: 250);
                return false;
            }

            _ = Task.WhenAll(stdoutTask, stderrTask).Wait(millisecondsTimeout: 250);
            if (process.ExitCode == 0)
            {
                return true;
            }

            AppLog.Info("cli-candidate", $"Candidate exited with code {process.ExitCode}.");
            return false;
        }
        catch (Exception ex)
        {
            AppLog.Info("cli-candidate", $"Candidate failed: {ex.GetType().Name}");
            return false;
        }
    }

    private static bool IsWindowsAppsPath(string path)
    {
        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(path);
        }
        catch
        {
            fullPath = path;
        }

        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return IsUnderDirectory(fullPath, @"C:\Program Files\WindowsApps\") ||
            (!string.IsNullOrWhiteSpace(localAppData) &&
             IsUnderDirectory(fullPath, Path.Combine(localAppData, "Microsoft", "WindowsApps") + Path.DirectorySeparatorChar));
    }

    private static bool IsUnderDirectory(string path, string directory)
    {
        return path.StartsWith(directory, StringComparison.OrdinalIgnoreCase);
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(milliseconds: 500);
            }
        }
        catch
        {
            // Candidate validation failures are handled by trying the next candidate.
        }
    }

    private static async Task SendAsync(Process process, object payload, CancellationToken token)
    {
        string json = JsonSerializer.Serialize(payload);
        await process.StandardInput.WriteLineAsync(json.AsMemory(), token);
        await process.StandardInput.FlushAsync(token);
    }

    private static async Task<JsonDocument> WaitForResponseAsync(
        Process process,
        int expectedId,
        CancellationToken token,
        StderrDrain? stderrDrain)
    {
        while (true)
        {
            string? line = await process.StandardOutput.ReadLineAsync(token);
            if (line is null)
            {
                string error = stderrDrain?.GetSnippet() ?? string.Empty;
                throw new InvalidOperationException(
                    string.IsNullOrWhiteSpace(error)
                        ? "Codex app-serverが応答せず終了しました。"
                        : $"Codex app-serverエラー: {error.Trim()}");
            }

            JsonDocument document;
            try
            {
                document = JsonDocument.Parse(line);
            }
            catch (JsonException)
            {
                continue;
            }

            JsonElement root = document.RootElement;
            if (!root.TryGetProperty("id", out JsonElement idElement) ||
                idElement.ValueKind != JsonValueKind.Number ||
                idElement.GetInt32() != expectedId)
            {
                document.Dispose();
                continue;
            }

            if (root.TryGetProperty("error", out JsonElement errorElement))
            {
                string message = errorElement.TryGetProperty("message", out JsonElement messageElement)
                    ? messageElement.GetString() ?? errorElement.ToString()
                    : errorElement.ToString();
                document.Dispose();
                AppLog.Error("json-rpc", $"Codex returned an error for id {expectedId}: {message}");
                throw new InvalidOperationException($"Codexからエラーが返されました: {message}");
            }

            return document;
        }
    }

    private static RateLimitSnapshot ParseWeeklySnapshot(JsonElement response)
    {
        if (!response.TryGetProperty("result", out JsonElement result) ||
            !result.TryGetProperty("rateLimits", out JsonElement rateLimits))
        {
            throw new InvalidOperationException("週間利用枠が返されませんでした。プランまたはCodex側の仕様を確認してください。");
        }

        RateLimitWindow? primary = TryReadRateLimit(rateLimits, "primary");
        RateLimitWindow? secondary = TryReadRateLimit(rateLimits, "secondary");
        RateLimitWindow? weekly = SelectRateLimit(primary, secondary, IsWeeklyWindow);
        if (weekly is null)
        {
            throw new InvalidOperationException("週間利用枠が返されませんでした。プランまたはCodex側の仕様を確認してください。");
        }

        RateLimitWindow? fiveHour = SelectRateLimit(primary, secondary, IsFiveHourWindow);
        return new RateLimitSnapshot(weekly, fiveHour);
    }

    private static RateLimitWindow? TryReadRateLimit(JsonElement rateLimits, string name)
    {
        if (!rateLimits.TryGetProperty(name, out JsonElement limit) ||
            limit.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        if (!limit.TryGetProperty("windowDurationMins", out JsonElement durationElement) ||
            durationElement.ValueKind != JsonValueKind.Number ||
            !durationElement.TryGetInt32(out int windowDuration) ||
            windowDuration <= 0)
        {
            return null;
        }

        if (!limit.TryGetProperty("usedPercent", out JsonElement usedElement) ||
            !usedElement.TryGetDouble(out double usedPercent))
        {
            throw new InvalidOperationException("利用率の形式を解釈できませんでした。");
        }

        DateTimeOffset? resetsAt = null;
        if (limit.TryGetProperty("resetsAt", out JsonElement resetElement))
        {
            resetsAt = ParseResetTime(resetElement);
        }

        return new RateLimitWindow(Math.Clamp(usedPercent, 0d, 100d), resetsAt, windowDuration);
    }

    private static RateLimitWindow? SelectRateLimit(
        RateLimitWindow? primary,
        RateLimitWindow? secondary,
        Func<RateLimitWindow, bool> predicate)
    {
        return primary is not null && predicate(primary)
            ? primary
            : secondary is not null && predicate(secondary)
                ? secondary
                : null;
    }

    private static bool IsFiveHourWindow(RateLimitWindow limit) =>
        Math.Abs(limit.WindowDurationMinutes!.Value - 300) <= 60;

    private static bool IsWeeklyWindow(RateLimitWindow limit) =>
        Math.Abs(limit.WindowDurationMinutes!.Value - 10_080) <= 1_440;

    private static DateTimeOffset? ParseResetTime(JsonElement resetElement)
    {
        if (resetElement.ValueKind == JsonValueKind.Number &&
            resetElement.TryGetInt64(out long resetUnix))
        {
            return DateTimeOffset.FromUnixTimeSeconds(resetUnix);
        }

        if (resetElement.ValueKind == JsonValueKind.String)
        {
            string? resetText = resetElement.GetString();
            if (DateTimeOffset.TryParse(resetText, out DateTimeOffset parsed))
            {
                return parsed;
            }
        }

        return null;
    }

    private static void TryStop(Process process)
    {
        try
        {
            if (process.HasExited)
                return;

            try
            {
                process.StandardInput.Close();
            }
            catch (Exception ex)
            {
                AppLog.Error("app-server-stop", "Failed to close stdin.", ex);
            }

            if (process.WaitForExit(milliseconds: 1500))
                return;

            process.Kill(entireProcessTree: true);
        }
        catch (Exception ex)
        {
            AppLog.Error("app-server-stop", "Failed to stop codex app-server.", ex);
            // 終了処理では例外を表へ出さない。
        }
    }

    private sealed class StderrDrain
    {
        private const int MaxBufferedChars = 2048;
        private readonly object _sync = new();
        private readonly StringBuilder _buffer = new();

        private StderrDrain()
        {
            Completion = Task.CompletedTask;
        }

        public Task Completion { get; private set; }

        public static StderrDrain Start(StreamReader reader, CancellationToken token)
        {
            var drain = new StderrDrain();
            drain.Completion = drain.ReadAsync(reader, token);
            return drain;
        }

        public string GetSnippet()
        {
            lock (_sync)
            {
                return _buffer.ToString();
            }
        }

        public void WaitBriefly()
        {
            try
            {
                Completion.Wait(millisecondsTimeout: 250);
            }
            catch
            {
                // stderr diagnostics must not affect app-server shutdown.
            }
        }

        private async Task ReadAsync(StreamReader reader, CancellationToken token)
        {
            try
            {
                while (true)
                {
                    string? line = await reader.ReadLineAsync(token);
                    if (line is null)
                    {
                        return;
                    }

                    Append(line);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
            catch
            {
                // stderr drain failures must not fail the primary JSON-RPC request.
            }
        }

        private void Append(string line)
        {
            string compact = Sanitize(line);
            if (compact.Length == 0)
            {
                return;
            }

            lock (_sync)
            {
                if (_buffer.Length > 0)
                {
                    _buffer.Append(' ');
                }

                _buffer.Append(compact);
                if (_buffer.Length > MaxBufferedChars)
                {
                    _buffer.Remove(0, _buffer.Length - MaxBufferedChars);
                }
            }
        }

        private static string Sanitize(string value)
        {
            var builder = new StringBuilder(value.Length);
            foreach (char c in value)
            {
                builder.Append(char.IsControl(c) ? ' ' : c);
            }

            string compact = builder.ToString().Trim();
            return compact.Length <= 500 ? compact : compact[..500];
        }
    }
}
