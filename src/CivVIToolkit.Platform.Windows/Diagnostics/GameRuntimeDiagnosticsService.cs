using System.Diagnostics;
using System.Security.Cryptography;
using CivVIToolkit.Core.Game;

namespace CivVIToolkit.Platform.Windows.Diagnostics;

public sealed class GameRuntimeDiagnosticsService : IGameRuntimeDiagnosticsService
{
    private const string GatheringStormModuleName = "GameCore_XP2_FinalRelease.dll";

    public async Task<GameRuntimeDiagnostics> CaptureAsync(
        GameSession session,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        cancellationToken.ThrowIfCancellationRequested();

        using var process = Process.GetProcessById(session.ProcessId);
        var module = process.MainModule
            ?? throw new InvalidOperationException("The Civilization VI main module is unavailable.");

        var executablePath = module.FileName;
        if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
        {
            throw new FileNotFoundException("The running Civilization VI executable could not be read.", executablePath);
        }

        var baseAddress = $"0x{module.BaseAddress.ToInt64():X}";
        var imageSize = module.ModuleMemorySize;
        var fileVersion = module.FileVersionInfo.FileVersion ?? session.FileVersionText ?? session.FileVersion?.ToString();
        var executableSha256 = await HashFileAsync(executablePath, cancellationToken);
        var gameCore = await CaptureGameCoreAsync(process, cancellationToken);

        return new GameRuntimeDiagnostics(
            DateTimeOffset.UtcNow,
            session.ProcessId,
            session.Store,
            session.GraphicsBackend,
            executablePath,
            fileVersion,
            executableSha256,
            baseAddress,
            imageSize,
            session.InstallDirectory,
            session.MatchedKnownInstallation,
            gameCore);
    }

    private static async Task<GameModuleDiagnostics?> CaptureGameCoreAsync(
        Process process,
        CancellationToken cancellationToken)
    {
        ProcessModule? gameCoreModule = null;
        foreach (ProcessModule candidate in process.Modules)
        {
            if (string.Equals(candidate.ModuleName, GatheringStormModuleName, StringComparison.OrdinalIgnoreCase))
            {
                gameCoreModule = candidate;
                break;
            }
        }

        if (gameCoreModule is null)
        {
            return null;
        }

        var path = gameCoreModule.FileName;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return null;
        }

        var symbolMapPath = Path.ChangeExtension(path, ".map");
        if (!File.Exists(symbolMapPath))
        {
            symbolMapPath = null;
        }

        return new GameModuleDiagnostics(
            gameCoreModule.ModuleName,
            path,
            gameCoreModule.FileVersionInfo.FileVersion,
            await HashFileAsync(path, cancellationToken),
            $"0x{gameCoreModule.BaseAddress.ToInt64():X}",
            gameCoreModule.ModuleMemorySize,
            symbolMapPath);
    }

    private static async Task<string> HashFileAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete,
            1024 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        var digest = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(digest).ToLowerInvariant();
    }
}
