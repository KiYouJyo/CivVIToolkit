using System.Diagnostics;
using System.Security.Cryptography;
using CivVIToolkit.Core.Game;
using CivVIToolkit.Core.Trainer;
using CivVIToolkit.Platform.Windows.Memory;

namespace CivVIToolkit.Platform.Windows.Trainer;

public sealed class SteamDx12Build1023995Probe : ITrainerBuildProbe
{
    public const string ProfileId = "steam-dx12-gs-1.0.12.68-1023995";
    public const string ExpectedGameCoreSha256 = "324c51e9ea3531758842e16c69e6cddbefbb226c5b675c3d6e60111646c2e98c";

    private const int GameRootGlobalRva = 0xB8A720;
    private const int PlayerManagerGlobalRva = 0xB8E140;
    private const int GameRootGameOffset = 0x08;
    private const int LocalPlayerIdOffset = 0x16F8;
    private const int PlayerArrayOffset = 0x20;
    private const int PlayerStateArrayOffset = 0x2B48;
    private const int PlayerComponentsOffset = 0xB0;
    private const int ReligionComponentOffset = 0x1090;
    private const int TreasuryComponentOffset = 0x1260;
    private const int InfluenceComponentOffset = 0x1450;
    private const int FaithBalanceOffset = 0xB0;
    private const int GoldBalanceOffset = 0xA8;
    private const int InfluencePointsOffset = 0xB8;
    private const double FixedPointScale = 256.0;

    private static readonly SignatureCheck[] SignatureChecks =
    [
        new("game-root", 0xA200, "48 8B 05 19 05 B8 00 C3 CC CC CC CC CC CC CC CC 48 83 EC 28 48 8B 01"),
        new("game-context-current-game", 0x956030, "48 8B 41 08 C3 CC CC CC CC CC CC CC CC CC CC CC 48 89 5C 24 10"),
        new("local-player-id", 0x696CC0, "8B 81 F8 16 00 00 C3 CC CC CC CC CC CC CC CC CC 40 53 48 83 EC 20"),
        new("player-manager", 0x306B80, "48 8B 05 B9 75 88 00 C3 CC CC CC CC CC CC CC CC 48 8D 81 E0 2D 00 00"),
        new("treasury-accessor", 0xBDCD0, "48 8B 81 B0 00 00 00 48 05 60 12 00 00 C3"),
        new("religion-accessor", 0xBE2C0, "48 8B 81 B0 00 00 00 48 05 90 10 00 00 C3"),
        new("influence-accessor", 0xBDC90, "48 8B 81 B0 00 00 00 48 05 50 14 00 00 C3"),
        new("change-gold", 0x3432E0, "48 83 EC 28 44 8B 02 4C 8B C9 8B 91 A8 00 00 00 45 85 C0 78 ?? B8 FF FF FF 7F"),
        new("set-gold", 0x343D30, "40 53 48 83 EC 30 8B 02 48 8B D9 39 81 A8 00 00 00 74 ?? 44 0F B6 44 24 40"),
        new("set-faith", 0x316090, "48 89 5C 24 10 57 48 83 EC 20 8B 02 48 8B FA 48 8B D9 39 81 B0 00 00 00 74 ??"),
    ];

