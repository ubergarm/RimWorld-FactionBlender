﻿using RimWorld;
using System.Collections.Generic;
using System.Linq;
using HugsLib;
using HugsLib.Settings;
using Verse;

namespace FactionBlender {
    [StaticConstructorOnStartup]
    public class Base : ModBase {
        public override string ModIdentifier {
            get { return "FactionBlender"; }
        }
        public static Base             Instance    { get; private set; }
        public static DefInjectors     DefInjector { get; private set; }
        public static List<FactionDef> FB_Factions { get; private set; }

        public Base() {
            Instance    = this;
            DefInjector = new FactionBlender.DefInjectors();
            FB_Factions = new List<FactionDef>();
        }

        // Settings
        internal Dictionary<string, SettingHandle> config = new Dictionary<string, SettingHandle>();

        public string lastSettingChanged = "";

        public string[] excludedFactionTypesList;

        public override void DefsLoaded() {
            FB_Factions.RemoveAll(x => true);
            FB_Factions.Add( FactionDef.Named("FactionBlender_Pirate") );
            FB_Factions.Add( FactionDef.Named("FactionBlender_Civil")  );

            Logger.Message("Injecting hair, backstory, and trader kinds to our factions");
            DefInjector.InjectMiscToFactions(FB_Factions);

            ProcessSettings();

            Logger.Message("Injecting pawn groups to our factions");
            DefInjector.InjectPawnKindDefsToFactions(FB_Factions);
        }

        public override void SettingsChanged() {
            lastSettingChanged = "";

            Logger.Message("Re-injecting pawn groups to our factions");
            DefInjector.InjectPawnKindDefsToFactions(FB_Factions);

            Logger.Message("Re-injecting pawn groups to our race settings");
            DefInjector.InjectPawnKindEntriesToRaceSettings();
        }

