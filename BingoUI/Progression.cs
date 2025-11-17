using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FMOD.Studio;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Monocle;
using MonoMod.RuntimeDetour;
using MonoMod.Utils;
using MonoMod.Cil;
using Celeste.Editor;

namespace Celeste.Mod.BingoUI {
    public static class CustomProgression {
        public static bool IsAssistSkipping = false; // HACK HACK HACK HACK
        private static Hook ModeSetterHook, ModeGetterHook;

        public static void Load() {
            On.Celeste.SaveData.StartSession += RegisterEnteringChapter;
            On.Celeste.SaveData.RegisterCompletion += CustomLevelUnlock;
            On.Celeste.OuiChapterSelect.Update += CustomAssistEnable;
            On.Celeste.OuiChapterSelectIcon.AssistModeUnlock += CustomAssist;
            On.Celeste.Audio.Play_string += Audio_Play_string;
            On.Celeste.OuiChapterSelectIcon.Render += CustomIconRender;
            On.Celeste.OuiFileSelectSlot.OnNewGameSelected += AssignFileProgression;
            On.Celeste.OuiFileSelectSlot.Render += ShowBingoIcon;
            On.Celeste.OuiChapterPanel.Reset += CustomModeUnlock;
            On.Celeste.OuiChapterPanel.IsStart += DontAlwaysAdvance;
            IL.Celeste.HeartGemDoor.ctor += HeartGateNumbers;

            ModeSetterHook = new Hook(
                    typeof(OuiChapterPanel).GetProperty("option", BindingFlags.Instance | BindingFlags.NonPublic).GetSetMethod(true),
                    new Action<Action<OuiChapterPanel, int>, OuiChapterPanel, int>(SetModeCorrectly));
            ModeGetterHook = new Hook(
                    typeof(OuiChapterPanel).GetProperty("option", BindingFlags.Instance | BindingFlags.NonPublic).GetGetMethod(true),
                    new Func<Func<OuiChapterPanel, int>, OuiChapterPanel, int>(GetModeCorrectly));
        }

        public static void Unload() {
            On.Celeste.SaveData.StartSession -= RegisterEnteringChapter;
            On.Celeste.SaveData.RegisterCompletion -= CustomLevelUnlock;
            On.Celeste.OuiChapterSelect.Update -= CustomAssistEnable;
            On.Celeste.OuiChapterSelectIcon.AssistModeUnlock -= CustomAssist;
            On.Celeste.Audio.Play_string -= Audio_Play_string;
            On.Celeste.OuiChapterSelectIcon.Render -= CustomIconRender;
            On.Celeste.OuiFileSelectSlot.OnNewGameSelected -= AssignFileProgression;
            On.Celeste.OuiFileSelectSlot.Render -= ShowBingoIcon;
            On.Celeste.OuiChapterPanel.Reset -= CustomModeUnlock;
            On.Celeste.OuiChapterPanel.IsStart -= DontAlwaysAdvance;
            IL.Celeste.HeartGemDoor.ctor -= HeartGateNumbers;

            ModeSetterHook?.Dispose();
            ModeSetterHook = null;
        }

        private static void SetModeCorrectly(Action<OuiChapterPanel, int> orig, OuiChapterPanel self, int option) {
            if (BingoModule.SaveData.CustomProgression == ProgressionType.None || !(bool)BingoUtils.GetInstanceField(typeof(OuiChapterPanel), self, "selectingMode")) {
                orig(self, option);
                return;
            }

            var modes = (IList)BingoUtils.GetInstanceField(typeof(OuiChapterPanel), self, "modes");
            var mode = modes[option];
            var id = (string)BingoUtils.GetInstanceField(typeof(OuiChapterPanel).GetNestedType("Option", BindingFlags.NonPublic), mode, "ID");
            switch (id) {
                case "A":
                    self.Area.Mode = AreaMode.Normal;
                    break;
                case "B":
                    self.Area.Mode = AreaMode.BSide;
                    break;
                case "C":
                    self.Area.Mode = AreaMode.CSide;
                    break;
                default:
                    throw new Exception("what in the name of the lord did you do to OuiChapterPanel");
            }
        }

