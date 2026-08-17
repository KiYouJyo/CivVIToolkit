using System.Diagnostics;
using System.Globalization;
using CivVIToolkit.Core.Game;
using CivVIToolkit.Core.Trainer;
using CivVIToolkit.Platform.Windows.Memory;

namespace CivVIToolkit.Platform.Windows.Trainer;

/// <summary>
/// Exact-profile trainer for Steam / DX12 / Gathering Storm 1.0.12.68 (1023995).
/// Every feature is gated by the verified GameCore fingerprint and the probe's
/// runtime signature set. This engine is intentionally build-specific.
/// </summary>
public sealed class SteamDx12Build1023995TrainerEngine : ITrainerEngine
{
    private const long FixedPointScale = 256;
    private const int UnlimitedBalance = 1_000_000;
    private const int UnlimitedResourceAmount = 999;
    private const int UnlimitedMovesRaw = 100 * (int)FixedPointScale;
    private const int UnlimitedBuilderCharges = 99;
    private const int MaximumPopulation = 99;
    private const int AlmostDeadDamage = 99;
    private const int CompletionProgressRaw = 1_000_000 * (int)FixedPointScale;

    private const int GoldBalanceOffset = 0xA8;
    private const int FaithBalanceOffset = 0xB0;
    private const int InfluencePointsOffset = 0xB8;

    private const int PlayerUnitsPointerOffset = 0x6C8;
    private const int PlayerCitiesPointerOffset = 0x6D0;
    private const int PlayerCulturePointerOffset = 0x6F0;
    private const int PlayerResourcesPointerOffset = 0x700;
    private const int PlayerTechsPointerOffset = 0x718;
    private const int PlayerReligionPointerOffset = 0x720;
    private const int PlayerInfluencePointerOffset = 0x748;
    private const int PlayerTreasuryPointerOffset = 0x780;

    private const int UnitDamageOffset = 0x4C0;
    private const int UnitMovesOffset = 0x4F0;
    private const int UnitBuilderChargesOffset = 0x538;

    private const int CityPopulationOffset = 0x268;
    private const int CityBuildQueueWrapperOffset = 0x10C0;

    private const int TechCurrentQueuePointerOffset = 0xE0;
    private const int TechCurrentQueueCountOffset = 0xF0;

    private const int CultureCurrentQueuePointerOffset = 0x408;
    private const int CultureCurrentQueueCountOffset = 0x418;

    private const int ResourcesVectorWrapperOffset = 0x118;
    private const int ResourcesCountOffset = 0x138;

    private const int PlayerManagerPlayerArrayOffset = 0x20;
    private const int PlayerManagerStateArrayOffset = 0x2B48;
    private const int MaximumPlayerSlots = 65;

    // Chunked collections used by live Player::Units / Player::Cities.
    private const int UnitsChunkArrayStartOffset = 0x08;
    private const int UnitsChunkArrayEndOffset = 0x10;
    private const int CitiesChunkArrayStartOffset = 0x98;
    private const int CitiesChunkArrayEndOffset = 0xA0;
    private const int EntriesPerChunk = 8;
    private const int ChunkEntrySize = 24;
    private const int MaximumChunks = 4096;

    // BuildQueue layout for the current production item.
    private const int BuildQueueCurrentItemPointerOffset = 0x1D0;
    private const int BuildQueueCurrentItemCountOffset = 0x1E0;
    private const int BuildQueueUnitProgressArrayOffset = 0x218;
    private const int BuildQueueDistrictProgressArrayOffset = 0x230;
    private const int BuildQueueBuildingProgressArrayOffset = 0x248;
    private const int BuildQueueProjectProgressArrayOffset = 0x260;

    // Exact GameCore RVAs from the supplied SHA-256-locked build.
    private const int ResolveLazyWrapperRva = 0x72A920;
    private const int SetResearchProgressRva = 0x335C20;
    private const int SetCulturalProgressRva = 0x2844E0;
    private const int FinishProductionRva = 0x0FADA0;