        public void ProcessSettings () {
            /*
             * Booleans
             */
            var bSettings = new List<string> {
                "EnableMixedStartingColonists",
                "EnableMixedRefugees",         
                "EnableMixedSlaves",           
                "EnableMixedWanderers",        
            };

            int order = 1;
            foreach (string sName in bSettings) {
                config[sName] = Settings.GetHandle<bool>(
                    sName, ("FB_" + sName + "_Title").Translate(), ("FB_" + sName + "_Description").Translate(), true
                );
                var setting = (SettingHandle<bool>)config[sName];
                setting.DisplayOrder = order;
                setting.OnValueChanged = x => { lastSettingChanged = ""; };
                order++;
            }

            /*
             * Float sliders
             */
            var fSettings = new List<string> {
                "FilterWeakerAnimalsRaids",
                "FilterSlowPawnsCaravans", 
                "PawnKindDifficultyLevel", 
            };
            var fDefaults = new Dictionary<string, float> {
                // Pirates will want stronger animals.  Bears are 200, and we definitely don't want to exclude
                // those.  Muffalos are 100, which is probably something a pirate raid shouldn't have.
                { "FilterWeakerAnimalsRaids",  150 },

                // MoveSpeed of 3.0 is still slower than the 4.6 humanlike pawns, but fast enough for them to not
                // lag too far behind.  Don't want to go beyond 4.0, as that hits stuff like Muffallos.
                { "FilterSlowPawnsCaravans",     3 },

                // This should just filter out the Archotech Centipede
                { "PawnKindDifficultyLevel",  5000 },
            };
            var fValidators = new Dictionary<string, SettingHandle.ValueIsValid> {
                { "FilterWeakerAnimalsRaids", Validators.FloatRangeValidator(0, 400)   },
                { "FilterSlowPawnsCaravans",  Validators.FloatRangeValidator(0, 4)     },
                { "PawnKindDifficultyLevel",  Validators.IntRangeValidator(100, 12000) },
            };
            var fDrawerParams = new Dictionary<string, List<float>> {  // ie: min, max, step
                { "FilterWeakerAnimalsRaids", new List<float> {0, 400, 10}      },
                { "FilterSlowPawnsCaravans",  new List<float> {0, 4, 0.1f}      },
                { "PawnKindDifficultyLevel",  new List<float> {100, 12000, 100} },
            };

            foreach (string sName in fSettings) {
                config[sName] = Settings.GetHandle<float>(
                    sName, ("FB_" + sName + "_Title").Translate(), ("FB_" + sName + "_Description").Translate(), fDefaults[sName], fValidators[sName]
                );

                var setting = (SettingHandle<float>)config[sName];
                setting.DisplayOrder = order;

                List<float> p = fDrawerParams[sName];
                float min  = p[0];
                float max  = p[1];
                float step = p[2];

                setting.CustomDrawer = rect => {
                    return DrawUtility.CustomDrawer_Filter(
                        rect, setting, false, min, max, step
                    );
                };
                setting.OnValueChanged = x => { lastSettingChanged = sName; };
                order += 2;
            }

            /*
             * Strings
             */
            var sSettings = new List<string> {
                "ExcludedFactionTypes",
            };
            var sDefaults = new Dictionary<string, string> {
                // No Vampires: Too many post-XML modifications and they tend to burn up on entry, anyway
                // No Star Vampires: They are loners that attack ANYBODY on contact, including their own faction
                { "ExcludedFactionTypes", "ROMV_Sabbat, ROM_StarVampire" },
            };

            foreach (string sName in sSettings) {
                config[sName] = Settings.GetHandle<string>(
                    sName, ("FB_" + sName + "_Title").Translate(), ("FB_" + sName + "_Description").Translate(), sDefaults[sName]
                );

                var setting = (SettingHandle<string>)config[sName];
                setting.DisplayOrder = order;
                // XXX: You need to actually hit Enter to see the filtered list.  Need an onClick here somehow.
                setting.OnValueChanged = x => { lastSettingChanged = sName; };
                order += 2;
            }

            /*
             * Filter Displays
             */
            List<PawnKindDef> fullPawnKindList = DefDatabase<PawnKindDef>.AllDefs.ToList();

            var fltSettings = new List<string> {
                "fwarFilterDisplay",
                "fspcFilterDisplay",
                "pkdlFilterDisplay",
                 "eftFilterDisplay",
            };
            var fltAffected = new Dictionary<string, string> {
                { "fwarFilterDisplay", "FilterWeakerAnimalsRaids" },
                { "fspcFilterDisplay", "FilterSlowPawnsCaravans"  },
                { "pkdlFilterDisplay", "PawnKindDifficultyLevel"  },
                {  "eftFilterDisplay", "ExcludedFactionTypes"     },
            };
            var fltDrawers = new Dictionary<string, SettingHandle.DrawCustomControl> {
                { "fwarFilterDisplay", rect => {
                    return DrawUtility.CustomDrawer_FilteredPawnKinds(
                        rect, config["fwarFilterDisplay"], fullPawnKindList,
                        (pawn => FilterPawnKindDef(pawn, "combat", "FilterWeakerAnimalsRaids", (int)((SettingHandle<float>)config["FilterWeakerAnimalsRaids"]).Value) == null),
                        (list => { list.SortBy(pawn => pawn.combatPower, pawn => pawn.defName); }),
                        (pawn => pawn.combatPower.ToString("N0"))
                    );
                } },
                { "fspcFilterDisplay", rect => {
                    return DrawUtility.CustomDrawer_FilteredPawnKinds(
                        rect, config["fspcFilterDisplay"], fullPawnKindList,
                        (pawn => FilterPawnKindDef(pawn, "trade", "FilterSlowPawnsCaravans") == null),
                        (list => { list.SortBy(pawn => pawn.race.GetStatValueAbstract(StatDefOf.MoveSpeed), pawn => pawn.defName); }),
                        (pawn => pawn.race.GetStatValueAbstract(StatDefOf.MoveSpeed).ToString("F2"))
                    );
                } },
                { "pkdlFilterDisplay", rect => {
                    return DrawUtility.CustomDrawer_FilteredPawnKinds(
                        rect, config["pkdlFilterDisplay"], fullPawnKindList,
                        (pawn => FilterPawnKindDef(pawn, "global", "PawnKindDifficultyLevel") == null),
                        (list => { list.SortBy(pawn => pawn.combatPower, pawn => pawn.defName); }),
                        (pawn => pawn.combatPower.ToString("N0"))
                    );
                } },
                {  "eftFilterDisplay", rect => {
                    return DrawUtility.CustomDrawer_FilteredPawnKinds(
                        rect, config["eftFilterDisplay"], fullPawnKindList,
                        (pawn => FilterPawnKindDef(pawn, "global", "ExcludedFactionTypes") == null),
                        (list => { list.SortBy(pawn => pawn.defaultFactionType != null ? pawn.defaultFactionType.defName : "", pawn => pawn.defName); }),
                        (pawn => pawn.defaultFactionType != null ? pawn.defaultFactionType.defName : "")
                    );
                } },
            };

            order = 6;
            foreach (string sName in fltSettings) {
                config[sName] = Settings.GetHandle<float>(sName, "", "", 0);

                var setting = (SettingHandle<float>)config[sName];
                setting.Unsaved = true;
                setting.DisplayOrder = order;
                setting.CustomDrawer = fltDrawers[sName];
                setting.VisibilityPredicate = delegate { return lastSettingChanged == fltAffected[sName]; };
                order += 2;
            }
        }

