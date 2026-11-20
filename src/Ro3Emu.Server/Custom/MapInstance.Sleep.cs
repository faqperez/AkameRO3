using Ro3Emu.Server.Game;

namespace Ro3Emu.Server.Maps;

public sealed partial class MapInstance
{
    private static readonly TimeSpan IdleMaintenanceInterval = TimeSpan.FromSeconds(1);
    private readonly List<WorldUnit> _tickSnapshot = [];
    private TimeSpan _nextIdleTick;
    private bool _hasActiveUnits = true;

    /// <summary>Wakes the map instance to tick on the next game-loop turn.</summary>
    public void Wake()
    {
        _hasActiveUnits = true;
        _nextIdleTick = TimeSpan.Zero;
    }

    private void TickUnits(GameTime time)
    {
        if (_units.Count == 0) return;
        if (world.HasPlayers(sceneEntry))
        {
            _hasActiveUnits = true;
            _nextIdleTick = time.Now + IdleMaintenanceInterval;
            RunUnitTicks(time, activeOnly: false);
            return;
        }
        if (!_hasActiveUnits && time.Now < _nextIdleTick) return;
        _nextIdleTick = time.Now + IdleMaintenanceInterval;
        _hasActiveUnits = HasActiveUnit();
        if (_hasActiveUnits) RunUnitTicks(time, activeOnly: true);
    }

    private void RunUnitTicks(GameTime time, bool activeOnly)
    {
        _tickSnapshot.Clear();
        _tickSnapshot.AddRange(_units);
        foreach (var unit in _tickSnapshot)
        {
            if (!activeOnly || IsUnitActive(unit)) unit.Tick(this, time);
        }
        _tickSnapshot.Clear();
    }

    private bool HasActiveUnit()
    {
        foreach (var unit in _units)
            if (IsUnitActive(unit)) return true;
        return false;
    }

    private static bool IsUnitActive(WorldUnit unit) => unit switch
    {
        MonsterUnit m => !m.Dead && m.Hp > 0 && (m.Moving || m.Returning || m.Target != null
            || m.PetTarget != null || m.March != null || m.SlowMovementExpireMs > 0),
        GatheringUnit => false,
        _ => true,
    };
}
