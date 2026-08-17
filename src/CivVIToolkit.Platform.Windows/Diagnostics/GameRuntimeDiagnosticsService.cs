using System.Diagnostics;
using System.Security.Cryptography;
using CivVIToolkit.Core.Game;

namespace CivVIToolkit.Platform.Windows.Diagnostics;

public sealed class GameRuntimeDiagnosticsService : IGameRuntimeDiagnosticsService
{
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
        var fileVersion = module.FileVersionInfo.FileVersion ?? session.FileVersion?.ToString();

        await using var stream = new FileStream(
            executablePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete,
            1024 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        var digest = await SHA256.HashDataAsync(stream, cancellationToken);

        return new GameRuntimeDiagnostics(
            DateTimeOffset.UtcNow,
            session.ProcessId,
            session.Store,
            session.GraphicsBackend,
            executablePath,
            fileVersion,
            Convert.ToHexString(digest).ToLowerInvariant(),
            baseAddress,
            imageSize,
            session.InstallDirectory,
            session.MatchedKnownInstallation);
    }
}
