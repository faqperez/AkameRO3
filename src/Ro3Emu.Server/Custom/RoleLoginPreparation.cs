using Romsg;
using Ro3Emu.Server.Accounts;

namespace Ro3Emu.Server.Game;

public sealed partial class RoleSessionService
{
    private sealed record PreparedRoleLogin(
        Role SourceRole,
        Maps.MapPoint Arrival,
        long Speed,
        RoleBaseInfo BaseInfo,
        DBSettingInfo Settings,
        bool SettingsRepaired,
        PreparedMessage SettingsMessage);

    private Task<PreparedRoleLogin> PrepareRoleOffLoopAsync(Role role, string? clientAccount)
    {
        byte[]? settingsBytes = roles.LoadSettings(role.Id);
        return Task.Run(() =>
        {
            var arrival = scene.LoginPoint(role.SceneEntry, new Vec3 { X = role.X, Y = role.Y, Z = role.Z }, role.Face);
            long speed = data.JobAttributes(role.Career).GetValueOrDefault(AttrSpeed, DefaultSpeed);
            var baseInfo = RoleProto.BaseInfo(role, clientAccount);
            var parsedSettings = settingsBytes is { } bytes ? DBSettingInfo.Parser.ParseFrom(bytes) : new DBSettingInfo();
            parsedSettings.WildAFKInfo ??= new SettingWildAFKInfo();
            bool repaired = RepairClassicMovement(parsedSettings);
            var settingsMessage = new PreparedMessage(MsgId.AutoSyncSettings, SettingsSync(parsedSettings));
            return new PreparedRoleLogin(role, arrival, speed, baseInfo, parsedSettings, repaired, settingsMessage);
        });
    }

    private static bool SamePlacement(Role a, Role b) =>
        a.SceneEntry == b.SceneEntry && a.X == b.X && a.Y == b.Y && a.Z == b.Z && a.Face == b.Face;

    private static bool SameIdentity(Role a, Role b) =>
        a.Id == b.Id && a.Name == b.Name && a.Career == b.Career && a.Level == b.Level && a.Sex == b.Sex && a.Hair == b.Hair;
}