    public async Task<TrainerBuildProbeSnapshot> ProbeAsync(
        GameSession session,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        cancellationToken.ThrowIfCancellationRequested();

        if (session.Store != GameStore.Steam || session.GraphicsBackend != GraphicsBackend.DirectX12)
        {
            throw new NotSupportedException("This research profile is verified only for the Steam DX12 build.");
        }

        if (string.IsNullOrWhiteSpace(session.GameCoreModulePath) || !File.Exists(session.GameCoreModulePath))
        {
            throw new InvalidOperationException("Enter a Gathering Storm match before validating this build.");
        }

        var hash = await ComputeSha256Async(session.GameCoreModulePath, cancellationToken);
        if (!hash.Equals(ExpectedGameCoreSha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException($"Unsupported GameCore build: {hash}.");
        }

        using var process = Process.GetProcessById(session.ProcessId);
        var module = process.Modules.Cast<ProcessModule>().FirstOrDefault(candidate =>
            string.Equals(candidate.FileName, session.GameCoreModulePath, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("The verified Gathering Storm GameCore module is no longer loaded.");

        var moduleBase = module.BaseAddress;
        var moduleSize = module.ModuleMemorySize;
        using var memory = new ProcessMemoryAccessor(session.ProcessId, allowWrite: false);
        var scanner = new AobScanner(memory);
        var verified = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var check in SignatureChecks)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var match = scanner.FindFirst(moduleBase, moduleSize, AobPattern.Parse(check.Pattern));
            if (match is null)
            {
                throw new InvalidOperationException($"Signature '{check.Name}' was not found in the loaded GameCore module.");
            }

            var rva = checked((long)(match.Value - moduleBase));
            if (rva != check.ExpectedRva)
            {
                throw new InvalidOperationException($"Signature '{check.Name}' resolved to RVA 0x{rva:X}, expected 0x{check.ExpectedRva:X}.");
            }

            verified[check.Name] = $"0x{rva:X}";
        }

        // The verified game-context vtable getter at RVA 0x956030 is exactly
        // `mov rax, [rcx+8]; ret`, so reading +0x08 is equivalent to the game's
        // own current-game accessor without executing code in the target process.
        var gameRoot = ReadPointer(memory, moduleBase + GameRootGlobalRva, "game root");
        var game = ReadPointer(memory, gameRoot + GameRootGameOffset, "game instance");
        var localPlayerId = ReadInt32(memory, game + LocalPlayerIdOffset, "local player ID");
        if ((uint)localPlayerId >= 0x41)
        {
            throw new InvalidOperationException($"Local player ID {localPlayerId} is outside the expected 0-64 range.");
        }

        // Player::Manager::Get at RVA 0x306B80 resolves the singleton from
        // GameCore+0xB8E140. Its active-slot table is at +0x2B48 and the player
        // pointer table is at +0x20; both contracts are visible in the same build.
        var manager = ReadPointer(memory, moduleBase + PlayerManagerGlobalRva, "player manager");
        var states = ReadPointer(memory, manager + PlayerStateArrayOffset, "player state array");
        var state = ReadInt32(memory, states + (localPlayerId * sizeof(int)), "local player state");
        if (state == -1)
        {
            throw new InvalidOperationException("The local player slot is not active in the player manager.");
        }

        var players = ReadPointer(memory, manager + PlayerArrayOffset, "player array");
        var player = ReadPointer(memory, players + (localPlayerId * IntPtr.Size), "local player");
        var components = ReadPointer(memory, player + PlayerComponentsOffset, "local player component block");

        var faithRaw = ReadInt32(memory, components + ReligionComponentOffset + FaithBalanceOffset, "faith balance");
        var goldRaw = ReadInt32(memory, components + TreasuryComponentOffset + GoldBalanceOffset, "gold balance");
        var influenceRaw = ReadInt32(memory, components + InfluenceComponentOffset + InfluencePointsOffset, "influence points");

        return new TrainerBuildProbeSnapshot(
            ProfileId,
            hash,
            localPlayerId,
            goldRaw / FixedPointScale,
            faithRaw / FixedPointScale,
            influenceRaw / FixedPointScale,
            verified);
    }

    private static nint ReadPointer(ProcessMemoryAccessor memory, nint address, string label)
    {
        var bytes = ReadExact(memory, address, IntPtr.Size, label);
        var value = IntPtr.Size == sizeof(long)
            ? checked((nint)BitConverter.ToInt64(bytes, 0))
            : checked((nint)BitConverter.ToInt32(bytes, 0));
        if (value == 0)
        {
            throw new InvalidOperationException($"The {label} pointer is null.");
        }

        return value;
    }

    private static int ReadInt32(ProcessMemoryAccessor memory, nint address, string label) =>
        BitConverter.ToInt32(ReadExact(memory, address, sizeof(int), label), 0);

    private static byte[] ReadExact(ProcessMemoryAccessor memory, nint address, int length, string label)
    {
        var bytes = memory.Read(address, length);
        if (bytes.Length != length)
        {
            throw new InvalidOperationException($"Could not read the complete {label} value at 0x{address:X}.");
        }

        return bytes;
    }

    private static async Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken)
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

    private sealed record SignatureCheck(string Name, int ExpectedRva, string Pattern);
}