        public bool? FilterPawnKindDef(PawnKindDef pawn, string filterType, string watchSetting, int minCombatPower = 50) {
            bool? nil = new bool?();
            RaceProperties race = pawn.RaceProps;

            /*
             * DEBUG
             *
            string msg = pawn.defName;
            msg += " (" + pawn.combatPower + "/" + race.baseBodySize + ") --> ";
            if (race.Animal)     msg += "Animal, ";
            if (race.ToolUser)   msg += "ToolUser, ";
            if (race.Humanlike)  msg += "Humanlike, ";
            if (pawn.isFighter)  msg += "Fighter, ";
            if (pawn.trader)     msg += "Trader, ";
            if (race.packAnimal) msg += "PackAnimal, ";
            if (race.predator)   msg += "Predator, ";
            if (isRanged)        msg += "Ranged, ";
            if (!isRanged)       msg += "Melee, ";
            if (isSniper)        msg += "Sniper, ";
            if (isHeavyWeapons)  msg += "Heavy Weapons, ";

            msg += "Speed: " + pawn.race.GetStatValueAbstract(StatDefOf.MoveSpeed);
            if (pawn.combatPower > 1000) Logger.Message(msg);
             */

            // Global filters //

            /* True Story: Sarg Bjornson (Genetic Rim author) added Archotech Centipedes and somebody ended up
             * fighting one in a FB raid the same day.  Amusing, but, in @Extinction's words, "a fight of
             * apocalyptic proportions".
             */
            if (pawn.combatPower > ((SettingHandle<float>)config["PawnKindDifficultyLevel"]).Value)
                return watchSetting == "PawnKindDifficultyLevel" ? nil : false;

            // Filter by defaultFactionType
            if (pawn.defaultFactionType != null) {
                foreach (string factionDefName in excludedFactionTypesList) {
                    if (pawn.defaultFactionType.defName == factionDefName)
                        return watchSetting == "ExcludedFactionTypes" ? nil : false;
                }
            }

            // Combat filters //
            if (filterType == "combat") {
                // Gotta fight if you're in a combat raid
                if (!pawn.isFighter && !race.predator) return false;

                // If it's an animal, make sure Vegeta agrees with the power level
                if (((SettingHandle<float>)config["FilterWeakerAnimalsRaids"]).Value > 0) {
                    if (race.Animal && pawn.combatPower < minCombatPower)
                        return watchSetting == "FilterWeakerAnimalsRaids" ? nil : false;
                }
            }
            // Trade filters //
            else if (filterType == "trade") {
                // Enforce a minimum speed.  Trader pawns shouldn't get left too far behind, especially pack animals.
                if ((pawn.trader || race.packAnimal || FilterPawnKindDef(pawn, "combat", minCombatPower)) &&
                    pawn.race.GetStatValueAbstract(StatDefOf.MoveSpeed) < ((SettingHandle<float>)config["FilterSlowPawnsCaravans"]).Value
                )
                    return watchSetting == "FilterSlowPawnsCaravans" ? nil : false;
            }
            
            return true;
        }

        // If we're not watching a setting, the three-way return (true, null, false) simplifies to just true/false
        public bool FilterPawnKindDef(PawnKindDef pawn, string filterType, int minCombatPower = 50) {
            bool? ret = FilterPawnKindDef(pawn, filterType, "", minCombatPower);
            return ret != null && ret == true;
        }
    }
}
