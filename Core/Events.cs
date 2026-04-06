using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Entities;
using CounterStrikeSharp.API.Modules.Utils;
namespace AdminESP;

public partial class AdminESP
{

	private void RegisterListeners()
	{
		RegisterListener<Listeners.OnClientAuthorized>(OnClientAuthorized);
		RegisterListener<Listeners.OnClientConnected>(OnClientConnected);
		RegisterListener<Listeners.OnClientPutInServer>(OnClientPutInServer);
		RegisterListener<Listeners.OnClientDisconnect>(OnClientDisconnected);
		RegisterListener<Listeners.CheckTransmit>(CheckTransmitListener);
		//register event listeners
		RegisterEventHandler<EventPlayerSpawn>(OnPlayerSpawn, HookMode.Pre);
		RegisterEventHandler<EventPlayerDeath>(OnPlayerDeath);
		RegisterEventHandler<EventRoundStart>(OnRoundStart);
		RegisterEventHandler<EventPlayerTeam>(OnPlayerTeam);

	}
	private void DeregisterListeners()
	{
		RemoveListener<Listeners.OnClientAuthorized>(OnClientAuthorized);
		RemoveListener<Listeners.OnClientConnected>(OnClientConnected);
		RemoveListener<Listeners.OnClientPutInServer>(OnClientPutInServer);
		RemoveListener<Listeners.OnClientDisconnect>(OnClientDisconnected);
		RemoveListener<Listeners.CheckTransmit>(CheckTransmitListener);
		//deregister event listeners
		DeregisterEventHandler<EventPlayerSpawn>(OnPlayerSpawn, HookMode.Pre);
		DeregisterEventHandler<EventPlayerDeath>(OnPlayerDeath);
		DeregisterEventHandler<EventRoundStart>(OnRoundStart);
		DeregisterEventHandler<EventPlayerTeam>(OnPlayerTeam);
	}
	private void OnClientAuthorized(int slot, SteamID steamid)
	{
		var player = Utilities.GetPlayerFromSlot(slot);
		if (player == null || player.IsValid is not true) return;
		if (cachedPlayers.Contains(player) is not true)
			cachedPlayers.Add(player);

	}
	private void OnClientConnected(int slot)
	{
		var player = Utilities.GetPlayerFromSlot(slot);
		if (player == null || player.IsValid is not true) return;
		if (cachedPlayers.Contains(player) is not true)
			cachedPlayers.Add(player);

	}
	private void OnClientPutInServer(int slot)
	{
		var player = Utilities.GetPlayerFromSlot(slot);
		if (player is null || player.IsBot is not true) return;
		if (cachedPlayers.Contains(player) is not true)
			cachedPlayers.Add(player);

	}
	// Core/Events.cs - MODIFICAT pentru persistent ESP la moarte/spectator si rezolvarea problema ESP la bot takeover
	// Adaugă logica de dezactivare instant în CheckTransmitListener (apelat frecvent)
	private void CheckTransmitListener(CCheckTransmitInfoList infoList)
	{
		foreach ((CCheckTransmitInfo info, CCSPlayerController? player) in infoList)
		{
			if (player is null || player.IsValid is not true) continue;
			// NOU: Verifică și dezactivează ESP instant dacă player-ul este alive (inclusiv bot takeover)
			if (player.PawnIsAlive && toggleAdminESP[player.Slot])
			{
				toggleAdminESP[player.Slot] = false;
				if (togglePlayersGlowing is not true || AreThereEsperingAdmins() is not true)
				{
					RemoveAllGlowingPlayers();
				}
				if (wantESP[player.Slot])
				{
					SendMessageToSpecificChat(player, msg: "Admin ESP has been {RED}disabled{DEFAULT} because you are now alive (bot takeover)!", print: PrintTo.Chat);
				}
			}
			//itereate cached players
			for (int i = 0; i < cachedPlayers.Count(); i++)
			{

				//leave self's observerPawn so it can spectate and check if feature is enabled
				//we are clearing the whole spectator list as it doesn't work relaibly per person basis
				if (Config.HideAdminSpectators is true)
				{
					if (cachedPlayers[i] is null || cachedPlayers[i].IsValid is not true) continue;
					//check if it 'us' in the current context and do the magic only if it's not
					if (cachedPlayers[i].Slot != player.Slot)
					{
						//get the target's pawn
						var targetPawn = cachedPlayers[i].PlayerPawn.Value;
						if (targetPawn is null || targetPawn.IsValid is not true) continue;
						//get the target's observerpawn
						var targetObserverPawn = cachedPlayers[i].ObserverPawn.Value;
						if (targetObserverPawn is null
						|| targetObserverPawn.IsValid is not true) continue;
						//we clear the spec list via clearing all of the observerTarget' pawns indexes
						//from the Observer_services class that any cheat uses as a method to campare
						//against current players in the server
						info.TransmitEntities.Remove((int)targetObserverPawn.Index);
					}
				}
				//check if admin has enabled ESP
				if (toggleAdminESP[player.Slot] == true)
					continue;

				//stop transmitting any entity from the glowingPlayers list
				foreach (var glowingProp in glowingPlayers)
				{
					if (glowingProp.Value.Item1 is not null && glowingProp.Value.Item1.IsValid is true
					&& glowingProp.Value.Item2 is not null && glowingProp.Value.Item2.IsValid is true)
					{
						//prop one
						info.TransmitEntities.Remove((int)glowingProp.Value.Item1.Index);
						//prop two
						info.TransmitEntities.Remove((int)glowingProp.Value.Item2.Index);
					}
				}
			}
		}

	}

