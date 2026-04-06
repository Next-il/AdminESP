using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Utils;

namespace AdminESP;

public sealed partial class AdminESP : BasePlugin, IPluginConfig<Config>
{
    public override string ModuleName => "Next-il | Admin ESP";
    public override string ModuleAuthor => "ShiNxz";
    public override string ModuleVersion => "1.0"; 
    public override string ModuleDescription => "Plugin uses code borrowed from CS2Fixes / cs2kz-metamod / hl2sdk / unknown cheats and xstage from CS# discord";

    public bool[] toggleAdminESP = new bool[64]; 
    public bool[] wantESP = new bool[64]; 
    public bool togglePlayersGlowing = false;
    public Config Config { get; set; } = new();
    private static readonly ConVar? _forceCamera = ConVar.Find("mp_forcecamera");

    public override void Load(bool hotReload)
    {
        RegisterListeners();

        if (hotReload) {
      
            foreach (var player in Utilities.GetPlayers().Where(p => p is not null 
                                                                && p.IsValid is true
                                                                && p.Connected is PlayerConnectedState.PlayerConnected)) {

                if (cachedPlayers.Contains(player) is not true)
                    cachedPlayers.Add(player);

            }
        }

    }

    public override void Unload(bool hotReload)
    {
        DeregisterListeners();
    }

    private void ApplyESPToggle(CCSPlayerController player, bool enabled)
    {
        toggleAdminESP[player.Slot] = enabled;

        if (enabled) {
            if (togglePlayersGlowing is not true || AreThereEsperingAdmins() is not true)
                SetAllPlayersGlowing();
        }
        else {
            if (togglePlayersGlowing is not true || AreThereEsperingAdmins() is not true)
                RemoveAllGlowingPlayers();
        }
    }

    [ConsoleCommand("css_esp", "Toggle Admin ESP")]
    [CommandHelper(minArgs: 0, whoCanExecute: CommandUsage.CLIENT_ONLY)]
    public void OnTogglePersistentAdminEsp(CCSPlayerController? player, CommandInfo command)
    {
        if (player is null || player.IsValid is not true) return;

        if (AdminManager.PlayerHasPermissions(player, Config.AdminFlag) is not true) {
            SendMessageToSpecificChat(player, msg: "Admin ESP can only be used from {GREEN}admins{DEFAULT}!", print: PrintTo.Chat);
            return;  
        }

        if (player.Team is not CsTeam.Spectator && player.Team is not CsTeam.None) {
            SendMessageToSpecificChat(player, msg: "Admin ESP is only allowed in {RED}spectate mode{DEFAULT}!", print: PrintTo.Chat);
            return;
        }

        wantESP[player.Slot] = !wantESP[player.Slot];
        ApplyESPToggle(player, wantESP[player.Slot]);
        SendMessageToSpecificChat(player, msg: $"Admin ESP has been " + (wantESP[player.Slot] ? "{GREEN}enabled!" : "{RED}disabled!"), print: PrintTo.Chat);
    }

    public void OnConfigParsed(Config config)
    {
        Config = config;
    }

}
