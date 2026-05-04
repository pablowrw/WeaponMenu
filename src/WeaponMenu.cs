using SwiftlyS2.Shared.Plugins;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Commands;
using SwiftlyS2.Shared.Events;
using SwiftlyS2.Shared.GameEventDefinitions;
using SwiftlyS2.Shared.Menus;
using SwiftlyS2.Shared.Misc;
using SwiftlyS2.Core.Menus.OptionsBase;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.SchemaDefinitions;
using SwiftlyS2.Shared.Translation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace WeaponMenu;

[PluginMetadata(
    Id = "WeaponMenu",
    Version = "1.0.0",
    Name = "Weapon Menu",
    Author = "Pablowrw",
    Description = "Per-map weapon menu, spawn loadout, ground weapon cleanup and quick commands.",
    Website = "")]
public partial class WeaponMenuPlugin : BasePlugin
{
    public WeaponMenuConfig Config { get; set; } = new();

    private string _cachedMapName = "";
    private string _cachedWorkshopId = "";

    // Per-player weapon choices, reset on map change and cleared on disconnect.
    private readonly Dictionary<IPlayer, string> _playerPrimaryChoice   = new();
    private readonly Dictionary<IPlayer, string> _playerSecondaryChoice = new();

    // Keyed by SteamID — set on each spawn, cleared on death and map load.
    // SelectionTimeLimit counts from this timestamp.
    private readonly Dictionary<ulong, DateTime> _playerSpawnTime = new();

    private ILocalizer Localizer => Core.Localizer;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private static readonly JsonDocumentOptions DocOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public WeaponMenuPlugin(ISwiftlyCore core) : base(core) { }

    public override void ConfigureSharedInterface(IInterfaceManager interfaceManager) { }
    public override void UseSharedInterface(IInterfaceManager interfaceManager) { }

    public override void Load(bool hotReload)
    {
        Core.Configuration.InitializeJsonWithModel<WeaponMenuConfig>("weaponmenu", "WeaponMenu");
        EnsureConfigTemplate();
        LoadConfigFromFile();

        Core.Event.OnMapLoad += OnMapLoad;
        Core.Registrator.Register(this);

        Core.GameEvent.HookPost<EventRoundStart>(@event =>
        {
            OnRoundStart();
            return HookResult.Continue;
        });

        Core.GameEvent.HookPost<EventPlayerSpawn>(@event =>
        {
            var controller = @event.UserIdController;
            if (controller == null) return HookResult.Continue;
            // Defer by one tick: EventPlayerSpawn fires before PlayerManager
            // finishes registering the player, so an immediate lookup returns null.
            Core.Scheduler.NextTick(() =>
            {
                var player = Core.PlayerManager.GetAllValidPlayers()
                    .FirstOrDefault(p => p?.Controller == controller);
                if (player == null || player.IsFakeClient) return;

                // Close any menu that was left open from the previous life.
                var openMenu = Core.MenusAPI?.GetCurrentMenu(player);
                if (openMenu != null) Core.MenusAPI?.CloseMenuForPlayer(player, openMenu);

                // CS2 fires player_spawn multiple times per physical spawn.
                // isFirst = true only when no entry exists (first spawn, or after death
                // cleared it). isRespawn = true if somehow an old entry survived and
                // SelectionTimeLimit has already expired (safety net).
                bool isFirst   = !_playerSpawnTime.TryGetValue(player.SteamID, out var prev);
                float limit    = Config.SelectionTimeLimit;
                bool isRespawn = !isFirst && limit > 0f
                                          && (DateTime.UtcNow - prev).TotalSeconds >= (double)limit;

                if (isFirst || isRespawn)
                {
                    _playerSpawnTime[player.SteamID] = DateTime.UtcNow;

                    if (Config.AutoOpenMenu && PlayerHasPermission(player))
                    {
                        Core.Scheduler.DelayBySeconds(0.5f, () =>
                        {
                            if (player == null || !player.IsValid) return;
                            if (player.Controller?.PawnIsAlive != true) return;
                            if (!IsSelectionAllowed(player)) return;
                            ShowMenu(player, "Weapon Menu", GetAllowedWeapons(Helpers.Weapons));
                        });
                    }
                }
            });
            return HookResult.Continue;
        });

        Core.GameEvent.HookPost<EventPlayerDeath>(@event =>
        {
            var controller = @event.UserIdController;
            if (controller == null) return HookResult.Continue;
            var player = Core.PlayerManager.GetAllValidPlayers()
                .FirstOrDefault(p => p?.Controller == controller);
            if (player == null) return HookResult.Continue;
            var openMenu = Core.MenusAPI?.GetCurrentMenu(player);
            if (openMenu != null) Core.MenusAPI?.CloseMenuForPlayer(player, openMenu);
            // Clear spawn time so the next respawn gets a fresh selection window.
            _playerSpawnTime.Remove(player.SteamID);
            return HookResult.Continue;
        });

        Core.GameEvent.HookPost<EventPlayerDisconnect>(@event =>
        {
            var player = @event.UserIdPlayer;
            if (player != null)
            {
                _playerPrimaryChoice.Remove(player);
                _playerSecondaryChoice.Remove(player);
                _playerSpawnTime.Remove(player.SteamID);
            }
            return HookResult.Continue;
        });

        RegisterMenuCommands();
    }

