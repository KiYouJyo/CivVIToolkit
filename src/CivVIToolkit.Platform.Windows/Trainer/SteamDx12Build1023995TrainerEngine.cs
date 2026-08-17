using System.Globalization;
using CivVIToolkit.Core.Game;
using CivVIToolkit.Core.Trainer;
using CivVIToolkit.Platform.Windows.Memory;

namespace CivVIToolkit.Platform.Windows.Trainer;

public sealed class SteamDx12Build1023995TrainerEngine : ITrainerEngine
{
    private const string AddGoldFeatureId = "player.add-gold";
    private const int GoldBalanceOffset = 0xA8;
    private const long FixedPointScale = 256;

    private readonly SteamDx12Build1023995Probe _probe;
    private IReadOnlyList<TrainerFeatureState> _features = BuildStates(
        TrainerAvailability.NotAttached,
        "Start Civilization VI to attach.");

    public SteamDx12Build1023995TrainerEngine(SteamDx12Build1023995Probe probe)
    {
        _probe = probe ?? throw new ArgumentNullException(nameof(probe));
    }

    public GameSession? Session { get; private set; }
    public IReadOnlyList<TrainerFeatureState> Features => _features;

    public async Task AttachAsync(GameSession session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        cancellationToken.ThrowIfCancellationRequested();
        Session = session;

        if (!session.IsGatheringStormCoreLoaded)
        {
            _features = BuildStates(
                TrainerAvailability.SignaturePending,
                "Enter a Gathering Storm match to load the verified GameCore profile.");
            return;
        }

        try
        {
            await _probe.ProbeAsync(session, cancellationToken);
            _features = TrainerCatalog.All.Select(feature =>
                feature.Id == AddGoldFeatureId
                    ? new TrainerFeatureState(
                        feature,
                        TrainerAvailability.Available,
                        false,
                        "Verified for Steam / DX12 / Gathering Storm 1.0.12.68 (1023995).")
                    : new TrainerFeatureState(
                        feature,
                        TrainerAvailability.SignaturePending,
                        false,
                        "This feature still requires build-specific validation."))
                .ToArray();
        }
        catch (NotSupportedException exception)
        {
            _features = BuildStates(TrainerAvailability.UnsupportedGameVersion, exception.Message);
        }
        catch (Exception exception)
        {
            _features = BuildStates(TrainerAvailability.Error, exception.Message);
        }
    }

    public Task DetachAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Session = null;
        _features = BuildStates(TrainerAvailability.NotAttached, "Start Civilization VI to attach.");
        return Task.CompletedTask;
    }

    public Task SetEnabledAsync(string featureId, bool enabled, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException($"Feature '{featureId}' is not a toggle in the currently verified profile.");

    public async Task ExecuteAsync(string featureId, long? value = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!string.Equals(featureId, AddGoldFeatureId, StringComparison.Ordinal))
        {
            throw new NotSupportedException($"Feature '{featureId}' has no verified write path yet.");
        }

        var session = Session ?? throw new InvalidOperationException("Civilization VI is not attached.");
        var state = _features.FirstOrDefault(candidate => candidate.Definition.Id == AddGoldFeatureId);
        if (state?.Availability != TrainerAvailability.Available)
        {
            throw new InvalidOperationException("Add Gold is not available for the currently attached game build.");
        }

        var definition = TrainerCatalog.All.First(candidate => candidate.Id == AddGoldFeatureId);
        var amount = value ?? definition.DefaultValue ?? 10_000;
        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Gold amount must be positive.");
        }

        // Re-run the exact read-only profile immediately before every write. This
        // revalidates the GameCore hash, all AoB anchors and the local-player path.
        var snapshot = await _probe.ProbeAsync(session, cancellationToken);
        var treasury = ParseAddress(snapshot.TreasuryAddress);
        var goldAddress = treasury + GoldBalanceOffset;

        using var memory = new ProcessMemoryAccessor(session.ProcessId, allowWrite: true);
        var currentRaw = ReadInt32(memory, goldAddress, "gold balance");
        var deltaRaw = checked(amount * FixedPointScale);
        var nextRawLong = checked((long)currentRaw + deltaRaw);
        if (nextRawLong is < int.MinValue or > int.MaxValue)
        {
            throw new OverflowException("The requested Gold value exceeds Civilization VI's 32-bit fixed-point storage range.");
        }

        var nextRaw = (int)nextRawLong;
        memory.Write(goldAddress, BitConverter.GetBytes(nextRaw));
        var verifiedRaw = ReadInt32(memory, goldAddress, "gold balance verification");
        if (verifiedRaw != nextRaw)
        {
            throw new InvalidOperationException(
                $"Gold write verification failed: expected raw {nextRaw}, read back {verifiedRaw}.");
        }
    }

    public void Dispose()
    {
        Session = null;
    }

    private static IReadOnlyList<TrainerFeatureState> BuildStates(TrainerAvailability availability, string message) =>
        TrainerCatalog.All.Select(feature => new TrainerFeatureState(feature, availability, false, message)).ToArray();

    private static nint ParseAddress(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException("The verified Treasury address is missing.");
        }

        var text = value.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? value[2..] : value;
        if (!ulong.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var parsed)
            || parsed > (ulong)nint.MaxValue)
        {
            throw new InvalidOperationException($"Invalid Treasury address '{value}'.");
        }

        return (nint)parsed;
    }

    private static int ReadInt32(ProcessMemoryAccessor memory, nint address, string label)
    {
        var bytes = memory.Read(address, sizeof(int));
        if (bytes.Length != sizeof(int))
        {
            throw new InvalidOperationException($"Could not read the complete {label} value at 0x{address:X}.");
        }

        return BitConverter.ToInt32(bytes, 0);
    }
}
