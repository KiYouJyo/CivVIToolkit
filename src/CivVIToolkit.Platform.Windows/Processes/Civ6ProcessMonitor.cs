using System.Diagnostics;
using System.Text.RegularExpressions;
using CivVIToolkit.Core.Game;

namespace CivVIToolkit.Platform.Windows.Processes;

public sealed partial class Civ6ProcessMonitor : IGameProcessMonitor
{
    private const string GatheringStormModuleName = "GameCore_XP2_FinalRelease.dll";
    private const int MaxTransientMissScans = 2;
    private static readonly string[] ProcessNames = ["CivilizationVI_DX12", "CivilizationVI"];

    private GameSession? _lastStableSession;
    private int _missingSessionScans;
    private int _missingGameCoreScans;

    public GameSession? FindRunningSession(IReadOnlyList<GameInstallation> installations)
    {
        foreach (var processName in ProcessNames)
        {
            foreach (var process in Process.GetProcessesByName(processName))
            {
                using (process)
                {
                    var session = TryCreateSession(process, installations);
                    if (session is not null)
                    {
                        return StabilizeSession(session);
                    }
                }
            }
        }

        return PreserveSessionAcrossTransientProbeMiss();
    }

    private GameSession StabilizeSession(GameSession current)
    {
        _missingSessionScans = 0;

        if (_lastStableSession is { } previous && current.ProcessId == previous.ProcessId)
        {
            if (string.IsNullOrWhiteSpace(current.GameCoreModulePath)
                && !string.IsNullOrWhiteSpace(previous.GameCoreModulePath))
            {
                _missingGameCoreScans++;
                if (_missingGameCoreScans <= MaxTransientMissScans)
                {
                    current = current with { GameCoreModulePath = previous.GameCoreModulePath };
                }
            }
            else
            {
                _missingGameCoreScans = 0;
            }
        }
        else
        {
            _missingGameCoreScans = 0;
        }

        _lastStableSession = current;
        return current;
    }

    private GameSession? PreserveSessionAcrossTransientProbeMiss()
    {
        if (_lastStableSession is null)
        {
            return null;
        }

        try
        {
            using var process = Process.GetProcessById(_lastStableSession.ProcessId);
            if (process.HasExited)
            {
                ResetStableSession();
                return null;
            }

            _missingSessionScans++;
            if (_missingSessionScans <= MaxTransientMissScans)
            {
                return _lastStableSession;
            }
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException)
        {
            // The process is gone or temporarily inaccessible. A genuinely exited
            // process should not be kept attached just to hide a transient UI refresh.
        }

        ResetStableSession();
        return null;
    }

    private void ResetStableSession()
    {
        _lastStableSession = null;
        _missingSessionScans = 0;
        _missingGameCoreScans = 0;
    }

    private static GameSession? TryCreateSession(Process process, IReadOnlyList<GameInstallation> installations)
    {
        try
        {
            var mainModule = process.MainModule;
            var executablePath = mainModule?.FileName;
            if (string.IsNullOrWhiteSpace(executablePath))
            {
                return null;
            }

            var backend = Path.GetFileNameWithoutExtension(executablePath)
                .Contains("DX12", StringComparison.OrdinalIgnoreCase)
                ? GraphicsBackend.DirectX12
                : GraphicsBackend.DirectX11;

            var matched = installations.FirstOrDefault(installation => IsUnder(executablePath, installation.InstallDirectory));
            var store = matched?.Store ?? InferStore(executablePath);
            var fileVersionText = mainModule?.FileVersionInfo.FileVersion;
            var fileVersion = ParseVersion(fileVersionText);
            var gameCoreModulePath = FindLoadedModulePath(process, GatheringStormModuleName);

            return new GameSession(
                process.Id,
                executablePath,
                process.ProcessName,
                store,
                backend,
                matched?.InstallDirectory ?? FindLikelyInstallRoot(executablePath),
                fileVersion,
                matched is not null,
                fileVersionText,
                gameCoreModulePath);
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException)
        {
            return null;
        }
    }

    private static string? FindLoadedModulePath(Process process, string moduleName)
    {
        try
        {
            foreach (ProcessModule module in process.Modules)
            {
                if (string.Equals(module.ModuleName, moduleName, StringComparison.OrdinalIgnoreCase))
                {
                    return module.FileName;
                }
            }
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException)
        {
            return null;
        }

        return null;
    }

    private static bool IsUnder(string filePath, string directory)
    {
        var fullDirectory = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var fullFile = Path.GetFullPath(filePath);
        return fullFile.StartsWith(fullDirectory, StringComparison.OrdinalIgnoreCase);
    }

    private static GameStore InferStore(string executablePath)
    {
        if (executablePath.Contains("steamapps", StringComparison.OrdinalIgnoreCase))
        {
            return GameStore.Steam;
        }

        if (executablePath.Contains("Epic Games", StringComparison.OrdinalIgnoreCase)
            || executablePath.Contains("Win64EOS", StringComparison.OrdinalIgnoreCase))
        {
            return GameStore.EpicGames;
        }

        return GameStore.Unknown;
    }

    private static string? FindLikelyInstallRoot(string executablePath)
    {
        var directory = new FileInfo(executablePath).Directory;
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "Base"))
                || Directory.Exists(Path.Combine(directory.FullName, "DLC")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return null;
    }

    private static Version? ParseVersion(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var match = CivVersionRegex().Match(text);
        if (!match.Success)
        {
            return null;
        }

        return new Version(
            int.Parse(match.Groups["major"].Value),
            int.Parse(match.Groups["minor"].Value),
            int.Parse(match.Groups["build"].Value),
            int.Parse(match.Groups["revision"].Value));
    }

    [GeneratedRegex(@"(?<major>\d+)\s*[,\.]\s*(?<minor>\d+)\s*[,\.]\s*(?<build>\d+)\s*[,\.]\s*(?<revision>\d+)")]
    private static partial Regex CivVersionRegex();
}