    // Upgrade validation: preserve target/technology checks but relax the two
    // economic/location branches. Strategic resources are supplied by the same
    // toggle's local-resource enforcement rather than patching more code.
    private const int UpgradeGoldBranchRva = 0x3B7967;
    private const int UpgradeTerritoryBranchRva = 0x3B79D2;

    private static readonly HashSet<string> ToggleFeatureIds = TrainerCatalog.All
        .Where(feature => feature.Kind == TrainerFeatureKind.Toggle)
        .Select(feature => feature.Id)
        .ToHashSet(StringComparer.Ordinal);

    private static readonly HashSet<string> ActionFeatureIds = TrainerCatalog.All
        .Where(feature => feature.Kind == TrainerFeatureKind.ValueAction)
        .Select(feature => feature.Id)
        .ToHashSet(StringComparer.Ordinal);

    private readonly SteamDx12Build1023995Probe _probe;
    private readonly SemaphoreSlim _operationGate = new(1, 1);
    private readonly object _stateSync = new();
    private readonly HashSet<string> _enabled = new(StringComparer.Ordinal);
    private readonly Dictionary<string, AppliedPatch> _patches = new(StringComparer.Ordinal);

    private IReadOnlyList<TrainerFeatureState> _features = BuildStates(
        TrainerAvailability.NotAttached,
        "Start Civilization VI to attach.");
    private TrainerBuildProbeSnapshot? _snapshot;
    private CancellationTokenSource? _enforcementCts;
    private Task? _enforcementTask;
    private bool _disposed;

    public SteamDx12Build1023995TrainerEngine(SteamDx12Build1023995Probe probe)
    {
        _probe = probe ?? throw new ArgumentNullException(nameof(probe));
    }

    public GameSession? Session { get; private set; }

    public IReadOnlyList<TrainerFeatureState> Features
    {
        get
        {
            lock (_stateSync)
            {
                return _features;
            }
        }
    }

    public async Task AttachAsync(GameSession session, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(session);
        cancellationToken.ThrowIfCancellationRequested();

        _enforcementCts?.Cancel();
        await _operationGate.WaitAsync(cancellationToken);
        try
        {
            RestoreAllPatchesBestEffort();
            lock (_stateSync)
            {
                _enabled.Clear();
            }

            Session = session;
            _snapshot = null;

            if (!session.IsGatheringStormCoreLoaded)
            {
                SetAllStates(
                    TrainerAvailability.SignaturePending,
                    "Enter a Gathering Storm match to load the verified GameCore profile.");
                return;
            }

            try
            {
                _snapshot = await _probe.ProbeAsync(session, cancellationToken);
                RebuildAvailableStates("Verified exact Steam / DX12 / Gathering Storm 1.0.12.68 (1023995) profile.");
                StartEnforcementLoop();
            }
            catch (NotSupportedException exception)
            {
                SetAllStates(TrainerAvailability.UnsupportedGameVersion, exception.Message);
            }
            catch (Exception exception)
            {
                SetAllStates(TrainerAvailability.Error, exception.Message);
            }
        }
        finally
        {
            _operationGate.Release();
        }
    }

    public async Task DetachAsync(CancellationToken cancellationToken = default)
    {
        _enforcementCts?.Cancel();
        await _operationGate.WaitAsync(cancellationToken);
        try
        {
            RestoreAllPatchesBestEffort();
            lock (_stateSync)
            {
                _enabled.Clear();
            }

            _snapshot = null;
            Session = null;
            SetAllStates(TrainerAvailability.NotAttached, "Start Civilization VI to attach.");
        }
        finally
        {
            _operationGate.Release();
        }
    }

