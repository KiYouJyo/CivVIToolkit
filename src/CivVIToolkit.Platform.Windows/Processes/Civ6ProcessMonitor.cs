using System.Diagnostics;
using System.Text.RegularExpressions;
using CivVIToolkit.Core.Game;

namespace CivVIToolkit.Platform.Windows.Processes;

public sealed partial class Civ6ProcessMonitor : IGameProcessMonitor
{
    private const string GatheringStormModuleName = "GameCore_XP2_FinalRelease.dll";
    private static readonly string[] ProcessNames = ["CivilizationVI_DX12", "CivilizationVI"];

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
                        return session;
                    }
                }
            }
        }

        return null;
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