    public override void Unload()
    {
        Core.Event.OnMapLoad -= OnMapLoad;
    }

    private void OnMapLoad(IOnMapLoadEvent @event)
    {
        _cachedMapName = @event.MapName?.ToLower() ?? "";
        try { _cachedWorkshopId = Core.Engine?.WorkshopId?.ToLower() ?? ""; } catch { }
        LoadConfigFromFile();
        _playerPrimaryChoice.Clear();
        _playerSecondaryChoice.Clear();
        _playerSpawnTime.Clear();
    }

    // ── Chat helpers ─────────────────────────────────────────────────────────

    // Hardcoded fallback strings used if Core.Localizer fails to load translations.
    private static readonly Dictionary<string, string> _fallback = new()
    {
        ["WeaponSelected"]     = "You selected {0}.",
        ["WeaponNotAvailable"] = "{0} is not available on this map.",
        ["MustBeAlive"]        = "You must be alive to use this command.",
        ["NoPermission"]       = "You don't have permission to use this command.",
        ["OnlyPlayers"]        = "This command can only be used by players!",
        ["MenuUnavailable"]    = "Menu system is not available.",
        ["SelectionExpired"]   = "Weapon selection time has expired.",
        ["WeaponLimitReached"] = "Your team has reached the limit for {0}.",
        ["NotInBuyZone"]       = "You must be in spawn to use this command."
    };

    // Returns true when the player is standing in their team's buy zone.
    // Uses player.PlayerPawn.InBuyZone — works on maps with buy zone triggers (de_, cs_).
    // Always returns true when RequireBuyZone is disabled.
    private bool IsInBuyZone(IPlayer player)
    {
        if (!Config.RequireBuyZone) return true;
        try { return player.PlayerPawn?.InBuyZone == true; }
        catch { return false; }
    }

    // Returns true when the player has permission to use weapon commands.
    private bool PlayerHasPermission(IPlayer player)
    {
        if (string.IsNullOrEmpty(Config.FlagForCommands)) return true;
        return Core.Permission.PlayerHasPermission(player.SteamID, Config.FlagForCommands);
    }

    // Returns true when the player is still within the allowed selection window.
    // SelectionTimeLimit always counts from when the player spawned.
    private bool IsSelectionAllowed(IPlayer player)
    {
        float limit = Config.SelectionTimeLimit;
        if (limit <= 0f) return true;

        if (!_playerSpawnTime.TryGetValue(player.SteamID, out var spawnedAt))
            return false;

        return (DateTime.UtcNow - spawnedAt).TotalSeconds <= (double)limit;
    }

    // Returns true when the team weapon limit would be exceeded by this selection.
    // Counts teammates (excluding the requesting player) who currently carry the weapon.
    private bool IsWeaponLimitReached(IPlayer player, string weaponKey)
    {
        var mapConfig = GetMapConfig();
        var limits = (mapConfig?.WeaponLimits?.Count > 0)
            ? mapConfig.WeaponLimits
            : Config.WeaponLimits;
        if (limits.Count == 0) return false;
        if (!limits.TryGetValue(weaponKey, out var limit) || limit <= 0) return false;
        if (!Helpers.Weapons.TryGetValue(weaponKey, out var weapon)) return false;

        var team = player.Controller?.Team;
        if (team != Team.CT && team != Team.T) return false;

        int count = Core.PlayerManager.GetAllValidPlayers()
            .Count(p => p != null && p.IsValid && !p.IsFakeClient
                     && p != player
                     && p.Controller?.Team == team
                     && p.Controller.PawnIsAlive
                     && PlayerHasWeapon(p, weapon.GiveName));

        return count >= limit;
    }