        private static int GetModeCorrectly(Func<OuiChapterPanel, int> orig, OuiChapterPanel self) {
            if (BingoModule.SaveData.CustomProgression == ProgressionType.None || !(bool)BingoUtils.GetInstanceField(typeof(OuiChapterPanel), self, "selectingMode")) {
                return orig(self);
            }

            string search;
            switch (self.Area.Mode) {
                case AreaMode.Normal:
                    search = "A";
                    break;
                case AreaMode.BSide:
                    search = "B";
                    break;
                case AreaMode.CSide:
                    search = "C";
                    break;
                default:
                    throw new Exception("what in the name of the lord did you do to AreaMode");
            }

            var modes = (IList)BingoUtils.GetInstanceField(typeof(OuiChapterPanel), self, "modes");
            for (var i = 0; i < modes.Count; i++) {
                var id = (string)BingoUtils.GetInstanceField(typeof(OuiChapterPanel).GetNestedType("Option", BindingFlags.NonPublic), modes[i], "ID");
                if (id == search) {
                    return i;
                }
            }
            throw new Exception("what in the name of the lord did you do do this poor class");
        }

        private static bool DontAlwaysAdvance(On.Celeste.OuiChapterPanel.orig_IsStart orig, OuiChapterPanel self, Overworld overworld, Overworld.StartMode start) {
            if (SaveData.Instance != null && SaveData.Instance.CurrentSession != null && start == Overworld.StartMode.AreaComplete && BingoModule.SaveData.CustomProgression != ProgressionType.None && SaveData.Instance.LevelSet == "Celeste") {
                var chstat = ChapterStatuses();
                var nextlvl = SaveData.Instance.CurrentSession.Area.ID + 1;
                if (nextlvl >= chstat.Count || chstat[nextlvl].Icon == ChapterIconStatus.Hidden || chstat[nextlvl].Icon == ChapterIconStatus.Skippable) {
                    start = Overworld.StartMode.AreaQuit;
                }
            }
            return orig(self, overworld, start);
        }

        private static void ShowBingoIcon(On.Celeste.OuiFileSelectSlot.orig_Render orig, OuiFileSelectSlot self) {
            if (self.SaveData != null && self.SaveData.HasFlag("BINGO")) {
                var archie = GFX.Game["ARCHIE"];
                var ease = Ease.CubeInOut((float)BingoUtils.GetInstanceField(typeof(OuiFileSelectSlot), self, "highlightEase"));
                archie.Draw(self.Position + new Vector2(0, -10f) + new Vector2(-800f, 0f) * ease, new Vector2(), Color.White, 0.25f, 0f, SpriteEffects.None);
                self.AssistModeEnabled = false;
                self.VariantModeEnabled = false;
            }
            orig(self);
        }

        private static void AssignFileProgression(On.Celeste.OuiFileSelectSlot.orig_OnNewGameSelected orig, OuiFileSelectSlot self) {
            orig(self);
            if (BingoModule.Settings.Enabled) {
                BingoModule.SaveData.CustomProgression = BingoModule.Settings.CustomProgression;
                SaveData.Instance.SetFlag("BINGO");
                if (BingoModule.SaveData.CustomProgression == ProgressionType.CheatMode)
                    SaveData.Instance.CheatMode = true;
            }
        }

        private static void CustomIconRender(On.Celeste.OuiChapterSelectIcon.orig_Render orig, OuiChapterSelectIcon self) {
            if (SaveData.Instance == null) {
                orig(self);
            } else {
                var origUnlock = SaveData.Instance.UnlockedAreas_Safe;
                if (BingoModule.Settings.Enabled && BingoModule.SaveData.CustomProgression != ProgressionType.None) {
                    SaveData.Instance.UnlockedAreas_Safe = self.Area;
                }
                orig(self);
                SaveData.Instance.UnlockedAreas_Safe = origUnlock;
            }
        }

        public static void RegisterEnteringChapter(On.Celeste.SaveData.orig_StartSession startSession, SaveData saveData, Session session) {
            var areas = BingoModule.SaveData.EnteredAreas;
            var currentArea = session.Area.ID;
            if (!areas.Contains(currentArea))
                areas.Add(currentArea);
            startSession(saveData, session);
        }

