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
    private const int MaximumPlayerSlots = 65;
    private const int VerifiedSinglePlayerFallbackSlot = 0;

    // The live Player::Instance returned by Player::Manager does not use the
    // Player::Cache component block at +0xB0. The game's own bridge functions
    // at 0x59CE70/0x59CEE0/0x59CFD0 prove these direct component pointers.
    private const int LivePlayerReligionPointerOffset = 0x720;
    private const int LivePlayerInfluencePointerOffset = 0x748;
    private const int LivePlayerTreasuryPointerOffset = 0x780;

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
        new("cache-treasury-accessor", 0xBDCD0, "48 8B 81 B0 00 00 00 48 05 60 12 00 00 C3"),
        new("cache-religion-accessor", 0xBE2C0, "48 8B 81 B0 00 00 00 48 05 90 10 00 00 C3"),
        new("cache-influence-accessor", 0xBDC90, "48 8B 81 B0 00 00 00 48 05 50 14 00 00 C3"),
        new("live-faith-bridge", 0x59CE70, "40 53 48 83 EC 20 48 8B 41 08 48 8B DA 48 85 C0 74 ?? 48 8B 80 20 07 00 00 8B 88 B0 00 00 00"),
        new("live-gold-bridge", 0x59CEE0, "40 53 48 83 EC 20 48 8B 41 08 48 8B DA 48 85 C0 74 ?? 48 8B 80 80 07 00 00 8B 88 A8 00 00 00"),
        new("live-influence-bridge", 0x59CFD0, "40 53 48 83 EC 20 48 83 79 08 00 48 8B D9 B9 20 00 00 00 74 ?? E8 ?? ?? ?? ?? 48 85 C0 74 ?? 48 8B 4B 08 48 8B 91 48 07 00 00"),
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

        // Player::Manager remains live in a loaded single-player match even when
        // the GameContext root is transiently cleared by the game UI/state machine.
        // Resolve it first so a null GameContext does not poison all trainer rows.
        var manager = ReadPointer(memory, moduleBase + PlayerManagerGlobalRva, "player manager");
        var states = ReadPointer(memory, manager + PlayerStateArrayOffset, "player state array");
        var players = ReadPointer(memory, manager + PlayerArrayOffset, "player array");

        var (localPlayerId, localPlayerRoute) = ResolveLocalPlayerId(memory, moduleBase, states, players);
        var state = ReadInt32(memory, states + (localPlayerId * sizeof(int)), "local player state");
        if (state == -1)
        {
            throw new InvalidOperationException("The local player slot is not active in the player manager.");
        }

        var player = ReadPointer(memory, players + (localPlayerId * IntPtr.Size), "local player");

        var religion = ReadPointer(memory, player + LivePlayerReligionPointerOffset, "live player religion component");
        var treasury = ReadPointer(memory, player + LivePlayerTreasuryPointerOffset, "live player treasury component");
        var influence = ReadPointer(memory, player + LivePlayerInfluencePointerOffset, "live player influence component");

        var faithRaw = ReadInt32(memory, religion + FaithBalanceOffset, "faith balance");
        var goldRaw = ReadInt32(memory, treasury + GoldBalanceOffset, "gold balance");
        var influenceRaw = ReadInt32(memory, influence + InfluencePointsOffset, "influence points");

        return new TrainerBuildProbeSnapshot(
            ProfileId,
            hash,
            localPlayerId,
            goldRaw / FixedPointScale,
            faithRaw / FixedPointScale,
            influenceRaw / FixedPointScale,
            verified,
            goldRaw,
            faithRaw,
            influenceRaw,
            $"Player::Manager live instance -> direct component pointers; local player via {localPlayerRoute}",
            FormatAddress(manager),
            FormatAddress(player),
            FormatAddress(treasury),
            FormatAddress(religion),
            FormatAddress(influence));
    }

    private static (int PlayerId, string Route) ResolveLocalPlayerId(
        ProcessMemoryAccessor memory,
        nint moduleBase,
        nint states,
        nint players)
    {
        // Preferred route: the verified GameContext chain. This was validated in
        // the initial live probes and remains the authoritative local-player ID.
        var gameRoot = ReadPointerOrZero(memory, moduleBase + GameRootGlobalRva);
        if (gameRoot != 0)
        {
            var game = ReadPointerOrZero(memory, gameRoot + GameRootGameOffset);
            if (game != 0)
            {
                var playerId = ReadInt32OrDefault(memory, game + LocalPlayerIdOffset, -1);
                if ((uint)playerId < MaximumPlayerSlots && IsUsablePlayerSlot(memory, states, players, playerId))
                {
                    return (playerId, "GameContext");
                }
            }
        }

        // Exact-build single-player fallback. Earlier live verification for this
        // SHA-locked profile established that the human/local player occupies slot
        // zero. We only accept it when the slot is active, has a live Player object,
        // and exposes the already-verified Treasury/Religion/Influence components.
        if (IsUsablePlayerSlot(memory, states, players, VerifiedSinglePlayerFallbackSlot))
        {
            var player = ReadPointerOrZero(memory, players + (VerifiedSinglePlayerFallbackSlot * IntPtr.Size));
            if (player != 0
                && ReadPointerOrZero(memory, player + LivePlayerTreasuryPointerOffset) != 0
                && ReadPointerOrZero(memory, player + LivePlayerReligionPointerOffset) != 0
                && ReadPointerOrZero(memory, player + LivePlayerInfluencePointerOffset) != 0)
            {
                return (VerifiedSinglePlayerFallbackSlot, "verified single-player manager slot 0 fallback");
            }
        }

        throw new InvalidOperationException(
            "The game context root is unavailable and the verified single-player slot 0 fallback could not be validated.");
    }

    private static bool IsUsablePlayerSlot(ProcessMemoryAccessor memory, nint states, nint players, int playerId)
    {
        if ((uint)playerId >= MaximumPlayerSlots)
        {
            return false;
        }

        var state = ReadInt32OrDefault(memory, states + (playerId * sizeof(int)), -1);
        var player = ReadPointerOrZero(memory, players + (playerId * IntPtr.Size));
        return state != -1 && player != 0;
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

    private static nint ReadPointerOrZero(ProcessMemoryAccessor memory, nint address)
    {
        try
        {
            var bytes = memory.Read(address, IntPtr.Size);
            if (bytes.Length != IntPtr.Size)
            {
                return 0;
            }

            return IntPtr.Size == sizeof(long)
                ? checked((nint)BitConverter.ToInt64(bytes, 0))
                : checked((nint)BitConverter.ToInt32(bytes, 0));
        }
        catch
        {
            return 0;
        }
    }

    private static int ReadInt32OrDefault(ProcessMemoryAccessor memory, nint address, int fallback)
    {
        try
        {
            var bytes = memory.Read(address, sizeof(int));
            return bytes.Length == sizeof(int) ? BitConverter.ToInt32(bytes, 0) : fallback;
        }
        catch
        {
            return fallback;
        }
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

    private static string FormatAddress(nint address) => $"0x{address:X}";

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
