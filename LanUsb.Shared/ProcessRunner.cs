using System.Diagnostics;
using System.Text;

namespace LanUsb.Shared;

public static class ProcessRunner
{
    public static async Task<CommandResult> RunAsync(
        string fileName,
        string arguments,
        int timeoutMs = 15000,
        CancellationToken ct = default)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        using var process = new Process { StartInfo = psi };
        try
        {
            process.Start();
        }
        catch (Exception ex)
        {
            return new CommandResult
            {
                Success = false,
                ExitCode = -1,
                StdErr = ex.Message
            };
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeoutMs);

        var stdOutTask = process.StandardOutput.ReadToEndAsync(cts.Token);
        var stdErrTask = process.StandardError.ReadToEndAsync(cts.Token);

        try
        {
            await process.WaitForExitAsync(cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch { }
            return new CommandResult
            {
                Success = false,
                ExitCode = -2,
                StdErr = "Przekroczono limit czasu wykonania polecenia."
            };
        }

        var stdOut = await stdOutTask.ConfigureAwait(false);
        var stdErr = await stdErrTask.ConfigureAwait(false);

        return new CommandResult
        {
            Success = process.ExitCode == 0,
            ExitCode = process.ExitCode,
            StdOut = stdOut,
            StdErr = stdErr
        };
    }

    public static async Task<CommandResult> RunElevatedAsync(
        string fileName,
        string arguments,
        int timeoutMs = 30000,
        CancellationToken ct = default)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            UseShellExecute = true,
            Verb = "runas",
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        };

        using var process = new Process { StartInfo = psi };
        try
        {
            process.Start();
        }
        catch (Exception ex)
        {
            return new CommandResult
            {
                Success = false,
                ExitCode = -1,
                StdErr = ex.Message
            };
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeoutMs);

        try
        {
            await process.WaitForExitAsync(cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch { }
            return new CommandResult
            {
                Success = false,
                ExitCode = -2,
                StdErr = "Przekroczono limit czasu wykonania polecenia (UAC)."
            };
        }

        return new CommandResult
        {
            Success = process.ExitCode == 0,
            ExitCode = process.ExitCode
        };
    }

    public static string? FindExecutable(string exeName, params string[] extraPaths)
    {
        foreach (var path in extraPaths)
        {
            if (File.Exists(path))
                return path;
        }

        var pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (!string.IsNullOrWhiteSpace(pathEnv))
        {
            foreach (var dir in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                var candidate = Path.Combine(dir.Trim(), exeName);
                if (File.Exists(candidate))
                    return candidate;
            }
        }

        return null;
    }
}