        public static void CustomLevelUnlock(On.Celeste.SaveData.orig_RegisterCompletion register, SaveData saveData, Session session) {
            if (saveData.LevelSet == "Celeste") {
                var areas = BingoModule.SaveData.ClearedAreas;
                if (!areas.Contains(session.Area.ID)) {
                    areas.Add(session.Area.ID);
                }
                bool clearedCore = false;
                int oldProgress = 0;
                if (session.Area.ID == 9) {
                    clearedCore = true;
                    oldProgress = saveData.UnlockedAreas_Safe;
                }
                register(saveData, session);
                if (session.Area.ID == 5 && BingoModule.SaveData.CustomProgression == ProgressionType.TournamentStandard && BingoModule.Settings.Enabled)
                    saveData.UnlockedAreas_Safe = 7;
                if (BingoModule.SaveData.CustomProgression == ProgressionType.TournamentStandard && BingoModule.Settings.Enabled && clearedCore && oldProgress != 9)
                    saveData.UnlockedAreas_Safe = oldProgress;
            } else {
                register(saveData, session);
            }
        }



        public static void CustomAssistEnable(On.Celeste.OuiChapterSelect.orig_Update update, OuiChapterSelect select) {
            if (SaveData.Instance == null || select == null || !BingoModule.Settings.Enabled || BingoModule.SaveData.CustomProgression == ProgressionType.None) {
                update(select);
                return;
            }

            List<OuiChapterSelectIcon> c = BingoUtils.GetInstanceField(typeof(OuiChapterSelect), select, "icons") as List<OuiChapterSelectIcon>;
            int b = (int)BingoUtils.GetInstanceProp(typeof(OuiChapterSelect), select, "area");
            if (b > 10 || IsAssistSkipping) {
                update(select);
                return;
            }

            bool menuLeft = false;
            bool menuRight = false;
            int newArea = -1;
            var bestUnlocked = 0;
            SaveData.Instance.AssistMode = false;

            var specialHide = c[0].IsHidden || !((Engine.Scene as Overworld).Current is OuiChapterSelect);

            var statuses = ChapterStatuses();
            for (var i = 1; i <= 10; i++) {
                switch (statuses[i].Icon) {
                    case ChapterIconStatus.Hidden:
                        if (!c[i].IsHidden) {
                            c[i].Hide();
                        }
                        break;
                    case ChapterIconStatus.Skippable:
                        if (c[i].IsHidden && !specialHide) {
                            c[i].Show();
                        }
                        bestUnlocked = i;
                        c[i].New = false;
                        c[i].AssistModeUnlockable = true;
                        SaveData.Instance.AssistMode = true;
                        break;
                    case ChapterIconStatus.Excited:
                        if (c[i].IsHidden && !specialHide) {
                            c[i].Show();
                        }
                        bestUnlocked = i;
                        c[i].New = true;
                        c[i].AssistModeUnlockable = false;
                        break;
                    case ChapterIconStatus.Shown:
                        if (c[i].IsHidden && !specialHide) {
                            c[i].Show();
                        }
                        bestUnlocked = i;
                        c[i].New = false;
                        c[i].AssistModeUnlockable = false;
                        break;
                }
            }

            SaveData.Instance.UnlockedAreas_Safe = bestUnlocked;

            bool disable = (bool)BingoUtils.GetInstanceField(typeof(OuiChapterSelect), select, "disableInput");
            bool display = (bool)BingoUtils.GetInstanceField(typeof(OuiChapterSelect), select, "display");
            float delay = (float)BingoUtils.GetInstanceField(typeof(OuiChapterSelect), select, "inputDelay");

            if (select.Focused && display && !disable && delay <= Engine.DeltaTime) {
                if (Input.MenuLeft.Pressed) {
                    for (var i = b - 1; i >= 0; i--) {
                        if (statuses[i].Icon != ChapterIconStatus.Hidden) {
                            menuLeft = true;
                            newArea = i;
                            Audio.Play("event:/ui/world_map/icon/roll_left");
                            break;
                        }
                    }
                }
                if (Input.MenuRight.Pressed) {
                    for (var i = b + 1; i <= 10; i++) {
                        if (statuses[i].Icon != ChapterIconStatus.Hidden) {
                            menuRight = true;
                            newArea = i;
                            Audio.Play("event:/ui/world_map/icon/roll_right");
                            break;
                        }
                    }
                }
            }

            var saved = SaveData.Instance.AssistMode;
            SaveData.Instance.AssistMode = false;
            if (b >= 9 && c[8].IsHidden && !Input.MenuUp.Pressed && !Input.MenuDown.Pressed) {
                select.orig_Update();
            } else {
                update(select);
            }
            SaveData.Instance.AssistMode = saved;



            if (menuLeft || menuRight) {
                c[newArea].Hovered(menuLeft ? -1 : 1);
                BindingFlags bindFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
        | BindingFlags.Static;
                FieldInfo delayField = typeof(OuiChapterSelect).GetField("inputDelay", bindFlags);
                delayField.SetValue(select, .15f);
                PropertyInfo setNewArea = typeof(OuiChapterSelect).GetProperty("area", bindFlags);
                setNewArea.SetValue(select, newArea);
                MethodInfo ease = typeof(OuiChapterSelect).GetMethod("EaseCamera", bindFlags);
                ease.Invoke(select, new object[] { });

                select.Overworld.Maddy.Hide(true);
            }
        }

