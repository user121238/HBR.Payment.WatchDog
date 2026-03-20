using System.Diagnostics;
using System.IO;

namespace HBR.Payment.WatchDog;

internal static class WatchDogProcessHelper
{
    public static int GetRunningInstanceCount(string executablePath)
    {
        var processName = Path.GetFileNameWithoutExtension(executablePath);
        var runningCount = 0;

        foreach (var process in Process.GetProcessesByName(processName))
        {
            try
            {
                var currentPath = process.MainModule?.FileName;
                if (PathsEqual(currentPath, executablePath))
                {
                    runningCount++;
                }
            }
            catch
            {
                // Ignore processes that do not allow module inspection.
            }
            finally
            {
                process.Dispose();
            }
        }

        return runningCount;
    }

    public static void StartProcess(string executablePath, string arguments, string workingDirectory)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            Arguments = arguments ?? string.Empty,
            WorkingDirectory = workingDirectory,
            UseShellExecute = true
        };

        Process.Start(startInfo)?.Dispose();
    }

    public static void KillMatchingProcesses(string executablePath)
    {
        var processName = Path.GetFileNameWithoutExtension(executablePath);
        foreach (var process in Process.GetProcessesByName(processName))
        {
            try
            {
                var currentPath = process.MainModule?.FileName;
                if (!PathsEqual(currentPath, executablePath))
                {
                    continue;
                }

                process.Kill(entireProcessTree: true);
                process.WaitForExit(5000);
            }
            catch
            {
                // Best effort shutdown on exit.
            }
            finally
            {
                process.Dispose();
            }
        }
    }

    private static bool PathsEqual(string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
        {
            return false;
        }

        return string.Equals(
            Path.GetFullPath(left),
            Path.GetFullPath(right),
            StringComparison.OrdinalIgnoreCase);
    }
}
