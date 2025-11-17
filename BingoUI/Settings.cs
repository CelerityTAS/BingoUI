namespace Celeste.Mod.BingoUI {
    public enum ProgressionType {
        None,
        TournamentStandard,
        CheatMode,
        BananaSplit,
        RockyRoad,
        MintChip,
        Raspberry,
        DoubleSkip,
        DampedOnline,
    }

    public class BingoSettings : EverestModuleSettings {
        private bool enabled = true;
        public bool Enabled { get { return enabled; } set { if (value && BingoModule.CurrentLevel != null) BingoModule.LevelSetup(); else BingoModule.LevelTeardown(); enabled = value; } }

        public ProgressionType CustomProgression { get; set; } = ProgressionType.TournamentStandard;

        [SettingName("BINGO_UI_PREVENT_PROLOGUE_CUTSCENE_SKIPS")]
        public bool PreventPrologueCutsceneSkips { get; set; } = true;

        public bool SkipChapterComplete { get; set; } = true;

        public bool SkipTallyWithConfirm { get; set; }

        public bool AutoEnableVariants { get; set; } = true;

        public bool ShowChapterBerryCount { get; set; } = true;

        public bool HideVariantsExceptGrabless { get; set; }

        public bool HideControls { get; set; }

        [SettingRange(0, 23)]
        public int CoreAHearts { get; set; } = 4;

        [SettingRange(0, 23)]
        public int CoreBHearts { get; set; } = 15;

        [SettingRange(0, 23)]
        public int CoreCHearts { get; set; } = 23;
    }
}
