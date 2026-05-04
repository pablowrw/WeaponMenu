# WeaponMenu

A CS2 plugin for [SwiftlyS2](https://github.com/swiftlys2/swiftlys2) that gives players a weapon selection menu, quick weapon commands, per-map configurations, spawn loadouts, and automatic ground weapon cleanup.

---

## Features

- **Weapon menu** — full, primary-only, or secondary-only via chat commands
- **Quick commands** — type `!awp`, `!ak`, `!m4s` etc. to instantly switch weapons while alive
- **Persistent choice** — picked weapon is remembered and restored automatically every round
- **Per-map configuration** — whitelist, blacklist, loadout and ground weapon rules per map name or workshop ID
- **Spawn loadout** — give players specific weapons, grenades and armor on round start; saved menu choice overrides the matching slot
- **Ground weapon removal** — clear weapons from the ground at round start; keep specific weapons with `KeepGroundWeapons`; configurable delay for precise timing relative to the spawn loadout
- **Configurable commands** — rename or disable `!guns`, `!primary`, `!secondary`, and quick commands via config
- **Chat prefix** — configurable colored prefix before all plugin messages; set to `""` to disable
- **Permission system** — restrict all commands to a specific flag/permission group

---

## Requirements

- [SwiftlyS2](https://github.com/swiftlys2/swiftlys2) installed on the server

---

## Installation

1. Download the latest release ZIP
2. Extract the `WeaponMenu` folder into:
   ```
   csgo/addons/swiftlys2/plugins/
   ```
3. Start or restart the server — the default config is generated automatically at:
   ```
   csgo/addons/swiftlys2/configs/plugins/WeaponMenu/weaponmenu.jsonc
   ```
4. Edit the config to your needs and reload the plugin

---

## Commands

Default command names are configurable — see the [`Commands`](#commands-1) section.

### Menu commands

| Command | Description |
|---------|-------------|
| `!guns` / `!menu` | Open the full weapon menu |
| `!primary` | Open the primary weapons menu |
| `!secondary` | Open the secondary weapons menu |

### Quick weapon commands

| Command(s) | Weapon |
|------------|--------|
| `!ak` `!ak47` | AK-47 |
| `!m4` `!m4a4` | M4A4 |
| `!m4a1` `!m4s` | M4A1-S |
| `!awp` | AWP |
| `!scout` `!ssg` `!ssg08` | SSG 08 |
| `!g3sg1` `!auto` | G3SG1 |
| `!scar` `!scar20` | SCAR-20 |
| `!famas` | FAMAS |
| `!aug` | AUG |
| `!galil` | Galil AR |
| `!sg` `!sg556` `!krieg` | SG 556 |
| `!mp9` | MP9 |
| `!mp7` | MP7 |
| `!mp5` `!mp5sd` | MP5-SD |
| `!ump` `!ump45` | UMP-45 |
| `!p90` | P90 |
| `!bizon` | PP-Bizon |
| `!mac` `!mac10` | MAC-10 |
| `!xm` `!xm1014` | XM1014 |
| `!mag7` | MAG-7 |
| `!sawedoff` | Sawed-Off |
| `!nova` | Nova |
| `!m249` | M249 |
| `!negev` | Negev |
| `!usp` | USP-S |
| `!p2000` | P2000 |
| `!glock` | Glock-18 |
| `!dual` `!dualies` | Dual Berettas |
| `!p250` | P250 |
| `!57` `!fiveseven` | Five-SeveN |
| `!cz` `!cz75` | CZ75-Auto |
| `!tec` `!tec9` | Tec-9 |
| `!r8` `!revolver` | R8 Revolver |
| `!deagle` `!de` | Desert Eagle |

---

## Configuration

Config file location:
```
addons/swiftlys2/configs/plugins/WeaponMenu/weaponmenu.jsonc
```

### Global settings

| Key | Default | Description |
|-----|---------|-------------|
| `ChatPrefix` | `"[ [lime]★ [darkred]WeaponMenu [lime]★ [default]]"` | Prefix shown before every plugin message. `""` = disable. |
| `PermissionForCommands` | `""` | Permission flag required to use all commands. Empty = everyone. |
| `CloseMenuOnSelect` | `false` | Close the menu automatically after a weapon is chosen. |
| `RequireBuyZone` | `false` | Restrict weapon selection to buy zones (de_, cs_ maps only). |
| `AutoOpenMenu` | `false` | Automatically open the menu for every player when they spawn. |
| `MenuDisplayTime` | `0` | Seconds before the menu closes automatically. `0` = never. |
| `SelectionTimeLimit` | `0` | Seconds after spawn during which selection is allowed. `0` = no limit. |
| `Blacklist` | `[]` | Weapon display names blocked globally in the menu. |
| `RemoveGroundWeapons` | `false` | Remove all ground weapons at round start. |
| `RemoveGroundWeaponsDelay` | `0.1` | Seconds after round start before removing ground weapons. |
| `KeepGroundWeapons` | `[]` | Weapons to keep on the ground when `RemoveGroundWeapons` is true. |
| `WeaponLimits` | `{}` | Maximum alive players per team carrying each weapon (see below). |
| `SpawnLoadout` | — | Weapons and armor given on round start (see below). |
| `MapConfigs` | `{}` | Per-map overrides (see below). |
| `Commands` | — | Rename or disable commands (see below). |

### WeaponLimits

`WeaponLimits` is a JSON **object** (key → value), not an array. Each key is a weapon display name and the value is the maximum number of alive players per team allowed to carry it simultaneously.

```jsonc
// Array format (used by Blacklist, Whitelist, etc.) — a plain list:
"Blacklist": ["AWP", "NEGEV", "M249"]

// Object format (used by WeaponLimits) — name mapped to a count:
"WeaponLimits": { "AWP": 1, "SSG08": 2 }
```

The two formats are different because a limit needs both a weapon name and a number, while a list needs only names.

### SpawnLoadout

```jsonc
"SpawnLoadout": {
  "Enabled": false,
  "ForceStrip": true,      // Remove existing primary/secondary before giving loadout
  "ArmorCT": 100,          // Armor for CT (-1 = do not change)
  "ArmorT": 100,           // Armor for T  (-1 = do not change)
  "SpawnDelay": 0.5,       // Seconds after round start before giving weapons
  "WeaponsCT": [],         // Weapons for CT
  "WeaponsT": []           // Weapons for T
}
```

If `SpawnLoadout` is enabled and the player has a saved menu choice, that choice replaces the matching slot (primary or secondary) from the loadout.

### Commands

```jsonc
"Commands": {
  // Comma-separated aliases for the full weapon menu. "" = disable.
  "MenuCommand": "guns,menu",

  // Primary-weapons-only menu. "" = disable.
  "PrimaryCommand": "primary",

  // Secondary-weapons-only menu. "" = disable.
  "SecondaryCommand": "secondary",

  // false = disable all quick commands (!ak, !awp, !deagle, …).
  "EnableQuickCommands": true
}
```

### Per-map options

Each entry in `MapConfigs` uses the map name (e.g. `"aim_map"`) or workshop ID as the key. All fields are optional — omit any field to fall back to the global value.

```jsonc
"MapConfigs": {

  // Standard competitive map — 1 AWP per team, heavy MGs hidden.
  "de_dust2": {
    "WeaponLimits": { "AWP": 1 },
    "Blacklist": ["NEGEV", "M249"],
    "RemoveGroundWeapons": false
  },

  // Aim map — no buy zones; use SelectionTimeLimit (global) for restriction.
  // Only AK47, M4A1 and DEAGLE in the menu; keep AWPs on the ground.
  "aim_map": {
    "Whitelist": ["AK47", "M4A1", "DEAGLE"],
    "RemoveGroundWeapons": true,
    "RemoveGroundWeaponsDelay": 0.1,
    "KeepGroundWeapons": ["AWP"]
  },

  // Workshop map by ID — loadout with rifles and grenades.
  "3085962528": {
    "Blacklist": ["NEGEV", "M249"],
    "SpawnLoadout": {
      "Enabled": true,
      "ForceStrip": true,
      "ArmorCT": 100,
      "ArmorT": 100,
      "SpawnDelay": 0.5,
      "WeaponsCT": ["M4A1", "USP", "flashbang", "smoke"],
      "WeaponsT": ["AK47", "GLOCK", "flashbang", "smoke"]
    }
  }
}
```

All per-map fields:

| Field | Type | Description |
|-------|------|-------------|
| `Whitelist` | `[]` | If non-empty, only these weapons appear in the menu (global `Blacklist` ignored). |
| `Blacklist` | `[]` | Combined with the global `Blacklist`. |
| `RemoveGroundWeapons` | bool | Override global setting. |
| `RemoveGroundWeaponsDelay` | float | Override global delay. |
| `KeepGroundWeapons` | `[]` | Override global keep list. |
| `WeaponLimits` | `{}` | Overrides global `WeaponLimits` entirely when non-empty. |
| `SpawnLoadout` | object | Overrides global `SpawnLoadout` entirely. |

---

## Weapon names

### Primaries

`M4A4` `M4A1` `FAMAS` `AUG` `AK47` `GALIL` `SG556` `SCAR20` `AWP` `SSG08` `G3SG1`
`MP9` `MP7` `MP5SD` `UMP45` `P90` `BIZON` `MAC10`
`XM1014` `MAG7` `SAWEDOFF` `NOVA` `M249` `NEGEV`

### Secondaries

`USP` `P2000` `GLOCK` `DUAL` `P250` `FIVESEVEN` `CZ75A` `TEC9` `REVOLVER` `DEAGLE`

### Grenades and utility (for SpawnLoadout)

Short aliases and full CS2 give names are both accepted in `WeaponsCT` / `WeaponsT`:

| Short alias | Also accepted as | Item |
|-------------|-----------------|------|
| `flashbang` | `flash` | Flashbang |
| `smoke` | `smokegrenade` | Smoke grenade |
| `he` | `hegrenade` | HE grenade |
| `molotov` | — | Molotov (T only) |
| `incgrenade` | `incendiary` | Incendiary (CT only) |
| `decoy` | — | Decoy grenade |
| `c4` | — | Bomb |
| `taser` | — | Zeus x27 |

---

## Spawn restriction

Two independent mechanisms — they can be used together:

| Setting | Works on | How it restricts |
|---------|----------|-----------------|
| `RequireBuyZone: true` | Maps with buy zone triggers (de_, cs_) | Player must be standing inside the buy zone |
| `SelectionTimeLimit: N` | All maps | Selection allowed for N seconds after each spawn |

For aim maps or any map without buy zone brushes, use `SelectionTimeLimit`.

### Combining AutoOpenMenu with SelectionTimeLimit

```jsonc
"AutoOpenMenu": true,
"SelectionTimeLimit": 10,
"MenuDisplayTime": 10    // menu closes exactly when selection expires
```

The menu pops up on spawn and disappears after 10 seconds. After that, no further selection is possible until the next spawn.

---

## Ground weapon timing

```
RemoveGroundWeaponsDelay < SpawnDelay  →  ground cleared first, then loadout given
RemoveGroundWeaponsDelay > SpawnDelay  →  loadout given first, then ground cleared
```

---

## Chat prefix color codes

Supported color tags for `ChatPrefix`:

`[default]` `[white]` `[darkred]` `[red]` `[green]` `[lime]` `[lightgreen]`
`[grey]` `[silver]` `[blue]` `[lightblue]` `[purple]` `[orange]` `[yellow]` `[gold]`

---

## License

MIT
