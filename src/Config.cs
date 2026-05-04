using System.Text.Json.Serialization;

namespace WeaponMenu;

public class SpawnLoadoutConfig
{
    [JsonPropertyName("Enabled")]
    public bool Enabled { get; set; } = false;

    [JsonPropertyName("ForceStrip")]
    public bool ForceStrip { get; set; } = true;

    [JsonPropertyName("ArmorCT")]
    public int ArmorCT { get; set; } = 100;

    [JsonPropertyName("ArmorT")]
    public int ArmorT { get; set; } = 100;

    [JsonPropertyName("SpawnDelay")]
    public float SpawnDelay { get; set; } = 0.5f;

    // Display names (e.g. "AK47", "AWP") or CS2 give names for grenades/items.
    [JsonPropertyName("WeaponsCT")]
    public List<string> WeaponsCT { get; set; } = new();

    [JsonPropertyName("WeaponsT")]
    public List<string> WeaponsT { get; set; } = new();
}

public class MapConfig
{
    [JsonPropertyName("Blacklist")]
    public List<string> WeaponBlacklist { get; set; } = new();

    [JsonPropertyName("Whitelist")]
    public List<string> WeaponWhitelist { get; set; } = new();

    [JsonPropertyName("SpawnLoadout")]
    public SpawnLoadoutConfig? SpawnLoadout { get; set; }

    [JsonPropertyName("RemoveGroundWeapons")]
    public bool? RemoveGroundWeapons { get; set; }

    [JsonPropertyName("RemoveGroundWeaponsDelay")]
    public float? RemoveGroundWeaponsDelay { get; set; }

    [JsonPropertyName("KeepGroundWeapons")]
    public List<string> KeepGroundWeapons { get; set; } = new();

    // Maximum number of alive players per team that may carry each weapon.
    // Example: { "AWP": 1 } — at most 1 player per side can hold an AWP.
    // Overrides the global WeaponLimits entirely when non-empty.
    [JsonPropertyName("WeaponLimits")]
    public Dictionary<string, int> WeaponLimits { get; set; } = new();
}

public class CommandsConfig
{
    // Comma-separated command aliases for the full weapon menu.
    // Set to "" to disable entirely.
    [JsonPropertyName("MenuCommand")]
    public string MenuCommand { get; set; } = "guns,menu";

    // Command name(s) for the primary-weapons-only menu. Empty = disable.
    [JsonPropertyName("PrimaryCommand")]
    public string PrimaryCommand { get; set; } = "primary";

    // Command name(s) for the secondary-weapons-only menu. Empty = disable.
    [JsonPropertyName("SecondaryCommand")]
    public string SecondaryCommand { get; set; } = "secondary";

    // Enable or disable all quick weapon commands (!ak, !awp, !deagle, …).
    [JsonPropertyName("EnableQuickCommands")]
    public bool EnableQuickCommands { get; set; } = true;
}

public class WeaponMenuConfig
{
    [JsonPropertyName("PermissionForCommands")]
    public string FlagForCommands { get; set; } = "";

    // Chat prefix shown before every plugin message. Set to "" to disable.
    [JsonPropertyName("ChatPrefix")]
    public string ChatPrefix { get; set; } = "[ [lime]★ [darkred]WeaponMenu [lime]★ [default]]";

    // Close the weapon menu automatically after a weapon is selected.
    [JsonPropertyName("CloseMenuOnSelect")]
    public bool CloseMenuOnSelect { get; set; } = false;

    // When true, players can only use the weapon menu while standing in their
    // team's spawn area (buy zone). Only works on maps with buy zone triggers
    // (de_, cs_ maps). For aim maps use SelectionTimeLimit instead.
    [JsonPropertyName("RequireBuyZone")]
    public bool RequireBuyZone { get; set; } = false;

    // Automatically open the weapon menu for every player when they spawn.
    // Works on any map — no buy zone triggers required.
    [JsonPropertyName("AutoOpenMenu")]
    public bool AutoOpenMenu { get; set; } = false;

    // Seconds the menu stays visible before it closes automatically.
    // 0 = never auto-close.
    // Tip: set to the same value as SelectionTimeLimit — the menu disappears
    // exactly when selection expires.
    [JsonPropertyName("MenuDisplayTime")]
    public float MenuDisplayTime { get; set; } = 0f;

    // Seconds after spawn during which weapon selection is allowed.
    // 0 = no time limit.
    // SelectionTimeLimit always counts from when the PLAYER SPAWNED
    // (it does not matter if they stay in spawn or walk away).
    [JsonPropertyName("SelectionTimeLimit")]
    public float SelectionTimeLimit { get; set; } = 0f;

    [JsonPropertyName("Blacklist")]
    public List<string> WeaponBlacklist { get; set; } = new();

    [JsonPropertyName("RemoveGroundWeapons")]
    public bool RemoveGroundWeapons { get; set; } = false;

    [JsonPropertyName("RemoveGroundWeaponsDelay")]
    public float RemoveGroundWeaponsDelay { get; set; } = 0.1f;

    [JsonPropertyName("KeepGroundWeapons")]
    public List<string> KeepGroundWeapons { get; set; } = new();

    [JsonPropertyName("SpawnLoadout")]
    public SpawnLoadoutConfig SpawnLoadout { get; set; } = new();

    // Maximum number of alive players per team that may carry each weapon (globally).
    // Example: { "AWP": 1 } — at most 1 AWP carrier per side at a time.
    // Per-map WeaponLimits in MapConfigs overrides this entirely when non-empty.
    [JsonPropertyName("WeaponLimits")]
    public Dictionary<string, int> WeaponLimits { get; set; } = new();

    [JsonPropertyName("MapConfigs")]
    public Dictionary<string, MapConfig> MapConfigs { get; set; } = new();

    [JsonPropertyName("Commands")]
    public CommandsConfig Commands { get; set; } = new();
}
