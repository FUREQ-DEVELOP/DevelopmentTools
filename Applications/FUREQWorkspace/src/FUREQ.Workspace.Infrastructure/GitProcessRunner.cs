using System.Diagnostics;
using System.Text;
using FUREQ.Workspace.Application;

namespace FUREQ.Workspace.Infrastructure;

public sealed class GitProcessRunner : IGitProcessRunner
{
    private const int MaximumCapturedCharacters = 4 * 1024 * 1024;

    public async Task<GitCommandResult> RunAsync(string workingDirectory, IReadOnlyList<string> arguments,
        TimeSpan timeout, CancellationToken cancellationToken = default, bool disableHooks = false,
        bool disableFsMonitor = false, bool useGitCredentialManager = false)
    {
        if (!Directory.Exists(workingDirectory)) throw new DirectoryNotFoundException("Gitの作業フォルダーがありません。");
        if (arguments.Count == 0) throw new ArgumentException("Gitの引数が必要です。", nameof(arguments));
        if (timeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(timeout));

        var startInfo = new ProcessStartInfo("git")
        {
            WorkingDirectory = Path.GetFullPath(workingDirectory),
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        if (disableFsMonitor)
        {
            startInfo.ArgumentList.Add("-c");
            startInfo.ArgumentList.Add("core.fsmonitor=false");
        }
        if (disableHooks)
        {
            startInfo.ArgumentList.Add("-c");
            startInfo.ArgumentList.Add("core.hooksPath=/dev/null");
        }
        if (useGitCredentialManager)
        {
            startInfo.ArgumentList.Add("-c");
            startInfo.ArgumentList.Add("credential.helper=");
            startInfo.ArgumentList.Add("-c");
            startInfo.ArgumentList.Add("credential.helper=manager");
        }
        foreach (var argument in arguments) startInfo.ArgumentList.Add(argument);
        startInfo.Environment["GIT_TERMINAL_PROMPT"] = "0";
        startInfo.Environment["GIT_OPTIONAL_LOCKS"] = "0";

        using var process = new Process { StartInfo = startInfo };
        if (!process.Start()) throw new InvalidOperationException("Gitを起動できませんでした。");
        using var timeoutCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCancellation.CancelAfter(timeout);
        var standardOutputTask = ReadBoundedAsync(process.StandardOutput, timeoutCancellation.Token);
        var standardErrorTask = ReadBoundedAsync(process.StandardError, timeoutCancellation.Token);
        try
        {
            await process.WaitForExitAsync(timeoutCancellation.Token);
        }
        catch (OperationCanceledException)
        {
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
            catch (InvalidOperationException) { }
            await process.WaitForExitAsync(CancellationToken.None);
            try { await Task.WhenAll(standardOutputTask, standardErrorTask); }
            catch (OperationCanceledException) { }
            cancellationToken.ThrowIfCancellationRequested();
            throw new TimeoutException("Gitの処理がタイムアウトしました。");
        }

        var output = await standardOutputTask;
        var error = await standardErrorTask;
        if (output.Truncated || error.Truncated) throw new InvalidDataException("Gitの出力が上限を超えたため処理を中止しました。");
        return new GitCommandResult(process.ExitCode, output.Text, error.Text);
    }

    public async Task<string?> GetGitHubCredentialUsernameAsync(string workingDirectory, CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(workingDirectory)) throw new DirectoryNotFoundException("Gitの作業フォルダーがありません。");
        var startInfo = CreateStartInfo(workingDirectory);
        startInfo.ArgumentList.Add("-c");
        startInfo.ArgumentList.Add("credential.helper=");
        startInfo.ArgumentList.Add("-c");
        startInfo.ArgumentList.Add("credential.helper=manager");
        startInfo.ArgumentList.Add("credential");
        startInfo.ArgumentList.Add("fill");
        startInfo.StandardOutputEncoding = Encoding.UTF8;
        startInfo.StandardErrorEncoding = Encoding.UTF8;
        startInfo.RedirectStandardInput = true;
        using var process = new Process { StartInfo = startInfo };
        if (!process.Start()) throw new InvalidOperationException("Git Credential Managerを起動できませんでした。");
        using var timeoutCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCancellation.CancelAfter(TimeSpan.FromSeconds(45));
        var usernameTask = ReadCredentialUsernameAsync(process.StandardOutput, timeoutCancellation.Token);
        var discardErrorTask = DrainAsync(process.StandardError, timeoutCancellation.Token);
        try
        {
            await process.StandardInput.WriteAsync("protocol=https\nhost=github.com\n\n".AsMemory(), timeoutCancellation.Token);
            process.StandardInput.Close();
            await process.WaitForExitAsync(timeoutCancellation.Token);
        }
        catch (OperationCanceledException)
        {
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
            catch (InvalidOperationException) { }
            await process.WaitForExitAsync(CancellationToken.None);
            try { await Task.WhenAll(usernameTask, discardErrorTask); }
            catch (OperationCanceledException) { }
            cancellationToken.ThrowIfCancellationRequested();
            throw new TimeoutException("Git Credential Managerの応答がタイムアウトしました。");
        }

        var username = await usernameTask;
        await discardErrorTask;
        return process.ExitCode == 0 ? username : null;
    }