    // Returns true when the player currently carries a weapon with the given give-name.
    private bool PlayerHasWeapon(IPlayer player, string giveName)
    {
        try
        {
            var ws = player.Pawn?.WeaponServices;
            if (ws == null) return false;
            foreach (var handle in ws.MyWeapons)
            {
                var w = handle.Value;
                if (w?.DesignerName?.Equals(giveName, StringComparison.OrdinalIgnoreCase) == true)
                    return true;
            }
            return false;
        }
        catch { return false; }
    }

    private string GetText(string key, object[] args)
    {
        string? text = null;
        try { text = args.Length > 0 ? Localizer[key, args] : Localizer[key]; } catch { }
        if (string.IsNullOrEmpty(text) || text == key)
        {
            if (_fallback.TryGetValue(key, out var fb))
                text = args.Length > 0 ? string.Format(fb, args) : fb;
            else
                text = key;
        }
        return text;
    }

    // Send a translated message to the player, prepending Chat.Prefix when set.
    private void Msg(IPlayer player, string key, params object[] args)
    {
        var prefix = Config.ChatPrefix;
        var text   = GetText(key, args);
        player.SendChat(string.IsNullOrEmpty(prefix) ? text : $"{prefix} {text}");
    }

    // Reply to a command context with the prefix applied.
    private void Reply(ICommandContext context, string key, params object[] args)
    {
        var prefix = Config.ChatPrefix;
        var text   = GetText(key, args);
        var msg    = string.IsNullOrEmpty(prefix) ? text : $"{prefix} {text}";
        if (context.IsSentByPlayer && context.Sender != null)
            context.Sender.SendChat(msg);
        else
            context.Reply(msg);
    }

    // ── Round start ──────────────────────────────────────────────────────────

    private void OnRoundStart()
    {
        _playerSpawnTime.Clear();

        var mapConfig = GetMapConfig();

        bool removeGround = mapConfig?.RemoveGroundWeapons ?? Config.RemoveGroundWeapons;
        if (removeGround)
        {
            var keepList = (mapConfig?.KeepGroundWeapons?.Count > 0)
                ? mapConfig.KeepGroundWeapons
                : Config.KeepGroundWeapons;
            float removeDelay = mapConfig?.RemoveGroundWeaponsDelay ?? Config.RemoveGroundWeaponsDelay;
            Core.Scheduler.DelayBySeconds(removeDelay, () => DoRemoveGroundWeapons(keepList));
        }

        var loadoutCfg = mapConfig?.SpawnLoadout ?? Config.SpawnLoadout;
        if (loadoutCfg.Enabled)
        {
            Core.Scheduler.DelayBySeconds(loadoutCfg.SpawnDelay, () =>
            {
                foreach (var player in Core.PlayerManager.GetAllValidPlayers())
                {
                    if (player == null || !player.IsValid || player.IsFakeClient) continue;
                    if (player.Controller == null || !player.Controller.PawnIsAlive) continue;
                    GiveLoadout(player, loadoutCfg);
                }
            });
        }
        else
        {
            // SpawnLoadout is disabled — still apply each player's saved weapon choice.
            Core.Scheduler.DelayBySeconds(0.5f, () =>
            {
                foreach (var player in Core.PlayerManager.GetAllValidPlayers())
                {
                    if (player == null || !player.IsValid || player.IsFakeClient) continue;
                    if (player.Controller == null || !player.Controller.PawnIsAlive) continue;
                    ApplySavedChoice(player);
                }
            });
        }
    }

    // ── Spawn loadout ────────────────────────────────────────────────────────