        public static void CustomAssist(On.Celeste.OuiChapterSelectIcon.orig_AssistModeUnlock unlock, OuiChapterSelectIcon icon, Action onComplete) {
            if (!BingoModule.Settings.Enabled || BingoModule.SaveData.CustomProgression == ProgressionType.None) {
                unlock(icon, onComplete);
                return;
            }

            Overworld oui = Engine.Scene as Overworld;
            OuiChapterSelect cselect = oui?.GetUI<OuiChapterSelect>();
            if (cselect == null) {
                unlock(icon, onComplete);
                return;
            }

            DynData<OuiChapterSelectIcon> dd = new DynData<OuiChapterSelectIcon>(icon);

            if (dd.Get<bool?>("attemptingSkip") ?? false) {
                dd.Set<bool?>("attemptingSkip", false);
                Audio.Play("cas_event:/ui/world_map/icon/assist_skip");
                SaveData.Instance.AssistMode = false;
                IsAssistSkipping = true;
                unlock(icon, () => {
                    BingoModule.SaveData.SkipUsed = icon.Area;
                    BingoModule.SaveData.SkippedAreas.Add(icon.Area);
                    BingoModule.SaveData.EnteredAreas.Add(icon.Area);
                    IsAssistSkipping = false;
                    onComplete();
                });
                return;
            }

            dd.Set<bool?>("attemptingSkip", true);
            cselect.Focused = true;
            oui.ShowInputUI = true;
        }

        private static EventInstance Audio_Play_string(On.Celeste.Audio.orig_Play_string orig, string path) {
            if (SaveData.Instance == null || BingoModule.SaveData.CustomProgression == ProgressionType.None || !BingoModule.Settings.Enabled)
                return orig(path);
            if (path == "event:/ui/world_map/icon/assist_skip")
                return null;
            if (path == "cas_event:/ui/world_map/icon/assist_skip")
                return orig("event:/ui/world_map/icon/assist_skip");
            return orig(path);
        }

        private static void CustomModeUnlock(On.Celeste.OuiChapterPanel.orig_Reset orig, OuiChapterPanel self) {
            var reveal = false;
            AreaMode? mode = null;
            var origCheat = SaveData.Instance.CheatMode;
            if (BingoModule.SaveData.CustomProgression != ProgressionType.None && SaveData.Instance.LevelSet == "Celeste") {
                SaveData.Instance.CheatMode = true;
                var st = SaveData.Instance.CurrentSession_Safe?.OldStats;
                if (st != null) {
                    reveal = !st.Cassette && SaveData.Instance.Areas_Safe[SaveData.Instance.LastArea_Safe.ID].Cassette;
                    mode = SaveData.Instance.LastArea_Safe.Mode;
                }
            }
            orig(self);
            // invariant: all three tabs are present UNLESS reveal is true, in which case the bside is missing
            if (BingoModule.SaveData.CustomProgression != ProgressionType.None && SaveData.Instance.LevelSet == "Celeste") {
                SaveData.Instance.CheatMode = origCheat;

                var status = ChapterStatuses()[SaveData.Instance.LastArea_Safe.ID];
                var modes = (System.Collections.IList)BingoUtils.GetInstanceField(typeof(OuiChapterPanel), self, "modes");
                if (modes.Count != 1) {
                    if (!status.C) {
                        modes.RemoveAt(reveal ? 1 : 2);
                    }
                    if (!status.B) {
                        modes.RemoveAt(1);
                    }
                    if (!status.A) {
                        modes.RemoveAt(0);
                    }
                }

                if (mode != null) {
                    self.Area.Mode = mode.Value;
                } else {
                    if (status.A) {
                        self.Area.Mode = AreaMode.Normal;
                    } else if (status.B) {
                        self.Area.Mode = AreaMode.BSide;
                    } else if (status.C) {
                        self.Area.Mode = AreaMode.CSide;
                    } else {
                        throw new Exception("Programming error in BingoUI: progression unlocked a chapter without any modes");
                    }
                }
            }
        }

