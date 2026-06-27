using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;

namespace DiskSpaceTracker.Services;

/// <summary>
/// Launches an external command line. Routing rules:
/// <list type="bullet">
/// <item>.ps1 → <c>powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File &lt;path&gt;</c></item>
/// <item>.bat / .cmd → <c>cmd.exe /c "&lt;path&gt;" &lt;args&gt;</c></item>
/// <item>.msi → <c>msiexec.exe /i "&lt;path&gt;"</c></item>
/// <item>.lnk → <c>Process.Start(UseShellExecute=true)</c> so Windows resolves the shortcut target itself.</item>
/// <item>.exe → direct <c>Process.Start</c>.</item>
/// <item>arbitrary command → <c>cmd.exe /c &lt;command&gt;</c>.</item>
/// </list>
/// All routes set the working directory to the executable's parent when known.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class ExecuteLauncher : IExecuteLauncher
{
    private readonly ILogger<ExecuteLauncher> _logger;

    public ExecuteLauncher(ILogger<ExecuteLauncher> logger) => _logger = logger;

    public Process? Launch(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload)) return null;

        try
        {
            var command = Environment.ExpandEnvironmentVariables(payload).Trim();
            if (command.Length == 0) return null;

            var (exe, arguments) = SplitFirstToken(command);

            string workingDirectory = Environment.CurrentDirectory;
            if (exe is not null)
            {
                var parent = Path.GetDirectoryName(exe);
                if (!string.IsNullOrEmpty(parent) && Directory.Exists(parent))
                    workingDirectory = parent;

                var ext = Path.GetExtension(exe).ToLowerInvariant();
                return ext switch
                {
                    ".ps1" => StartProcess("powershell.exe", BuildPs1Args(exe, arguments), workingDirectory, useShellExecute: false),
                    ".bat" or ".cmd" => StartCmd(exe, arguments, workingDirectory),
                    ".msi" => StartProcess("msiexec.exe", BuildMsiArgs(exe, arguments), workingDirectory, useShellExecute: false),
                    ".lnk" => StartShellExecute(exe, arguments, workingDirectory),
                    _ => string.IsNullOrWhiteSpace(arguments)
                        ? StartProcess(exe, Array.Empty<string>(), workingDirectory, useShellExecute: false)
                        : StartProcess(exe, SplitArgs(arguments), workingDirectory, useShellExecute: false),
                };
            }

            return StartCmd(null, command, workingDirectory);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ExecuteLauncher failed for payload: {Payload}", payload);
            return null;
        }
    }

    public bool IsRunning(Process? process)
    {
        if (process is null) return false;
        try
        {
            return !process.HasExited;
        }
        catch (InvalidOperationException)
        {
            // Process object no longer associated with a running process.
            return false;
        }
    }

    private static (string? exe, string arguments) SplitFirstToken(string command)
    {
        if (command.StartsWith('"'))
        {
            var end = command.IndexOf('"', 1);
            if (end > 1)
            {
                var candidate = command[1..end];
                var rest = command[(end + 1)..].Trim();
                if (File.Exists(candidate)) return (candidate, rest);
            }
        }

        var firstSpace = command.IndexOf(' ');
        if (firstSpace > 0)
        {
            var head = command[..firstSpace];
            var tail = command[(firstSpace + 1)..];
            if (File.Exists(head)) return (head, tail);
        }
        else if (File.Exists(command))
        {
            return (command, string.Empty);
        }

        return (null, command);
    }

    private static IEnumerable<string> BuildPs1Args(string exe, string arguments)
    {
        var args = new List<string>(8) { "-NoLogo", "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", $"\"{exe}\"" };
        if (!string.IsNullOrWhiteSpace(arguments))
            args.AddRange(SplitArgs(arguments));
        return args;
    }

    private static IEnumerable<string> BuildMsiArgs(string exe, string arguments)
    {
        var args = new List<string>(4) { "/i", $"\"{exe}\"" };
        if (!string.IsNullOrWhiteSpace(arguments))
            args.AddRange(SplitArgs(arguments));
        return args;
    }

    private static List<string> SplitArgs(string s)
    {
        var result = new List<string>();
        var sb = new System.Text.StringBuilder();
        bool inQuotes = false;
        for (int i = 0; i < s.Length; i++)
        {
            var c = s[i];
            if (c == '"') { inQuotes = !inQuotes; sb.Append(c); continue; }
            if (!inQuotes && char.IsWhiteSpace(c))
            {
                if (sb.Length > 0) { result.Add(sb.ToString()); sb.Clear(); }
                continue;
            }
            sb.Append(c);
        }
        if (sb.Length > 0) result.Add(sb.ToString());
        return result;
    }

    private static Process? StartProcess(string fileName, IEnumerable<string> arguments, string workingDirectory, bool useShellExecute)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            WorkingDirectory = workingDirectory,
            UseShellExecute = useShellExecute,
            CreateNoWindow = false,
            WindowStyle = ProcessWindowStyle.Normal,
        };
        foreach (var arg in arguments) psi.ArgumentList.Add(arg);

        try
        {
            // Return the live Process (the caller owns/disposes it) so completion can be polled via
            // HasExited on this exact object, avoiding a GetProcessById(pid) that a recycled PID
            // could spoof.
            return Process.Start(psi);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Launch through <c>cmd.exe</c> using the robust <c>/s /c "&lt;command&gt;"</c> form: with
    /// <c>/s</c>, cmd strips exactly the outermost pair of quotes and runs the rest verbatim, which
    /// correctly handles a quoted executable path that contains spaces (the plain ArgumentList form
    /// collides with cmd's own quote parsing). <paramref name="exe"/> is null for a raw arbitrary
    /// command (already a full command line); otherwise it is the script/exe path to quote.
    /// </summary>
    private static Process? StartCmd(string? exe, string arguments, string workingDirectory)
    {
        string inner = exe is null
            ? arguments
            : string.IsNullOrWhiteSpace(arguments) ? $"\"{exe}\"" : $"\"{exe}\" {arguments}";
        return StartProcessRaw("cmd.exe", $"/s /c \"{inner}\"", workingDirectory);
    }

    private static Process? StartProcessRaw(string fileName, string rawArguments, string workingDirectory)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = rawArguments,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            CreateNoWindow = false,
            WindowStyle = ProcessWindowStyle.Normal,
        };
        // Let Process.Start exceptions bubble to Launch's catch, which logs them with the payload.
        return Process.Start(psi);
    }

    private static Process? StartShellExecute(string fileName, string arguments, string workingDirectory)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            WorkingDirectory = workingDirectory,
            UseShellExecute = true,
            WindowStyle = ProcessWindowStyle.Normal,
        };
        if (!string.IsNullOrWhiteSpace(arguments))
            psi.Arguments = arguments;

        // Let Process.Start exceptions bubble to Launch's catch, which logs them with the payload.
        return Process.Start(psi);
    }
}