    private void GiveLoadout(IPlayer player, SpawnLoadoutConfig loadoutCfg)
    {
        if (player?.Pawn == null || !player.Pawn.IsValid) return;
        if (player.Controller == null || !player.Controller.PawnIsAlive) return;

        var team = player.Controller.Team;
        bool isCT = team == Team.CT;
        bool isT  = team == Team.T;
        if (!isCT && !isT) return;

        var rawList = isCT ? loadoutCfg.WeaponsCT : loadoutCfg.WeaponsT;
        // Accept display names ("AK47"), short grenade aliases ("flashbang", "smoke"),
        // or full CS2 give names ("weapon_flashbang") — all forms are supported.
        var giveNames = rawList
            .Select(entry =>
            {
                var trimmed = entry.Trim();
                // Display name lookup (e.g. "AK47" → "weapon_ak47")
                var key = Helpers.Weapons.Keys
                    .FirstOrDefault(k => k.Equals(trimmed, StringComparison.OrdinalIgnoreCase));
                if (key != null) return Helpers.Weapons[key].GiveName;
                // Short grenade/utility alias (e.g. "flashbang" → "weapon_flashbang")
                if (Helpers.GrenadeAliases.TryGetValue(trimmed, out var grenadeGive))
                    return grenadeGive;
                // Already a full give name — pass through as-is
                return trimmed.ToLowerInvariant();
            })
            .Where(s => !string.IsNullOrEmpty(s))
            .ToList();

        var primaryGiveNames = Helpers.Weapons.Values
            .Where(w => w.Type == Helpers.WeaponType.Primary)
            .Select(w => w.GiveName.ToLowerInvariant())
            .ToHashSet();
        var secondaryGiveNames = Helpers.Weapons.Values
            .Where(w => w.Type == Helpers.WeaponType.Secondary)
            .Select(w => w.GiveName.ToLowerInvariant())
            .ToHashSet();

        // Player menu choice overrides the loadout slot if still allowed on this map.
        var allowedSet = GetAllowedWeapons(Helpers.Weapons)
            .Select(w => w.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);

        string? chosenPrimaryGiveName = null;
        if (_playerPrimaryChoice.TryGetValue(player, out var sp) && allowedSet.Contains(sp)
            && Helpers.Weapons.TryGetValue(sp, out var spw))
            chosenPrimaryGiveName = spw.GiveName.ToLowerInvariant();

        string? chosenSecondaryGiveName = null;
        if (_playerSecondaryChoice.TryGetValue(player, out var ss) && allowedSet.Contains(ss)
            && Helpers.Weapons.TryGetValue(ss, out var ssw))
            chosenSecondaryGiveName = ssw.GiveName.ToLowerInvariant();

        bool primarySlotFilled = false, secondarySlotFilled = false;
        var finalGiveNames = new List<string>();

        foreach (var gn in giveNames)
        {
            if (gn.Contains("knife") || gn.Contains("bayonet")) continue;

            if (primaryGiveNames.Contains(gn))
            {
                if (!primarySlotFilled)
                {
                    finalGiveNames.Add(chosenPrimaryGiveName ?? gn);
                    primarySlotFilled = true;
                }
            }
            else if (secondaryGiveNames.Contains(gn))
            {
                if (!secondarySlotFilled)
                {
                    finalGiveNames.Add(chosenSecondaryGiveName ?? gn);
                    secondarySlotFilled = true;
                }
            }
            else
            {
                finalGiveNames.Add(gn);
            }
        }

        // If the loadout didn't define a slot, still give the player's saved choice.
        if (!primarySlotFilled && chosenPrimaryGiveName != null)
            finalGiveNames.Add(chosenPrimaryGiveName);
        if (!secondarySlotFilled && chosenSecondaryGiveName != null)
            finalGiveNames.Add(chosenSecondaryGiveName);

        if (loadoutCfg.ForceStrip)
        {
            try { player.Pawn.WeaponServices?.RemoveWeaponBySlot(gear_slot_t.GEAR_SLOT_RIFLE);  } catch { }
            try { player.Pawn.WeaponServices?.RemoveWeaponBySlot(gear_slot_t.GEAR_SLOT_PISTOL); } catch { }
        }

        foreach (var gn in finalGiveNames)
        {
            try { player.Pawn.ItemServices?.GiveItem(gn); } catch { }
        }

        int armorValue = isCT ? loadoutCfg.ArmorCT : loadoutCfg.ArmorT;
        if (armorValue >= 0)
        {
            try
            {
                var csPawn = player.Pawn as CCSPlayerPawn;
                if (csPawn != null) csPawn.ArmorValue = armorValue;
            }
            catch { }
        }
    }