        public static List<ChapterStatus> ChapterStatuses() {
            var result = new List<ChapterStatus>();
            result.Add(new ChapterStatus { Icon = ChapterIconStatus.Shown, A = true });
            var cheat = SaveData.Instance.CheatMode || BingoModule.SaveData.CustomProgression == ProgressionType.CheatMode;
            var defaultIcon = cheat ? ChapterIconStatus.Shown : ChapterIconStatus.Hidden;
            for (var i = 1; i <= 10; i++) {
                result.Add(new ChapterStatus { Icon = defaultIcon, A = true, B = cheat || SaveData.Instance.Areas[i].Cassette, C = i != 8 && i != 10 && (cheat || SaveData.Instance.UnlockedModes > 2) });
            }
            if (cheat) {
                return result;
            }

            foreach (var i in BingoModule.SaveData.ClearedAreas) {
                result[i].Icon = ChapterIconStatus.Shown;
            }
            var levels = BingoModule.SaveData.ClearedAreas;
            var unlocked = BingoModule.SaveData.EnteredAreas;
            int skipsavailible;
            var skipped = BingoModule.SaveData.SkipUsed;
            var canUseCore = levels.Contains(9);
            var areas = SaveData.Instance.Areas;

            switch (BingoModule.SaveData.CustomProgression) {
                case ProgressionType.TournamentStandard:
                    result[0].Icon = ChapterIconStatus.Excited;
                    result[9].Icon = ChapterIconStatus.Shown;
                    if (levels.Contains(0)) {
                        result[0].Icon = ChapterIconStatus.Shown;
                        result[1].Icon = ChapterIconStatus.Excited;
                    }
                    if (levels.Contains(1) || skipped == 2) {
                        result[1].Icon = ChapterIconStatus.Shown;
                        result[2].Icon = ChapterIconStatus.Excited;
                    }
                    if (levels.Contains(2) || skipped == 3) {
                        result[2].Icon = ChapterIconStatus.Shown;
                        result[3].Icon = ChapterIconStatus.Excited;
                    }
                    if (levels.Contains(3) || skipped == 4) {
                        result[3].Icon = ChapterIconStatus.Shown;
                        result[4].Icon = ChapterIconStatus.Excited;
                    }
                    if (levels.Contains(4) || skipped == 5) {
                        result[4].Icon = ChapterIconStatus.Shown;
                        result[5].Icon = ChapterIconStatus.Excited;
                        result[10].Icon = ChapterIconStatus.Shown;
                    }
                    if (levels.Contains(5) || skipped == 6) {
                        result[5].Icon = ChapterIconStatus.Shown;
                        result[6].Icon = ChapterIconStatus.Excited;
                        result[7].Icon = ChapterIconStatus.Excited;
                    }
                    if (levels.Contains(7) || skipped == 8) {
                        result[7].Icon = ChapterIconStatus.Shown;
                        result[8].Icon = ChapterIconStatus.Excited;
                    }
                    if (BingoModule.SaveData.SkipUsed == -1) {
                        for (var i = 0; i <= 10; i++) {
                            if (result[i].Icon == ChapterIconStatus.Hidden) {
                                result[i].Icon = ChapterIconStatus.Skippable;
                                break;
                            }
                        }
                    }
                    break;
                case ProgressionType.BananaSplit:
                    Func<bool> coreCheck = () => {
                        if (canUseCore) {
                            canUseCore = false;
                            return true;
                        }
                        return false;
                    };
                    result[0].Icon = ChapterIconStatus.Excited;
                    result[9].Icon = ChapterIconStatus.Shown;
                    if (levels.Contains(0) || coreCheck()) {
                        result[0].Icon = ChapterIconStatus.Shown;
                        result[1].Icon = ChapterIconStatus.Excited;
                    }
                    if (levels.Contains(1) || skipped == 2 || coreCheck()) {
                        result[1].Icon = ChapterIconStatus.Shown;
                        result[2].Icon = ChapterIconStatus.Excited;
                        result[4].Icon = ChapterIconStatus.Excited;
                    }
                    if (levels.Contains(2) || levels.Contains(4) || skipped == 3 || coreCheck()) {
                        result[2].Icon = ChapterIconStatus.Shown;
                        result[4].Icon = ChapterIconStatus.Shown;
                        result[3].Icon = ChapterIconStatus.Excited;
                        result[5].Icon = ChapterIconStatus.Excited;
                        result[10].Icon = ChapterIconStatus.Shown;
                    }
                    if (levels.Contains(3) || levels.Contains(5) || skipped == 6 || coreCheck()) {
                        result[3].Icon = ChapterIconStatus.Shown;
                        result[5].Icon = ChapterIconStatus.Shown;
                        result[6].Icon = ChapterIconStatus.Excited;
                        result[7].Icon = ChapterIconStatus.Excited;
                    }
                    if (levels.Contains(6) || levels.Contains(7) || skipped == 8 || coreCheck()) {
                        result[6].Icon = ChapterIconStatus.Shown;
                        result[7].Icon = ChapterIconStatus.Shown;
                        result[8].Icon = ChapterIconStatus.Excited;
                    }
                    if (levels.Contains(8) || skipped == 8) {
                        result[8].Icon = ChapterIconStatus.Shown;
                    }
                    break;
                case ProgressionType.RockyRoad:
                    for (int j = 1; j <= 9; j++) {
                        var i = j - 1;
                        if (j == 8) {
                            result[8].A = levels.Contains(7);
                            continue;
                        } else if (j == 9) {
                            i = 8;
                        }

                        int above = ((i + 1) % 9) + 1;
                        int below = ((i + 8) % 9) + 1;
                        if (above == 8) {
                            above = 9;
                        }
                        if (below == 8) {
                            below = 7;
                        }

                        result[j].A = areas[j].Modes[1].Completed || areas[below].Modes[0].Completed || areas[above].Modes[0].Completed;
                        result[j].B = areas[j].Modes[0].Completed || areas[j].Modes[2].Completed || areas[below].Modes[1].Completed || areas[above].Modes[1].Completed || areas[j].Cassette;
                        result[j].C = areas[j].Modes[1].Completed || areas[below].Modes[2].Completed || areas[above].Modes[2].Completed;
                        result[j].Icon = result[j].A || result[j].B || result[j].C ? ChapterIconStatus.Shown : ChapterIconStatus.Hidden;
                    }
                    result[1].A = true;
                    result[1].Icon = ChapterIconStatus.Shown;
                    result[10].A = true;
                    result[10].Icon = ChapterIconStatus.Shown;
                    break;
                case ProgressionType.MintChip:
                    result[0].Icon = ChapterIconStatus.Excited;
                    result[9].Icon = ChapterIconStatus.Shown;
                    if (levels.Contains(0)) {
                        result[0].Icon = ChapterIconStatus.Shown;
                        result[1].Icon = ChapterIconStatus.Excited;
                    }
                    if (levels.Contains(1) || skipped == 2) {
                        result[1].Icon = ChapterIconStatus.Shown;
                        result[2].Icon = ChapterIconStatus.Excited;
                    }
                    if (levels.Contains(2) || skipped == 3) {
                        result[2].Icon = ChapterIconStatus.Shown;
                        result[3].Icon = ChapterIconStatus.Excited;
                        result[4].Icon = ChapterIconStatus.Excited;
                    }
                    if (levels.Contains(3)) {
                        result[3].Icon = ChapterIconStatus.Shown;
                    }
                    if (levels.Contains(4)) {
                        result[4].Icon = ChapterIconStatus.Shown;
                    }
                    if ((levels.Contains(3) && levels.Contains(4)) || (levels.Contains(3) && skipped == 5) || (levels.Contains(4) && skipped == 5)) {
                        result[3].Icon = ChapterIconStatus.Shown;
                        result[4].Icon = ChapterIconStatus.Shown;
                        result[5].Icon = ChapterIconStatus.Excited;
                        result[10].Icon = ChapterIconStatus.Shown;
                    }
                    if (levels.Contains(5) || skipped == 6) {
                        result[5].Icon = ChapterIconStatus.Shown;
                        result[6].Icon = ChapterIconStatus.Shown;
                        result[7].Icon = ChapterIconStatus.Excited;
                    }
                    if (levels.Contains(7) || skipped == 8) {
                        result[7].Icon = ChapterIconStatus.Shown;
                        result[8].Icon = ChapterIconStatus.Excited;
                    }
                    if (levels.Contains(8)) {
                        result[8].Icon = ChapterIconStatus.Shown;
                    }
                    if (BingoModule.SaveData.SkipUsed == -1) {
                        for (var i = 0; i <= 10; i++) {
                            if (result[i].Icon == ChapterIconStatus.Hidden) {
                                result[i].Icon = ChapterIconStatus.Skippable;
                                break;
                            }
                        }
                    }
                    break;
                case ProgressionType.Raspberry:
                    result[0].Icon = ChapterIconStatus.Excited;
                    if (!levels.Contains(0))
                        break;
                    var enteredNotClearedLevels = BingoModule.SaveData.EnteredAreas
                        .Except(levels)
                        .Where(l => l != 6 && l != 9 && l != 10)
                        .ToList();
                    var usedSkipsNumber = skipped == -1 ? 0 : 1;
                    for (var i = 0; i <= 8; i++) {
                        if (i == 2 || i == 6)
                            continue;
                        if (enteredNotClearedLevels.Count <= usedSkipsNumber && i != 8 && levels.Contains(1) == levels.Contains(2))
                            result[i].Icon = ChapterIconStatus.Excited;
                        else if (skipped == -1)
                            result[i].Icon = ChapterIconStatus.Skippable;
                        else
                            result[i].Icon = ChapterIconStatus.Hidden;
                    }
                    for (var i = 0; i <= 8; i++) {
                        if (i == 6 || enteredNotClearedLevels.Contains(i) || levels.Contains(i) || i == skipped)
                            result[i].Icon = ChapterIconStatus.Shown;
                    }
                    result[2].Icon = levels.Contains(1) ? ChapterIconStatus.Shown : ChapterIconStatus.Hidden;
                    if (levels.Contains(7))
                        result[8].Icon = ChapterIconStatus.Excited;
                    result[9].Icon = ChapterIconStatus.Shown;
                    result[10].Icon = ChapterIconStatus.Shown;
                    break;
                case ProgressionType.CheatMode:
                    throw new InvalidOperationException("cheat mode should have been caught earlier");
                case ProgressionType.DoubleSkip:
                    result[0].Icon = ChapterIconStatus.Excited;
                    result[9].Icon = ChapterIconStatus.Shown;
                    if (levels.Contains(0)) {
                        result[0].Icon = ChapterIconStatus.Shown;
                        result[1].Icon = ChapterIconStatus.Excited;
                    }
                    if (levels.Contains(1) || skipped == 2) {
                        result[1].Icon = ChapterIconStatus.Shown;
                        result[2].Icon = ChapterIconStatus.Excited;
                    }
                    if (levels.Contains(2) || skipped == 3) {
                        result[2].Icon = ChapterIconStatus.Shown;
                        result[3].Icon = ChapterIconStatus.Excited;
                    }
                    if (levels.Contains(3) || skipped == 4) {
                        result[3].Icon = ChapterIconStatus.Shown;
                        result[4].Icon = ChapterIconStatus.Excited;
                    }
                    if (levels.Contains(4) || skipped == 5) {
                        result[4].Icon = ChapterIconStatus.Shown;
                        result[5].Icon = ChapterIconStatus.Excited;
                        result[10].Icon = ChapterIconStatus.Shown;
                    }
                    if (levels.Contains(5) || skipped == 6) {
                        result[5].Icon = ChapterIconStatus.Shown;
                        result[6].Icon = ChapterIconStatus.Excited;
                        result[7].Icon = ChapterIconStatus.Excited;
                    }
                    if (levels.Contains(7) || skipped == 8) {
                        result[7].Icon = ChapterIconStatus.Shown;
                        result[8].Icon = ChapterIconStatus.Excited;
                    }
                    if (BingoModule.SaveData.SkippedAreas.Count < 2) {
                        for (var i = 0; i <= 10; i++) {
                            if (result[i].Icon == ChapterIconStatus.Hidden) {
                                result[i].Icon = ChapterIconStatus.Skippable;
                                break;
                            }
                        }
                    }
                    break;
                case ProgressionType.DampedOnline:
                    if (!BingoClientInterop.isBingoClientLoaded) {
                        throw new InvalidOperationException("You are using DampedOnline, which requires BingoClient");
                    }

                    if (!BingoClientInterop.Instance.isConnected()) {
                        result[8].Icon = ChapterIconStatus.Excited;
                        break;
                    }

                    int ObjectivesCompleted = BingoClientInterop.Instance.GetObjectiveCount();
                    List<int> newSkipUnlocks = new List<int>() { 0, 0, 3, 7, 11, 15, 19, 22, 22, 24 };
                    skipsavailible = newSkipUnlocks.Select(border => ObjectivesCompleted >= border).Sum((b) => b ? 1 : 0);

                    for (var i = 0; i <= 10; i++) {
                        result[i].Icon = ChapterIconStatus.Skippable;
                        if (unlocked.Count > skipsavailible) result[i].Icon = ChapterIconStatus.Hidden;
                        if (unlocked.Contains(i)) result[i].Icon = ChapterIconStatus.Shown;
                    }
                    break;
                case ProgressionType.None:
                    throw new InvalidOperationException("This function should not be called for vanilla progression");
                default:
                    throw new InvalidOperationException("forgot a case");
            }

            return result;
        }