	public HookResult OnPlayerSpawn(EventPlayerSpawn @event, GameEventInfo info)
	{
		var player = @event.Userid;
		if (player is null
		|| player.IsValid is not true
		|| player.Connected is not PlayerConnectedState.PlayerConnected) return HookResult.Continue;
		// Dezactivează imediat (fără delay)
		toggleAdminESP[player.Slot] = false;
		if (togglePlayersGlowing is not true || AreThereEsperingAdmins() is not true)
		{
			RemoveAllGlowingPlayers();
		}
		
		if (wantESP[player.Slot])
		{
			SendMessageToSpecificChat(player, msg: "Admin ESP has been {RED}disabled{DEFAULT} because you spawned!", print: PrintTo.Chat);
			// Remove him from wantESP list as well, to avoid any potential issues with lingering ESP on next spectate (optional, depending on whether you want it to persist or not)
			wantESP[player.Slot] = false;
		}
		return HookResult.Continue;
	}

	public HookResult OnRoundStart(EventRoundStart @event, GameEventInfo info)
	{
		for (int i = 0; i < cachedPlayers.Count(); i++)
		{
			if (cachedPlayers[i] is null || cachedPlayers[i].IsValid is not true) continue;
			if (toggleAdminESP[cachedPlayers[i].Slot] is true && (cachedPlayers[i].Team is CsTeam.Spectator || cachedPlayers[i].Team is CsTeam.None) && Config.SkipSpectatingEsps is true)
				continue;

			toggleAdminESP[cachedPlayers[i].Slot] = false;
		}
		if (togglePlayersGlowing is true)
			togglePlayersGlowing = false;
		//check if there are espering admins and restore their ESP if they wanted it
		Server.NextFrame(() =>
		{
			// Re-enable ESP for admins who had wantESP on and are spectating
			for (int i = 0; i < cachedPlayers.Count(); i++)
			{
				if (cachedPlayers[i] is null || cachedPlayers[i].IsValid is not true) continue;
				var p = cachedPlayers[i];
				if (!wantESP[p.Slot]) continue;

				if (p.Team is CsTeam.Spectator || p.Team is CsTeam.None)
					toggleAdminESP[p.Slot] = true;
			}

			//remove props if there isn't any espering admin/s
			if (AreThereEsperingAdmins() is false)
			{
				RemoveAllGlowingPlayers();
				return;
			}
			//respawn the glowing props for all espering admins
			SetAllPlayersGlowing();
		});
		return HookResult.Continue;
	}
	public HookResult OnPlayerDeath(EventPlayerDeath @event, GameEventInfo info)
	{
		var player = @event.Userid;
		if (player is null
		|| player.IsValid is not true
		|| player.Connected is not PlayerConnectedState.PlayerConnected) return HookResult.Continue;
		//remove glowing prop if player has one upon death
		if (glowingPlayers.ContainsKey(player.Slot) is true)
		{
			if (glowingPlayers[player.Slot].Item1 is not null && glowingPlayers[player.Slot].Item1.IsValid is true
			&& glowingPlayers[player.Slot].Item2 is not null && glowingPlayers[player.Slot].Item2.IsValid is true)
			{

				//remove previous modelRelay prop
				glowingPlayers[player.Slot].Item1.AcceptInput("Kill");
				//remove previous modelGlow prop
				glowingPlayers[player.Slot].Item2.AcceptInput("Kill");
			}
			//remove player from the list
			glowingPlayers.Remove(player.Slot);
		}
		// NOU: Activează ESP automat dacă persistent este on și condițiile sunt îndeplinite - cu delay mai mare pentru stabilizare
		AddTimer(0.5f, () =>
		{
			if (player is null || player.IsValid is not true || player.PawnIsAlive) return;
			if (wantESP[player.Slot] && (player.Team == CsTeam.Spectator || player.Team == CsTeam.None))
			{
				toggleAdminESP[player.Slot] = true;
				togglePlayersGlowing = true;
				SetAllPlayersGlowing();
				SendMessageToSpecificChat(player, msg: "Admin ESP has been {GREEN}auto-enabled{DEFAULT} because you are now spectating!", print: PrintTo.Chat);
			}
		});
		return HookResult.Continue;
	}
	public HookResult OnPlayerTeam(EventPlayerTeam @event, GameEventInfo info)
	{
		var player = @event.Userid;
		if (player is null || player.IsValid is not true || player.Connected is not PlayerConnectedState.PlayerConnected) return HookResult.Continue;

		// If admin is leaving spectator, disable their ESP
		if (@event.Team != (int)CsTeam.Spectator && toggleAdminESP[player.Slot])
		{
			toggleAdminESP[player.Slot] = false;
			if (AreThereEsperingAdmins() is not true)
				RemoveAllGlowingPlayers();

			if (wantESP[player.Slot])
				SendMessageToSpecificChat(player, msg: "Admin ESP has been {RED}disabled{DEFAULT} because you left spectate!", print: PrintTo.Chat);
		}

		// If admin is joining spectator and has wantESP on, re-enable it
		if ((@event.Team == (int)CsTeam.Spectator || @event.Team == (int)CsTeam.None) && wantESP[player.Slot])
		{
			AddTimer(0.1f, () =>
			{
				if (player is null || player.IsValid is not true || (player.Team is not CsTeam.Spectator && player.Team is not CsTeam.None)) return;
				toggleAdminESP[player.Slot] = true;
				if (togglePlayersGlowing is not true || AreThereEsperingAdmins() is not true)
					SetAllPlayersGlowing();
				SendMessageToSpecificChat(player, msg: "Admin ESP has been {GREEN}auto-enabled{DEFAULT} because you joined spectate!", print: PrintTo.Chat);
			});
		}

		return HookResult.Continue;
	}

	private void OnClientDisconnected(int slot)
	{
		var player = Utilities.GetPlayerFromSlot(slot);
		if (player == null || player.IsValid is not true) return;
		//set 'toggleAdminESP' to false regardless, on player disconnected
		//thus avoid any lingering glowing props
		toggleAdminESP[slot] = false;
		// NOU: Resetează și starea persistentă la disconnect (opțional; poți elimina dacă vrei să persiste peste reconectări)
		wantESP[slot] = false;
		//remove player from cached list
		if (cachedPlayers.Contains(player) is true)
			cachedPlayers.Remove(player);

	}
}