    // Apply a player's saved primary/secondary choice without a full loadout.
    // Used at round start when SpawnLoadout is disabled.
    private void ApplySavedChoice(IPlayer player)
    {
        if (player?.Pawn == null || !player.Pawn.IsValid) return;
        if (player.Controller == null || !player.Controller.PawnIsAlive) return;

        var allowedSet = GetAllowedWeapons(Helpers.Weapons)
            .Select(w => w.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (_playerPrimaryChoice.TryGetValue(player, out var pk)
            && allowedSet.Contains(pk)
            && Helpers.Weapons.TryGetValue(pk, out var pw))
        {
            try { player.Pawn.WeaponServices?.RemoveWeaponBySlot(gear_slot_t.GEAR_SLOT_RIFLE);  } catch { }
            try { player.Pawn.ItemServices?.GiveItem(pw.GiveName); } catch { }
        }

        if (_playerSecondaryChoice.TryGetValue(player, out var sk)
            && allowedSet.Contains(sk)
            && Helpers.Weapons.TryGetValue(sk, out var sw))
        {
            try { player.Pawn.WeaponServices?.RemoveWeaponBySlot(gear_slot_t.GEAR_SLOT_PISTOL); } catch { }
            try { player.Pawn.ItemServices?.GiveItem(sw.GiveName); } catch { }
        }
    }

    // ── Ground weapon removal ────────────────────────────────────────────────

    private void DoRemoveGroundWeapons(IEnumerable<string> keepDisplayNames)
    {
        try
        {
            var keepGiveNames = keepDisplayNames
                .Where(k => Helpers.Weapons.ContainsKey(k))
                .Select(k => Helpers.Weapons[k].GiveName)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var allWeaponGiveNames = Helpers.Weapons.Values
                .Select(w => w.GiveName)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            // Materialize before iterating — modifying entities mid-loop would
            // invalidate the lazy LINQ enumerator and silently abort the loop.
            var snapshot = Core.EntitySystem.GetAllEntities().ToList();
            foreach (var entity in snapshot)
            {
                try
                {
                    if (entity == null || !entity.IsValid) continue;
                    var name = entity.DesignerName;
                    if (string.IsNullOrEmpty(name)) continue;
                    if (!allWeaponGiveNames.Contains(name)) continue;
                    if (keepGiveNames.Contains(name)) continue;
                    if (entity is CBaseEntity baseEntity && baseEntity.OwnerEntity.Value is CCSPlayerPawn) continue;
                    entity.AddEntityIOEvent<string>("Kill", "", null, null, 0.0f);
                }
                catch { }
            }
        }
        catch { }
    }

    // ── Weapon give + save choice ────────────────────────────────────────────

    private void GiveWeaponAndSave(IPlayer player, string weaponKey)
    {
        if (!Helpers.Weapons.TryGetValue(weaponKey, out var weapon)) return;
        if (player?.Pawn == null || !player.Pawn.IsValid) return;

        var slot = weapon.Type == Helpers.WeaponType.Primary
            ? gear_slot_t.GEAR_SLOT_RIFLE
            : gear_slot_t.GEAR_SLOT_PISTOL;

        if (weapon.Type == Helpers.WeaponType.Primary)
            _playerPrimaryChoice[player] = weaponKey;
        else
            _playerSecondaryChoice[player] = weaponKey;

        Core.Scheduler.NextTick(() =>
        {
            if (player == null || !player.IsValid) return;
            var pawn = player.Pawn;
            if (pawn?.WeaponServices == null || pawn.ItemServices == null) return;

            try { pawn.WeaponServices.RemoveWeaponBySlot(slot); } catch { }
            pawn.ItemServices.GiveItem(weapon.GiveName);
            Msg(player, "WeaponSelected", weaponKey);

            Core.Scheduler.Delay(1, () =>
            {
                if (player == null || !player.IsValid) return;
                var p = player.Pawn;
                if (p?.WeaponServices == null) return;
                try { p.WeaponServices.SelectWeaponBySlot(slot); }
                catch { try { p.WeaponServices.SelectWeaponByDesignerName(weapon.GiveName); } catch { } }
            });
        });
    }

    // ── Command registration ──────────────────────────────────────────────────

    private static IEnumerable<string> ParseAliases(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) yield break;
        foreach (var part in value.Split(','))
        {
            var trimmed = part.Trim();
            if (!string.IsNullOrEmpty(trimmed)) yield return trimmed;
        }
    }

    private void RegisterMenuCommands()
    {
        var cmds = Config.Commands;

        foreach (var alias in ParseAliases(cmds.MenuCommand))
            Core.Command.RegisterCommand(alias, OnGunsCommand);

        foreach (var alias in ParseAliases(cmds.PrimaryCommand))
            Core.Command.RegisterCommand(alias, OnPrimaryCommand);

        foreach (var alias in ParseAliases(cmds.SecondaryCommand))
            Core.Command.RegisterCommand(alias, OnSecondaryCommand);

        if (cmds.EnableQuickCommands)
            RegisterQuickCommands();
    }