        private static void HeartGateNumbers(ILContext il) {
            ILCursor cursor = new ILCursor(il);

            if (!cursor.TryGotoNext(MoveType.Before, instr => instr.MatchStfld<HeartGemDoor>("Requires"))) {
                throw new Exception("Couldn't patch heart door");
            }
            cursor.EmitDelegate<Func<int, int>>(CheckRequiredHearts);
        }

        private static int CheckRequiredHearts(int orig) {
            Logger.Log(LogLevel.Warn, "DEBUG", Engine.Scene.ToString());
            Logger.Log(LogLevel.Warn, "DEBUG", Engine.NextScene.ToString());
            var lvl = Engine.Scene as Level ?? Engine.NextScene as Level;
            var loader = Engine.Scene as LevelLoader ?? Engine.NextScene as LevelLoader;
            var editor = Engine.Scene as MapEditor ?? Engine.NextScene as MapEditor;
            AreaKey area;
            if (lvl != null) {
                area = lvl.Session.Area;
            } else if (loader != null) {
                var session = (Session)typeof(LevelLoader).GetField("session", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(loader);
                area = session.Area;
                if (area == null) {
                    throw new Exception("Hey. What the fuck.");
                }
            } else if (editor != null) {
                area = (AreaKey)typeof(MapEditor).GetField("area", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            } else {
                throw new Exception("How are you loading this heart gate?");
            }
            if (!BingoModule.Settings.Enabled || area.ID != 9) {
                return orig;
            }

            switch (area.Mode) {
                case AreaMode.Normal:
                    return BingoModule.Settings.CoreAHearts;
                case AreaMode.BSide:
                    return BingoModule.Settings.CoreBHearts;
                case AreaMode.CSide:
                    return BingoModule.Settings.CoreCHearts;
                default:
                    throw new Exception("What kind of mode is this?");
            }
        }
    }

    public class ChapterStatus {
        public ChapterIconStatus Icon;
        public bool A, B, C;
    }

    public enum ChapterIconStatus {
        Hidden,
        Skippable,
        Excited,
        Shown,
    }
}
