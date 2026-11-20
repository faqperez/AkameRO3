using Romsg;

namespace Ro3Emu.Server.Game;

public sealed partial class AchievementService
{
    private static readonly TimeSpan BackgroundSweepInterval = TimeSpan.FromSeconds(1);
    private const int BackgroundSweepSlices = 20;
    private const int MaxProbePerTick = 8;
    private TimeSpan _lastSweepTick;
    private HashSet<long>? _evalAppearances;
    private long? _evalFashionLevel;

    private EvalScope BeginEvalPass(GameSession s)
    {
        _evalAppearances = OwnedAppearancesCore(s);
        _evalFashionLevel = s.Wardrobe == null ? null : FashionLevelCore(WardrobeService.FashionScore(_data, s.Wardrobe));
        return new EvalScope(this);
    }

    private void EndEvalPass()
    {
        _evalAppearances = null;
        _evalFashionLevel = null;
    }

    private readonly struct EvalScope(AchievementService owner) : IDisposable
    {
        public void Dispose() => owner.EndEvalPass();
    }

    private long FashionLevelCore(long score) =>
        Rows().FashionLevels.Where(l => l.Score <= score).Select(l => l.Level).DefaultIfEmpty(0).Max();

    private void RefreshLevelsFast(DBRoleAchievementInfo state)
    {
        var catalog = Rows();
        long total = 0;
        var byType = new Dictionary<int, long>();
        foreach (var (id, info) in state.Infos)
        {
            if (!Reached(info) || !catalog.Rows.TryGetValue(id, out var row)) continue;
            total += row.Point;
            byType[row.Type] = byType.GetValueOrDefault(row.Type) + row.Point;
        }
        foreach (var (type, tiers) in catalog.Tiers)
        {
            long pts = type == 0 ? total : byType.GetValueOrDefault(type);
            var info = state.AchievementLevelInfo.TryGetValue(type, out var old) ? old.Clone() : new AchievementLevelInfo { Type = type };
            info.Type = type;
            info.Point = pts;
            info.Level = LevelOf(pts, tiers);
            if (!info.Equals(old)) state.AchievementLevelInfo[type] = info;
        }
    }

    private void TickOptimized(GameTime time)
    {
        if (time.Now - _lastTick < ProbeEvery) return;
        _lastTick = time.Now;
        var all = _sessions.All();
        if (_dirty.Count == 0 && all.Count == 0) return;
        bool runSweep = all.Count > 0 && (time.Now - _lastSweepTick >= BackgroundSweepInterval);
        if (_dirty.Count == 0 && !runSweep) return;
        var probe = new HashSet<GameSession>();
        foreach (var s in _dirty)
        {
            probe.Add(s);
            if (probe.Count >= MaxProbePerTick) break;
        }
        foreach (var s in probe) _dirty.Remove(s);
        if (runSweep)
        {
            _lastSweepTick = time.Now;
            int slice = Math.Min(MaxProbePerTick, (all.Count + BackgroundSweepSlices - 1) / BackgroundSweepSlices);
            for (int i = 0; i < slice && probe.Count < MaxProbePerTick; i++)
                probe.Add(all[(_sweepIndex + i) % all.Count]);
            _sweepIndex = (_sweepIndex + slice) % all.Count;
        }
        if (probe.Count == 0) return;
        long day = DayBoundary();
        foreach (var s in probe)
        {
            if (!Ready(s) || _recheckQueued.Contains(s)) continue;
            if (s.Handoff != null || s.Owner?.State == Ownership.RoleOwnerState.Loading) { _dirty.Add(s); continue; }
            if (s.AchievementRuntime!.LastLoginDay < day) QueueLoginDay(s);
            else if (SnapshotImproves(s)) PostRecheck(s);
        }
    }
}