    private void RegisterQuickCommands()
    {
        var registered = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (alias, weaponKey) in Helpers.QuickAliases)
        {
            if (!registered.Add(alias)) continue;
            var capturedAlias = alias;
            var capturedKey   = weaponKey;
            Core.Command.RegisterCommand(capturedAlias, ctx => OnQuickCommand(ctx, capturedKey));
        }
    }

    private void OnQuickCommand(ICommandContext context, string weaponKey)
    {
        if (!context.IsSentByPlayer) return;
        if (!CheckPermission(context)) { Reply(context, "NoPermission"); return; }

        var player = context.Sender!;
        if (!player.IsValid) return;
        if (player.Pawn == null || !player.Pawn.IsValid || player.Controller == null || !player.Controller.PawnIsAlive)
        { Reply(context, "MustBeAlive"); return; }

        if (!IsInBuyZone(player))
        { Reply(context, "NotInBuyZone"); return; }

        if (!IsSelectionAllowed(player))
        { Reply(context, "SelectionExpired"); return; }

        if (!GetAllowedWeapons(Helpers.Weapons).Any(w => w.Key.Equals(weaponKey, StringComparison.OrdinalIgnoreCase)))
        { Reply(context, "WeaponNotAvailable", weaponKey); return; }

        if (IsWeaponLimitReached(player, weaponKey))
        { Reply(context, "WeaponLimitReached", weaponKey); return; }

        GiveWeaponAndSave(player, weaponKey);
    }

    // ── Config ───────────────────────────────────────────────────────────────

    private void EnsureConfigTemplate()
    {
        if (FindConfigFile() != null) return;
        try
        {
            var pluginDir = Core.PluginPath;
            if (string.IsNullOrEmpty(pluginDir)) return;
            var templatePath = Path.GetFullPath(
                Path.Combine(pluginDir, "resources", "templates", "weaponmenu.jsonc"));
            if (!File.Exists(templatePath)) return;
            var configDir = Path.GetFullPath(
                Path.Combine(pluginDir, "..", "..", "configs", "plugins", "WeaponMenu"));
            Directory.CreateDirectory(configDir);
            File.Copy(templatePath, Path.Combine(configDir, "weaponmenu.jsonc"), overwrite: false);
        }
        catch { }
    }

    private void LoadConfigFromFile()
    {
        var path = FindConfigFile();
        if (path == null) return;
        try { Config = ReadConfigFromFile(path) ?? new WeaponMenuConfig(); }
        catch { Config = new WeaponMenuConfig(); }
    }

    // SwiftlyS2 may wrap the config as  "PluginId": { ... }.
    // We detect the inner object by checking for known top-level keys.
    private WeaponMenuConfig? ReadConfigFromFile(string path)
    {
        var raw = File.ReadAllText(path).Trim();
        if (!raw.StartsWith("{") && !raw.StartsWith("["))
            raw = "{" + raw + "}";

        using var doc = JsonDocument.Parse(raw, DocOptions);
        var root = doc.RootElement;

        if (root.ValueKind != JsonValueKind.Object)
            return null;

        foreach (var prop in root.EnumerateObject())
        {
            if (prop.Value.ValueKind != JsonValueKind.Object) continue;
            foreach (var inner in prop.Value.EnumerateObject())
            {
                var n = inner.Name;
                if (n.Equals("PermissionForCommands",      StringComparison.OrdinalIgnoreCase) ||
                    n.Equals("Blacklist",                   StringComparison.OrdinalIgnoreCase) ||
                    n.Equals("MapConfigs",                  StringComparison.OrdinalIgnoreCase) ||
                    n.Equals("SpawnLoadout",                StringComparison.OrdinalIgnoreCase) ||
                    n.Equals("RemoveGroundWeapons",         StringComparison.OrdinalIgnoreCase) ||
                    n.Equals("RemoveGroundWeaponsDelay",    StringComparison.OrdinalIgnoreCase) ||
                    n.Equals("ChatPrefix",                  StringComparison.OrdinalIgnoreCase))
                    return prop.Value.Deserialize<WeaponMenuConfig>(JsonOptions);
            }
        }

        return root.Deserialize<WeaponMenuConfig>(JsonOptions);
    }

    private string? FindConfigFile()
    {
        var candidates = new List<string>();

        var gameDir = Directory.GetCurrentDirectory();
        candidates.Add(Path.Combine(gameDir, "addons", "swiftlys2", "configs", "plugins", "WeaponMenu", "weaponmenu.json"));
        candidates.Add(Path.Combine(gameDir, "addons", "swiftlys2", "configs", "plugins", "WeaponMenu", "weaponmenu.jsonc"));
        candidates.Add(Path.Combine(gameDir, "addons", "swiftlys2", "configs", "plugins", "WeaponMenu", "weaponmenu"));

        try
        {
            var pluginDir = Core.PluginPath;
            if (!string.IsNullOrEmpty(pluginDir))
            {
                var configsDir = Path.Combine(pluginDir, "..", "..", "configs", "plugins", "WeaponMenu");
                candidates.Add(Path.GetFullPath(Path.Combine(configsDir, "weaponmenu.json")));
                candidates.Add(Path.GetFullPath(Path.Combine(configsDir, "weaponmenu.jsonc")));
                candidates.Add(Path.GetFullPath(Path.Combine(configsDir, "weaponmenu")));
            }
        }
        catch { }

        return candidates.FirstOrDefault(File.Exists);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private bool CheckPermission(ICommandContext context)
    {
        if (string.IsNullOrEmpty(Config.FlagForCommands)) return true;
        if (!context.IsSentByPlayer) return false;
        return Core.Permission.PlayerHasPermission(context.Sender!.SteamID, Config.FlagForCommands);
    }

    private (string mapName, string workshopId) GetCurrentMapInfo()
    {
        // Prefer the values cached by OnMapLoad — they come from the authoritative
        // map-change event and are already normalised to lower-case.
        // Only fall back to live lookups when the cache is empty (e.g. hot-reload
        // before the next map change).
        string mapName    = _cachedMapName;
        string workshopId = _cachedWorkshopId;

        if (string.IsNullOrEmpty(mapName))
        {
            try
            {
                var v = Core.Engine?.GlobalVars.MapName.ToString();
                if (!string.IsNullOrEmpty(v)) mapName = v.ToLower();
            }
            catch { }

            if (string.IsNullOrEmpty(mapName))
            {
                try
                {
                    var v = Core.ConVar.FindAsString("mapname")?.ValueAsString;
                    if (!string.IsNullOrEmpty(v)) mapName = v.ToLower();
                }
                catch { }
            }
        }

        if (string.IsNullOrEmpty(workshopId))
        {
            try
            {
                var v = Core.Engine?.WorkshopId;
                if (!string.IsNullOrEmpty(v)) workshopId = v.ToLower();
            }
            catch { }
        }

        return (mapName, workshopId);
    }

    private MapConfig? GetMapConfig()
    {
        var (mapName, workshopId) = GetCurrentMapInfo();

        if (!string.IsNullOrEmpty(workshopId))
            foreach (var key in Config.MapConfigs.Keys)
                if (string.Equals(key.Trim(), workshopId, StringComparison.OrdinalIgnoreCase))
                    return Config.MapConfigs[key];

        if (!string.IsNullOrEmpty(mapName))
            foreach (var key in Config.MapConfigs.Keys)
                if (string.Equals(key.Trim(), mapName, StringComparison.OrdinalIgnoreCase))
                    return Config.MapConfigs[key];

        return null;
    }

    // Whitelist (non-empty) → only those weapons, global Blacklist ignored.
    // Blacklist only → global + map Blacklist combined.
    // No map config → only global Blacklist applied.
    private IEnumerable<KeyValuePair<string, Helpers.Weapon>> GetAllowedWeapons(
        IEnumerable<KeyValuePair<string, Helpers.Weapon>> weapons)
    {
        var globalBlacklist = Config.WeaponBlacklist;
        var mapConfig = GetMapConfig();

        if (mapConfig != null)
        {
            if (mapConfig.WeaponWhitelist.Count > 0)
                return weapons.Where(w =>
                    mapConfig.WeaponWhitelist.Contains(w.Key, StringComparer.OrdinalIgnoreCase));

            if (mapConfig.WeaponBlacklist.Count > 0)
                return weapons.Where(w =>
                    !globalBlacklist.Contains(w.Key, StringComparer.OrdinalIgnoreCase) &&
                    !mapConfig.WeaponBlacklist.Contains(w.Key, StringComparer.OrdinalIgnoreCase));
        }

        return weapons.Where(w =>
            !globalBlacklist.Contains(w.Key, StringComparer.OrdinalIgnoreCase));
    }

    // ── Commands ─────────────────────────────────────────────────────────────

    private void OnGunsCommand(ICommandContext context)
    {
        if (!context.IsSentByPlayer) { Reply(context, "OnlyPlayers"); return; }
        if (!CheckPermission(context)) { Reply(context, "NoPermission"); return; }
        var player = context.Sender!;
        if (!player.IsValid) return;
        if (player.Pawn == null || !player.Pawn.IsValid || player.Controller == null || !player.Controller.PawnIsAlive)
        { Reply(context, "MustBeAlive"); return; }
        if (!IsInBuyZone(player))
        { Reply(context, "NotInBuyZone"); return; }
        if (!IsSelectionAllowed(player))
        { Reply(context, "SelectionExpired"); return; }
        ShowMenu(player, "Weapon Menu", GetAllowedWeapons(Helpers.Weapons));
    }

    private void OnPrimaryCommand(ICommandContext context)
    {
        if (!context.IsSentByPlayer) { Reply(context, "OnlyPlayers"); return; }
        if (!CheckPermission(context)) { Reply(context, "NoPermission"); return; }
        var player = context.Sender!;
        if (!player.IsValid) return;
        if (player.Pawn == null || !player.Pawn.IsValid || player.Controller == null || !player.Controller.PawnIsAlive)
        { Reply(context, "MustBeAlive"); return; }
        if (!IsInBuyZone(player))
        { Reply(context, "NotInBuyZone"); return; }
        if (!IsSelectionAllowed(player))
        { Reply(context, "SelectionExpired"); return; }
        ShowMenu(player, "Primary Weapons",
            GetAllowedWeapons(Helpers.Weapons.Where(w => w.Value.Type == Helpers.WeaponType.Primary)));
    }

    private void OnSecondaryCommand(ICommandContext context)
    {
        if (!context.IsSentByPlayer) { Reply(context, "OnlyPlayers"); return; }
        if (!CheckPermission(context)) { Reply(context, "NoPermission"); return; }
        var player = context.Sender!;
        if (!player.IsValid) return;
        if (player.Pawn == null || !player.Pawn.IsValid || player.Controller == null || !player.Controller.PawnIsAlive)
        { Reply(context, "MustBeAlive"); return; }
        if (!IsInBuyZone(player))
        { Reply(context, "NotInBuyZone"); return; }
        if (!IsSelectionAllowed(player))
        { Reply(context, "SelectionExpired"); return; }
        ShowMenu(player, "Secondary Weapons",
            GetAllowedWeapons(Helpers.Weapons.Where(w => w.Value.Type == Helpers.WeaponType.Secondary)));
    }

    // ── Menu ─────────────────────────────────────────────────────────────────

    private void ShowMenu(IPlayer player, string title, IEnumerable<KeyValuePair<string, Helpers.Weapon>> weapons)
    {
        if (player == null || !player.IsValid) return;
        if (Core.MenusAPI == null) { Msg(player, "MenuUnavailable"); return; }

        var existing = Core.MenusAPI.GetCurrentMenu(player);
        if (existing != null) Core.MenusAPI.CloseMenuForPlayer(player, existing);

        var builder = Core.MenusAPI.CreateBuilder();
        builder.Design.SetMenuTitle(title);
        builder.Design.SetMaxVisibleItems(5);

        foreach (var weapon in weapons)
        {
            var capturedKey = weapon.Key;
            var option = new ButtonMenuOption(capturedKey);
            option.Click += async (_, args) =>
            {
                if (args.Player == null || !args.Player.IsValid) return;

                // Capture the current menu reference NOW — before any deferred work
                // (NextTick in GiveWeaponAndSave) can change the menu state.
                var currentMenu = Core.MenusAPI?.GetCurrentMenu(args.Player);

                if (!PlayerHasPermission(args.Player))
                {
                    Msg(args.Player, "NoPermission");
                    if (currentMenu != null) Core.MenusAPI?.CloseMenuForPlayer(args.Player, currentMenu);
                    return;
                }
                if (!IsInBuyZone(args.Player))
                {
                    Msg(args.Player, "NotInBuyZone");
                    if (currentMenu != null) Core.MenusAPI?.CloseMenuForPlayer(args.Player, currentMenu);
                    return;
                }
                if (!IsSelectionAllowed(args.Player))
                {
                    Msg(args.Player, "SelectionExpired");
                    if (currentMenu != null) Core.MenusAPI?.CloseMenuForPlayer(args.Player, currentMenu);
                    return;
                }
                if (IsWeaponLimitReached(args.Player, capturedKey))
                {
                    Msg(args.Player, "WeaponLimitReached", capturedKey);
                    return;
                }
                GiveWeaponAndSave(args.Player, capturedKey);
                if (Config.CloseMenuOnSelect && currentMenu != null)
                    Core.MenusAPI?.CloseMenuForPlayer(args.Player, currentMenu);
                await System.Threading.Tasks.Task.CompletedTask;
            };
            builder.AddOption(option);
        }

        Core.MenusAPI.OpenMenuForPlayer(player, builder.Build());

        float displayTime = Config.MenuDisplayTime;
        if (displayTime > 0f)
        {
            Core.Scheduler.DelayBySeconds(displayTime, () =>
            {
                if (player == null || !player.IsValid) return;
                var openMenu = Core.MenusAPI?.GetCurrentMenu(player);
                if (openMenu != null) Core.MenusAPI?.CloseMenuForPlayer(player, openMenu);
            });
        }
    }
}
