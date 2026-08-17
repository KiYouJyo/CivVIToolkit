using System.Diagnostics;
using System.Text.RegularExpressions;
using CivVIToolkit.Core.Game;

namespace CivVIToolkit.Platform.Windows.Processes;

public sealed partial class Civ6ProcessMonitor : IGameProcessMonitor
{
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
            var executablePath = process.MainModule?.FileName;
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
            var fileVersion = ParseVersion(process.MainModule?.FileVersionInfo.FileVersion);

            return new GameSession(
                process.Id,
                executablePath,
                process.ProcessName,
                store,
                backend,
                matched?.InstallDirectory ?? FindLikelyInstallRoot(executablePath),
                fileVersion,
                matched is not null);
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException)
        {
            return null;
        }
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

        var match = VersionRegex().Match(text);
        return match.Success && Version.TryParse(match.Value, out var version) ? version : null;
    }

    [GeneratedRegex(@"\d+(?:\.\d+){1,3}")]
    private static partial Regex VersionRegex();
}