    private static ProcessStartInfo CreateStartInfo(string workingDirectory) => new("git")
    {
        WorkingDirectory = Path.GetFullPath(workingDirectory),
        UseShellExecute = false,
        CreateNoWindow = true,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        StandardOutputEncoding = Encoding.UTF8,
        StandardErrorEncoding = Encoding.UTF8,
        Environment = { ["GIT_TERMINAL_PROMPT"] = "0", ["GIT_OPTIONAL_LOCKS"] = "0" }
    };

    private static async Task<string?> ReadCredentialUsernameAsync(StreamReader reader, CancellationToken cancellationToken)
    {
        var buffer = new char[1024];
        var key = new StringBuilder(32);
        var username = new StringBuilder(128);
        var inValue = false;
        var captureUsername = false;
        string? result = null;
        while (true)
        {
            var count = await reader.ReadAsync(buffer.AsMemory(), cancellationToken);
            if (count == 0) break;
            for (var index = 0; index < count; index++)
            {
                var character = buffer[index];
                if (character == '\r') continue;
                if (character == '\n')
                {
                    if (captureUsername && username.Length > 0) result = username.ToString();
                    key.Clear();
                    username.Clear();
                    inValue = false;
                    captureUsername = false;
                    continue;
                }

                if (!inValue)
                {
                    if (character == '=')
                    {
                        inValue = true;
                        captureUsername = key.ToString() == "username";
                    }
                    else if (key.Length < 32)
                    {
                        key.Append(character);
                    }
                }
                else if (captureUsername && username.Length < 128)
                {
                    username.Append(character);
                }
            }
            Array.Clear(buffer, 0, count);
        }

        if (captureUsername && username.Length > 0) result = username.ToString();
        return result;
    }

    private static async Task DrainAsync(StreamReader reader, CancellationToken cancellationToken)
    {
        var buffer = new char[4096];
        while (true)
        {
            var count = await reader.ReadAsync(buffer.AsMemory(), cancellationToken);
            if (count == 0) return;
            Array.Clear(buffer, 0, count);
        }
    }

    private static async Task<CapturedText> ReadBoundedAsync(StreamReader reader, CancellationToken cancellationToken)
    {
        var builder = new StringBuilder();
        var buffer = new char[8192];
        var truncated = false;
        while (true)
        {
            var count = await reader.ReadAsync(buffer.AsMemory(), cancellationToken);
            if (count == 0) break;
            var remaining = MaximumCapturedCharacters - builder.Length;
            if (remaining > 0) builder.Append(buffer, 0, Math.Min(count, remaining));
            if (count > remaining) truncated = true;
        }

        return new CapturedText(builder.ToString(), truncated);
    }

    private sealed record CapturedText(string Text, bool Truncated);
}
