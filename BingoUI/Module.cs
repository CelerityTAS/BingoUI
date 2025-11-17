using System;
using System.Collections.Generic;
using FMOD.Studio;
using Microsoft.Xna.Framework;

namespace Celeste.Mod.BingoUI {
    public class BingoModule : EverestModule {
        public static BingoModule Instance;
        public BingoModule() {
            Instance = this;
        }

        public override Type SettingsType => typeof(BingoSettings);
        public override Type SaveDataType => typeof(BingoSaveData);
        public static BingoSettings Settings => (BingoSettings)Instance._Settings;
        public static BingoSaveData SaveData => (BingoSaveData)Instance._SaveData;
        public static Level CurrentLevel;
        
        public override void Load() {
            OuiJournalBinoculars.Load();
            HideControls.Load();
            ProloguePauseDisable.Load();
            BingoCounting.Load();
            Archie.Load();
            SkipTallyWithConfirm.Load();
            Variants.Load();
            ExtraCounters.Load();
            CustomProgression.Load();

            On.Celeste.Level.LoadLevel += LoadLevel;
            On.Celeste.Level.UnloadLevel += UnloadLevel;
        }

        public override void Unload() {
            OuiJournalBinoculars.Unload();
            HideControls.Unload();
            ProloguePauseDisable.Unload();
            BingoCounting.Unload();
            Archie.Unload();
            SkipTallyWithConfirm.Unload();
            Variants.Unload();
            ExtraCounters.Unload();
            CustomProgression.Unload();

            On.Celeste.Level.LoadLevel -= LoadLevel;
            On.Celeste.Level.UnloadLevel -= UnloadLevel;
        }
        public override void Initialize() {
            base.Initialize();
            BingoClientInterop.Initialise();
        }

        public override void CreateModMenuSection(TextMenu menu, bool inGame, EventInstance snapshot) {
            base.CreateModMenuSection(menu, inGame, snapshot);

            foreach (var item in menu.Items) {
                if (!(item is TextMenu.Slider btn) || !btn.Label.StartsWith(Dialog.Clean("modoptions_bingo_customprogression"))) {
                    continue;
                }

                var messageHeader = new TextMenuExt.EaseInSubHeaderExt("xxx", false, menu) {
                    HeightExtra = 17f,
                    Offset = new Vector2(30, -5),
                };
                var origAction = btn.OnValueChange;
                btn.OnValueChange = (newValue) => {
                    if (origAction != null) {
                        origAction(newValue);
                    }
                    this.SetFlavorText(messageHeader, (ProgressionType)Enum.GetValues(typeof(ProgressionType)).GetValue(newValue));
                };
                this.SetFlavorText(messageHeader, (ProgressionType)Enum.GetValues(typeof(ProgressionType)).GetValue(btn.Index));

                menu.Insert(menu.Items.IndexOf(item) + 1, messageHeader);
                btn.OnEnter = () => messageHeader.FadeVisible = true;
                btn.OnLeave = () => messageHeader.FadeVisible = false;
                break;
            }
        }

        private void SetFlavorText(TextMenuExt.EaseInSubHeaderExt label, ProgressionType flavor) {
            var words = Dialog.Clean($"bingoui_progression_{flavor.ToString()}").Split(' ');
            var line = new List<string>();
            var lineWidth = 0;
            var text = "";
            foreach (var word in words) {
                line.Add(word);
                var wordMeasure = ActiveFont.Measure(word);
                lineWidth += (int)wordMeasure.X + 10;
                if (lineWidth > 1300) {
                    text += string.Join(" ", line) + "\n";
                    lineWidth = 0;
                    line.Clear();
                }
            }
            if (line.Count != 0) {
                text += string.Join(" ", line) + "\n";
            }
            label.Title = text.TrimEnd('\n');
        }

        public static void LevelSetup() {
            if (CurrentLevel == null) {
                return;
            }
            if (CurrentLevel.Tracker.GetEntity<TotalCollectableDisplay>() == null) {
                BingoCounting.CreateDisplayEntities();
            }
            if (CurrentLevel.Tracker.GetEntity<GrablessDisplay>() == null) {
                CurrentLevel.Add(new GrablessDisplay());
            }
        }

        public static void LevelTeardown() {
            if (CurrentLevel == null) {
                return;
            }

            BingoCounting.DestroyDisplayEntities();
            var grabless = CurrentLevel.Tracker.GetEntity<GrablessDisplay>();
            if (grabless != null) {
                CurrentLevel.Remove(grabless);
            }
        }

        private static void LoadLevel(On.Celeste.Level.orig_LoadLevel orig, Level level, Player.IntroTypes playerIntro, bool isFromLoader) {
            CurrentLevel = level;
            orig(level, playerIntro, isFromLoader);

            if (BingoModule.Settings.Enabled) {
                BingoModule.LevelSetup();
                if (BingoModule.Settings.AutoEnableVariants)
                    global::Celeste.SaveData.Instance.VariantMode = true;
                if (BingoModule.SaveData.CustomProgression != ProgressionType.None)
                    global::Celeste.SaveData.Instance.AssistMode = false;
            }
        }

        private void UnloadLevel(On.Celeste.Level.orig_UnloadLevel orig, Level level) {
            orig(level);
            CurrentLevel = null;
        }
    }
}