    public async Task SetEnabledAsync(string featureId, bool enabled, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(featureId);
        if (!ToggleFeatureIds.Contains(featureId))
        {
            throw new NotSupportedException($"Feature '{featureId}' is not a toggle.");
        }

        await _operationGate.WaitAsync(cancellationToken);
        try
        {
            var session = RequireAvailableSession(featureId);
            var snapshot = await _probe.ProbeAsync(session, cancellationToken);
            _snapshot = snapshot;

            if (enabled)
            {
                if (featureId == "unit.always-upgrade")
                {
                    ApplyAlwaysUpgradePatches(session);
                }

                lock (_stateSync)
                {
                    _enabled.Add(featureId);
                }

                await ApplyEnabledFeaturesAsync(session, snapshot, cancellationToken);
            }
            else
            {
                lock (_stateSync)
                {
                    _enabled.Remove(featureId);
                }

                if (featureId == "unit.always-upgrade")
                {
                    RestorePatch(session, "upgrade-gold");
                    RestorePatch(session, "upgrade-territory");
                }
            }

            RebuildAvailableStates(enabled ? "Enabled." : "Disabled.");
        }
        finally
        {
            _operationGate.Release();
        }
    }

    public async Task ExecuteAsync(string featureId, long? value = null, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(featureId);
        if (!ActionFeatureIds.Contains(featureId))
        {
            throw new NotSupportedException($"Feature '{featureId}' is not a value action.");
        }

        await _operationGate.WaitAsync(cancellationToken);
        try
        {
            var session = RequireAvailableSession(featureId);
            var snapshot = await _probe.ProbeAsync(session, cancellationToken);
            _snapshot = snapshot;
            var definition = TrainerCatalog.All.First(feature => feature.Id == featureId);
            var amount = value ?? definition.DefaultValue ?? throw new InvalidOperationException($"Feature '{featureId}' has no default value.");
            if (amount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "The requested amount must be positive.");
            }

            using var memory = new ProcessMemoryAccessor(session.ProcessId, allowWrite: true);
            switch (featureId)
            {
                case "player.add-gold":
                    AddFixedPoint(memory, ParseAddress(snapshot.TreasuryAddress), GoldBalanceOffset, amount, "gold");
                    break;
                case "player.add-influence":
                    AddFixedPoint(memory, ParseAddress(snapshot.InfluenceAddress), InfluencePointsOffset, amount, "influence");
                    break;
                default:
                    throw new NotSupportedException($"Feature '{featureId}' has no exact-profile action implementation.");
            }

            RebuildAvailableStates($"{definition.DisplayName} completed.");
        }
        finally
        {
            _operationGate.Release();
        }
    }

    private void StartEnforcementLoop()
    {
        _enforcementCts?.Cancel();
        _enforcementCts?.Dispose();
        _enforcementCts = new CancellationTokenSource();
        var token = _enforcementCts.Token;
        _enforcementTask = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(1), token);
                    if (token.IsCancellationRequested || !HasEnabledFeatures())
                    {
                        continue;
                    }

                    if (!await _operationGate.WaitAsync(0, token))
                    {
                        continue;
                    }

                    try
                    {
                        var session = Session;
                        var snapshot = _snapshot;
                        if (session is not null && snapshot is not null)
                        {
                            await ApplyEnabledFeaturesAsync(session, snapshot, token);
                        }
                    }
                    finally
                    {
                        _operationGate.Release();
                    }
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception exception)
                {
                    MarkEnabledFeaturesError(exception.Message);
                }
            }
        }, token);
    }

    private async Task ApplyEnabledFeaturesAsync(
        GameSession session,
        TrainerBuildProbeSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        var enabled = GetEnabledSnapshot();
        if (enabled.Count == 0)
        {
            return;
        }

        using var memory = new ProcessMemoryAccessor(session.ProcessId, allowWrite: true);
        using var invoker = new RemoteProcessInvoker(session.ProcessId);
        var moduleBase = GetGameCoreBase(session);
        var localPlayer = ParseAddress(snapshot.PlayerAddress);
        var players = EnumeratePlayers(memory, ParseAddress(snapshot.PlayerManagerAddress), snapshot.LocalPlayerId);

        if (enabled.Contains("player.unlimited-gold"))
        {
            SetPlayerBalance(memory, localPlayer, PlayerTreasuryPointerOffset, GoldBalanceOffset, UnlimitedBalance);
        }

        if (enabled.Contains("player.unlimited-faith"))
        {
            SetPlayerBalance(memory, localPlayer, PlayerReligionPointerOffset, FaithBalanceOffset, UnlimitedBalance);
        }

        if (enabled.Contains("player.instant-research"))
        {
            SetResearchProgress(memory, invoker, moduleBase, localPlayer, CompletionProgressRaw);
        }

        if (enabled.Contains("player.instant-civic"))
        {
            SetCivicProgress(memory, invoker, moduleBase, localPlayer, CompletionProgressRaw);
        }

        if (enabled.Contains("unit.unlimited-movement") || enabled.Contains("unit.unlimited-health") || enabled.Contains("unit.unlimited-builder-charges"))
        {
            foreach (var unit in EnumerateUnits(memory, localPlayer))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (enabled.Contains("unit.unlimited-movement")) WriteInt32(memory, unit + UnitMovesOffset, UnlimitedMovesRaw);
                if (enabled.Contains("unit.unlimited-health")) WriteInt32(memory, unit + UnitDamageOffset, 0);
                if (enabled.Contains("unit.unlimited-builder-charges")) WriteInt32(memory, unit + UnitBuilderChargesOffset, UnlimitedBuilderCharges);
            }
        }

        if (enabled.Contains("city.instant-production") || enabled.Contains("city.max-population"))
        {
            foreach (var city in EnumerateCities(memory, localPlayer))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (enabled.Contains("city.max-population"))
                {
                    WriteInt32(memory, city + CityPopulationOffset, MaximumPopulation);
                }

                if (enabled.Contains("city.instant-production"))
                {
                    var queue = ResolveBuildQueue(memory, invoker, moduleBase, city);
                    if (queue != 0 && ReadInt32OrDefault(memory, queue + BuildQueueCurrentItemCountOffset) > 0)
                    {
                        invoker.Invoke(moduleBase + FinishProductionRva, Address(queue));
                    }
                }
            }
        }

        if (enabled.Contains("player.unlimited-resources") || enabled.Contains("unit.always-upgrade"))
        {
            SetAllResources(memory, invoker, moduleBase, localPlayer, UnlimitedResourceAmount);
        }

        if (enabled.Contains("unit.always-upgrade"))
        {
            SetPlayerBalance(memory, localPlayer, PlayerTreasuryPointerOffset, GoldBalanceOffset, UnlimitedBalance);
        }

        foreach (var player in players.Where(player => !player.IsLocal))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (enabled.Contains("ai.zero-gold"))
            {
                SetPlayerBalance(memory, player.Address, PlayerTreasuryPointerOffset, GoldBalanceOffset, 0);
            }

            if (enabled.Contains("ai.zero-faith"))
            {
                SetPlayerBalance(memory, player.Address, PlayerReligionPointerOffset, FaithBalanceOffset, 0);
            }

            if (enabled.Contains("ai.zero-influence"))
            {
                SetPlayerBalance(memory, player.Address, PlayerInfluencePointerOffset, InfluencePointsOffset, 0);
            }

            if (enabled.Contains("ai.block-research"))
            {
                SetResearchProgress(memory, invoker, moduleBase, player.Address, 0);
            }

            if (enabled.Contains("ai.block-civic"))
            {
                SetCivicProgress(memory, invoker, moduleBase, player.Address, 0);
            }

            if (enabled.Contains("ai.block-movement") || enabled.Contains("combat.one-hit-kill"))
            {
                foreach (var unit in EnumerateUnits(memory, player.Address))
                {
                    if (enabled.Contains("ai.block-movement")) WriteInt32(memory, unit + UnitMovesOffset, 0);
                    if (enabled.Contains("combat.one-hit-kill")) WriteInt32(memory, unit + UnitDamageOffset, AlmostDeadDamage);
                }
            }

            if (enabled.Contains("ai.block-production"))
            {
                foreach (var city in EnumerateCities(memory, player.Address))
                {
                    var queue = ResolveBuildQueue(memory, invoker, moduleBase, city);
                    if (queue != 0)
                    {
                        ZeroCurrentProductionProgress(memory, queue);
                    }
                }
            }

            if (enabled.Contains("ai.zero-resources"))
            {
                SetAllResources(memory, invoker, moduleBase, player.Address, 0);
            }
        }

        await Task.CompletedTask;
    }

    private static void SetResearchProgress(
        ProcessMemoryAccessor memory,
        RemoteProcessInvoker invoker,
        nint moduleBase,
        nint player,
        int progressRaw)
    {
        var techs = ReadPointerOrZero(memory, player + PlayerTechsPointerOffset);
        if (techs == 0 || ReadInt32OrDefault(memory, techs + TechCurrentQueueCountOffset) <= 0)
        {
            return;
        }

        var queue = ReadPointerOrZero(memory, techs + TechCurrentQueuePointerOffset);
        if (queue == 0)
        {
            return;
        }

        var techId = ReadInt32OrDefault(memory, queue, -1);
        if (techId < 0)
        {
            return;
        }

        invoker.Invoke(
            moduleBase + SetResearchProgressRva,
            Address(techs),
            unchecked((uint)techId),
            0,
            0,
            progressRaw,
            pointerArgumentIndex: 3);
    }

    private static void SetCivicProgress(
        ProcessMemoryAccessor memory,
        RemoteProcessInvoker invoker,
        nint moduleBase,
        nint player,
        int progressRaw)
    {
        var culture = ReadPointerOrZero(memory, player + PlayerCulturePointerOffset);
        if (culture == 0 || ReadInt32OrDefault(memory, culture + CultureCurrentQueueCountOffset) <= 0)
        {
            return;
        }

        var queue = ReadPointerOrZero(memory, culture + CultureCurrentQueuePointerOffset);
        if (queue == 0)
        {
            return;
        }

        var civicId = ReadInt32OrDefault(memory, queue, -1);
        if (civicId < 0)
        {
            return;
        }

        invoker.Invoke(
            moduleBase + SetCulturalProgressRva,
            Address(culture),
            unchecked((uint)civicId),
            0,
            0,
            progressRaw,
            pointerArgumentIndex: 3);
    }

    private static void SetAllResources(
        ProcessMemoryAccessor memory,
        RemoteProcessInvoker invoker,
        nint moduleBase,
        nint player,
        int amount)
    {
        var resources = ReadPointerOrZero(memory, player + PlayerResourcesPointerOffset);
        if (resources == 0)
        {
            return;
        }

        var count = ReadInt32OrDefault(memory, resources + ResourcesCountOffset);
        if (count is <= 0 or > 4096)
        {
            return;
        }

        // GameCore+0x72A920 is the exact lazy-wrapper resolver used by the
        // resource routines themselves. It returns a pointer to the vector slot.
        var slot = ToAddress(invoker.Invoke(moduleBase + ResolveLazyWrapperRva, Address(resources + ResourcesVectorWrapperOffset)));
        if (slot == 0)
        {
            return;
        }

        var table = ReadPointerOrZero(memory, slot);
        if (table == 0)
        {
            return;
        }

        for (var resourceId = 0; resourceId < count; resourceId++)
        {
            // Three category pointers per resource. Category zero is the live
            // stockpile used by the game's own unit-upgrade validation path.
            var entry = ReadPointerOrZero(memory, table + (resourceId * 3 * IntPtr.Size));
            if (entry != 0)
            {
                WriteInt32(memory, entry, amount);
            }
        }
    }

    private static nint ResolveBuildQueue(
        ProcessMemoryAccessor memory,
        RemoteProcessInvoker invoker,
        nint moduleBase,
        nint city)
    {
        var slot = ToAddress(invoker.Invoke(moduleBase + ResolveLazyWrapperRva, Address(city + CityBuildQueueWrapperOffset)));
        return slot == 0 ? 0 : ReadPointerOrZero(memory, slot);
    }

    private static void ZeroCurrentProductionProgress(ProcessMemoryAccessor memory, nint queue)
    {
        if (ReadInt32OrDefault(memory, queue + BuildQueueCurrentItemCountOffset) <= 0)
        {
            return;
        }

        var item = ReadPointerOrZero(memory, queue + BuildQueueCurrentItemPointerOffset);
        if (item == 0)
        {
            return;
        }

        var type = ReadInt32OrDefault(memory, item + 0x30, -1);
        var (idOffset, arrayOffset) = type switch
        {
            0 => (0x10, BuildQueueDistrictProgressArrayOffset),
            1 => (0x14, BuildQueueUnitProgressArrayOffset),
            2 => (0x18, BuildQueueBuildingProgressArrayOffset),
            3 => (0x1C, BuildQueueProjectProgressArrayOffset),
            _ => (-1, -1),
        };
        if (idOffset < 0)
        {
            return;
        }

        var itemId = ReadInt32OrDefault(memory, item + idOffset, -1);
        var progressArray = ReadPointerOrZero(memory, queue + arrayOffset);
        if (itemId >= 0 && progressArray != 0)
        {
            WriteInt32(memory, progressArray + (itemId * sizeof(int)), 0);
        }
    }

    private static IEnumerable<nint> EnumerateUnits(ProcessMemoryAccessor memory, nint player)
    {
        var collection = ReadPointerOrZero(memory, player + PlayerUnitsPointerOffset);
        return EnumerateChunkedCollection(memory, collection, UnitsChunkArrayStartOffset, UnitsChunkArrayEndOffset);
    }

    private static IEnumerable<nint> EnumerateCities(ProcessMemoryAccessor memory, nint player)
    {
        var collection = ReadPointerOrZero(memory, player + PlayerCitiesPointerOffset);
        return EnumerateChunkedCollection(memory, collection, CitiesChunkArrayStartOffset, CitiesChunkArrayEndOffset);
    }

    private static IEnumerable<nint> EnumerateChunkedCollection(
        ProcessMemoryAccessor memory,
        nint collection,
        int startOffset,
        int endOffset)
    {
        if (collection == 0)
        {
            yield break;
        }

        var start = ReadPointerOrZero(memory, collection + startOffset);
        var end = ReadPointerOrZero(memory, collection + endOffset);
        if (start == 0 || end <= start)
        {
            yield break;
        }

        var bytes = (long)(end - start);
        if (bytes <= 0 || bytes % IntPtr.Size != 0)
        {
            yield break;
        }

        var chunkCount = Math.Min(bytes / IntPtr.Size, MaximumChunks);
        for (var chunkIndex = 0L; chunkIndex < chunkCount; chunkIndex++)
        {
            var chunk = ReadPointerOrZero(memory, start + checked((int)(chunkIndex * IntPtr.Size)));
            if (chunk == 0)
            {
                continue;
            }

            for (var entryIndex = 0; entryIndex < EntriesPerChunk; entryIndex++)
            {
                var instance = ReadPointerOrZero(memory, chunk + (entryIndex * ChunkEntrySize));
                if (instance != 0)
                {
                    yield return instance;
                }
            }
        }
    }

    private static IReadOnlyList<PlayerRuntime> EnumeratePlayers(
        ProcessMemoryAccessor memory,
        nint manager,
        int localPlayerId)
    {
        var result = new List<PlayerRuntime>();
        var states = ReadPointerOrZero(memory, manager + PlayerManagerStateArrayOffset);
        var players = ReadPointerOrZero(memory, manager + PlayerManagerPlayerArrayOffset);
        if (states == 0 || players == 0)
        {
            return result;
        }

        for (var playerId = 0; playerId < MaximumPlayerSlots; playerId++)
        {
            var state = ReadInt32OrDefault(memory, states + (playerId * sizeof(int)), -1);
            if (state == -1)
            {
                continue;
            }

            var player = ReadPointerOrZero(memory, players + (playerId * IntPtr.Size));
            if (player != 0)
            {
                result.Add(new PlayerRuntime(playerId, player, playerId == localPlayerId));
            }
        }

        return result;
    }

    private void ApplyAlwaysUpgradePatches(GameSession session)
    {
        var moduleBase = GetGameCoreBase(session);
        using var memory = new ProcessMemoryAccessor(session.ProcessId, allowWrite: true);
        ApplyPatch(memory, "upgrade-gold", moduleBase + UpgradeGoldBranchRva, [0x7E], [0xEB]);
        ApplyPatch(memory, "upgrade-territory", moduleBase + UpgradeTerritoryBranchRva, [0x74], [0xEB]);
    }

    private void ApplyPatch(
        ProcessMemoryAccessor memory,
        string id,
        nint address,
        byte[] expected,
        byte[] patched)
    {
        if (_patches.ContainsKey(id))
        {
            return;
        }

        var current = memory.Read(address, expected.Length);
        if (!current.SequenceEqual(expected))
        {
            throw new InvalidOperationException(
                $"Patch '{id}' original bytes did not match at 0x{address:X}; refusing to patch this build.");
        }

        memory.WriteProtected(address, patched);
        var verified = memory.Read(address, patched.Length);
        if (!verified.SequenceEqual(patched))
        {
            throw new InvalidOperationException($"Patch '{id}' verification failed at 0x{address:X}.");
        }

        _patches[id] = new AppliedPatch(address, expected, patched);
    }

    private void RestorePatch(GameSession session, string id)
    {
        if (!_patches.Remove(id, out var patch))
        {
            return;
        }

        using var memory = new ProcessMemoryAccessor(session.ProcessId, allowWrite: true);
        var current = memory.Read(patch.Address, patch.Patched.Length);
        if (current.SequenceEqual(patch.Patched))
        {
            memory.WriteProtected(patch.Address, patch.Original);
        }
    }

    private void RestoreAllPatchesBestEffort()
    {
        var session = Session;
        if (session is null || _patches.Count == 0)
        {
            _patches.Clear();
            return;
        }

        try
        {
            using var memory = new ProcessMemoryAccessor(session.ProcessId, allowWrite: true);
            foreach (var patch in _patches.Values)
            {
                try
                {
                    var current = memory.Read(patch.Address, patch.Patched.Length);
                    if (current.SequenceEqual(patch.Patched))
                    {
                        memory.WriteProtected(patch.Address, patch.Original);
                    }
                }
                catch
                {
                    // Best-effort cleanup during detach/process exit.
                }
            }
        }
        catch
        {
            // The game may already be gone; there is nothing left to restore.
        }
        finally
        {
            _patches.Clear();
        }
    }

    private GameSession RequireAvailableSession(string featureId)
    {
        var session = Session ?? throw new InvalidOperationException("Civilization VI is not attached.");
        var state = Features.FirstOrDefault(candidate => candidate.Definition.Id == featureId);
        if (state?.Availability != TrainerAvailability.Available)
        {
            throw new InvalidOperationException($"Feature '{featureId}' is not available for the currently attached build.");
        }

        return session;
    }

    private static void SetPlayerBalance(
        ProcessMemoryAccessor memory,
        nint player,
        int componentPointerOffset,
        int balanceOffset,
        int value)
    {
        var component = ReadPointerOrZero(memory, player + componentPointerOffset);
        if (component != 0)
        {
            WriteInt32(memory, component + balanceOffset, checked(value * (int)FixedPointScale));
        }
    }

    private static void AddFixedPoint(
        ProcessMemoryAccessor memory,
        nint component,
        int balanceOffset,
        long amount,
        string label)
    {
        var address = component + balanceOffset;
        var currentRaw = ReadInt32Exact(memory, address, label);
        var deltaRaw = checked(amount * FixedPointScale);
        var nextRawLong = checked((long)currentRaw + deltaRaw);
        if (nextRawLong is < int.MinValue or > int.MaxValue)
        {
            throw new OverflowException($"The requested {label} value exceeds Civilization VI's 32-bit fixed-point storage range.");
        }

        var nextRaw = (int)nextRawLong;
        WriteInt32(memory, address, nextRaw);
        var verifiedRaw = ReadInt32Exact(memory, address, $"{label} verification");
        if (verifiedRaw != nextRaw)
        {
            throw new InvalidOperationException($"{label} write verification failed: expected raw {nextRaw}, read back {verifiedRaw}.");
        }
    }

    private static nint GetGameCoreBase(GameSession session)
    {
        using var process = Process.GetProcessById(session.ProcessId);
        var module = process.Modules.Cast<ProcessModule>().FirstOrDefault(candidate =>
            string.Equals(candidate.FileName, session.GameCoreModulePath, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("The verified Gathering Storm GameCore module is no longer loaded.");
        return module.BaseAddress;
    }

    private static nint ParseAddress(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException("A verified runtime address is missing.");
        }

        var text = value.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? value[2..] : value;
        if (!ulong.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var parsed)
            || parsed > (ulong)nint.MaxValue)
        {
            throw new InvalidOperationException($"Invalid runtime address '{value}'.");
        }

        return (nint)parsed;
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

    private static int ReadInt32OrDefault(ProcessMemoryAccessor memory, nint address, int fallback = 0)
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

    private static int ReadInt32Exact(ProcessMemoryAccessor memory, nint address, string label)
    {
        var bytes = memory.Read(address, sizeof(int));
        if (bytes.Length != sizeof(int))
        {
            throw new InvalidOperationException($"Could not read the complete {label} value at 0x{address:X}.");
        }

        return BitConverter.ToInt32(bytes, 0);
    }

    private static void WriteInt32(ProcessMemoryAccessor memory, nint address, int value) =>
        memory.Write(address, BitConverter.GetBytes(value));

    private static ulong Address(nint address) => unchecked((ulong)(nuint)address);

    private static nint ToAddress(ulong address) =>
        address == 0 || address > (ulong)nint.MaxValue ? 0 : (nint)address;

    private bool HasEnabledFeatures()
    {
        lock (_stateSync)
        {
            return _enabled.Count > 0;
        }
    }

    private HashSet<string> GetEnabledSnapshot()
    {
        lock (_stateSync)
        {
            return new HashSet<string>(_enabled, StringComparer.Ordinal);
        }
    }

    private void RebuildAvailableStates(string message)
    {
        lock (_stateSync)
        {
            _features = TrainerCatalog.All.Select(feature =>
            {
                var enabled = _enabled.Contains(feature.Id);
                var status = feature.Id switch
                {
                    "unit.always-upgrade" => "Exact-build upgrade checks patched; Gold/resources are also enforced while enabled.",
                    "combat.one-hit-kill" => "Acceptance implementation keeps non-local units at 99 damage.",
                    "ai.block-production" => "Acceptance implementation resets the active AI production item's progress.",
                    _ => message,
                };
                return new TrainerFeatureState(feature, TrainerAvailability.Available, enabled, status);
            }).ToArray();
        }
    }

    private void MarkEnabledFeaturesError(string message)
    {
        lock (_stateSync)
        {
            _features = TrainerCatalog.All.Select(feature =>
            {
                var enabled = _enabled.Contains(feature.Id);
                return enabled
                    ? new TrainerFeatureState(feature, TrainerAvailability.Error, true, message)
                    : new TrainerFeatureState(feature, TrainerAvailability.Available, false, "Verified exact build; not enabled.");
            }).ToArray();
        }
    }

    private void SetAllStates(TrainerAvailability availability, string message)
    {
        lock (_stateSync)
        {
            _features = BuildStates(availability, message);
        }
    }

    private static IReadOnlyList<TrainerFeatureState> BuildStates(TrainerAvailability availability, string message) =>
        TrainerCatalog.All.Select(feature => new TrainerFeatureState(feature, availability, false, message)).ToArray();

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _enforcementCts?.Cancel();
        try
        {
            if (_operationGate.Wait(TimeSpan.FromSeconds(2)))
            {
                try
                {
                    RestoreAllPatchesBestEffort();
                }
                finally
                {
                    _operationGate.Release();
                }
            }
        }
        catch
        {
            // Best-effort shutdown cleanup.
        }

        _enforcementCts?.Dispose();
        _operationGate.Dispose();
        Session = null;
    }

    private sealed record PlayerRuntime(int Id, nint Address, bool IsLocal);
    private sealed record AppliedPatch(nint Address, byte[] Original, byte[] Patched);
}
