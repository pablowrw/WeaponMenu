using SwiftlyS2.Shared.Players;

namespace WeaponMenu;

internal class Helpers
{
    public enum WeaponType
    {
        Primary,
        Secondary
    }

    public class Weapon
    {
        public string GiveName { get; set; }
        public WeaponType Type { get; set; }

        public Weapon(string giveName, WeaponType type)
        {
            GiveName = giveName;
            Type = type;
        }
    }

    /// <summary>Display name → weapon data. Display name is also used as config key.</summary>
    public static readonly Dictionary<string, Weapon> Weapons = new()
    {
        { "M4A4",     new Weapon("weapon_m4a1",           WeaponType.Primary)   },
        { "M4A1",     new Weapon("weapon_m4a1_silencer",  WeaponType.Primary)   },
        { "FAMAS",    new Weapon("weapon_famas",          WeaponType.Primary)   },
        { "AUG",      new Weapon("weapon_aug",            WeaponType.Primary)   },
        { "AK47",     new Weapon("weapon_ak47",           WeaponType.Primary)   },
        { "GALIL",    new Weapon("weapon_galilar",        WeaponType.Primary)   },
        { "MP9",      new Weapon("weapon_mp9",            WeaponType.Primary)   },
        { "MP7",      new Weapon("weapon_mp7",            WeaponType.Primary)   },
        { "MP5SD",    new Weapon("weapon_mp5sd",          WeaponType.Primary)   },
        { "UMP45",    new Weapon("weapon_ump45",          WeaponType.Primary)   },
        { "P90",      new Weapon("weapon_p90",            WeaponType.Primary)   },
        { "BIZON",    new Weapon("weapon_bizon",          WeaponType.Primary)   },
        { "MAC10",    new Weapon("weapon_mac10",          WeaponType.Primary)   },
        { "XM1014",   new Weapon("weapon_xm1014",         WeaponType.Primary)   },
        { "MAG7",     new Weapon("weapon_mag7",           WeaponType.Primary)   },
        { "SAWEDOFF", new Weapon("weapon_sawedoff",       WeaponType.Primary)   },
        { "NOVA",     new Weapon("weapon_nova",           WeaponType.Primary)   },
        { "M249",     new Weapon("weapon_m249",           WeaponType.Primary)   },
        { "NEGEV",    new Weapon("weapon_negev",          WeaponType.Primary)   },
        { "SG556",    new Weapon("weapon_sg556",          WeaponType.Primary)   },
        { "SCAR20",   new Weapon("weapon_scar20",         WeaponType.Primary)   },
        { "AWP",      new Weapon("weapon_awp",            WeaponType.Primary)   },
        { "SSG08",    new Weapon("weapon_ssg08",          WeaponType.Primary)   },
        { "G3SG1",    new Weapon("weapon_g3sg1",          WeaponType.Primary)   },

        { "USP",      new Weapon("weapon_usp_silencer",  WeaponType.Secondary) },
        { "P2000",    new Weapon("weapon_hkp2000",        WeaponType.Secondary) },
        { "GLOCK",    new Weapon("weapon_glock",          WeaponType.Secondary) },
        { "DUAL",     new Weapon("weapon_elite",          WeaponType.Secondary) },
        { "P250",     new Weapon("weapon_p250",           WeaponType.Secondary) },
        { "FIVESEVEN",new Weapon("weapon_fiveseven",      WeaponType.Secondary) },
        { "CZ75A",    new Weapon("weapon_cz75a",          WeaponType.Secondary) },
        { "TEC9",     new Weapon("weapon_tec9",           WeaponType.Secondary) },
        { "REVOLVER", new Weapon("weapon_revolver",       WeaponType.Secondary) },
        { "DEAGLE",   new Weapon("weapon_deagle",         WeaponType.Secondary) },
    };

    /// <summary>
    /// Short grenade/utility names → full CS2 give names.
    /// Allows using "flashbang", "smoke", "he", etc. in loadout configs.
    /// </summary>
    public static readonly Dictionary<string, string> GrenadeAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        { "flashbang",    "weapon_flashbang"    },
        { "flash",        "weapon_flashbang"    },
        { "smoke",        "weapon_smokegrenade" },
        { "smokegrenade", "weapon_smokegrenade" },
        { "he",           "weapon_hegrenade"    },
        { "hegrenade",    "weapon_hegrenade"    },
        { "molotov",      "weapon_molotov"      },
        { "incgrenade",   "weapon_incgrenade"   },
        { "incendiary",   "weapon_incgrenade"   },
        { "decoy",        "weapon_decoy"        },
        { "c4",           "weapon_c4"           },
        { "taser",        "weapon_taser"        },
    };

    /// <summary>
    /// Quick command aliases → Weapons display name.
    /// e.g. "ak" → "AK47", "scout" → "SSG08".
    /// Used to register !ak, !m4, !awp, etc.
    /// Multiple aliases can map to the same weapon.
    /// </summary>
    public static readonly Dictionary<string, string> QuickAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        // Rifles
        { "ak",        "AK47"      },
        { "ak47",      "AK47"      },
        { "m4",        "M4A4"      },
        { "m4a4",      "M4A4"      },
        { "m4a1",      "M4A1"      },
        { "m4s",       "M4A1"      },
        { "famas",     "FAMAS"     },
        { "aug",       "AUG"       },
        { "galil",     "GALIL"     },
        { "sg",        "SG556"     },
        { "sg556",     "SG556"     },
        { "krieg",     "SG556"     },
        { "scar",      "SCAR20"    },
        { "scar20",    "SCAR20"    },
        // Snipers
        { "awp",       "AWP"       },
        { "scout",     "SSG08"     },
        { "ssg",       "SSG08"     },
        { "ssg08",     "SSG08"     },
        { "g3sg1",     "G3SG1"     },
        { "auto",      "G3SG1"     },
        // SMGs
        { "mp9",       "MP9"       },
        { "mp7",       "MP7"       },
        { "mp5",       "MP5SD"     },
        { "mp5sd",     "MP5SD"     },
        { "ump",       "UMP45"     },
        { "ump45",     "UMP45"     },
        { "p90",       "P90"       },
        { "bizon",     "BIZON"     },
        { "mac",       "MAC10"     },
        { "mac10",     "MAC10"     },
        // Shotguns / heavy
        { "xm",        "XM1014"    },
        { "xm1014",    "XM1014"    },
        { "mag7",      "MAG7"      },
        { "sawedoff",  "SAWEDOFF"  },
        { "nova",      "NOVA"      },
        { "m249",      "M249"      },
        { "negev",     "NEGEV"     },
        // Pistols
        { "usp",       "USP"       },
        { "p2000",     "P2000"     },
        { "glock",     "GLOCK"     },
        { "dual",      "DUAL"      },
        { "dualies",   "DUAL"      },
        { "p250",      "P250"      },
        { "57",        "FIVESEVEN" },
        { "fiveseven", "FIVESEVEN" },
        { "cz",        "CZ75A"     },
        { "cz75",      "CZ75A"     },
        { "tec",       "TEC9"      },
        { "tec9",      "TEC9"      },
        { "r8",        "REVOLVER"  },
        { "revolver",  "REVOLVER"  },
        { "deagle",    "DEAGLE"    },
        { "de",        "DEAGLE"    },
    };
}
