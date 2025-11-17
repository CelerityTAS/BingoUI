using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace Celeste.Mod.BingoUI {
    public class BingoSaveData : EverestModuleSaveData {
        public ProgressionType CustomProgression = ProgressionType.None;
        public int SeekersHit = 0;
        public int OshiroHits = 0;
        public int SnowballHits = 0;
        public int SkipUsed = -1;
        public List<Binoculars> BinocularsList = new List<Binoculars>();
        public List<Keys> KeysList = new List<Keys>();
        public List<int> ClearedAreas = new List<int>();
        public List<int> EnteredAreas = new List<int>(); //need to implement filling this
        public List<int> SkippedAreas = new List<int>();
    }

    public class Binoculars {
        public int areaID;
        public int areaMode;
        public Vector2 pos;

        public Binoculars() {
        }

        public Binoculars(AreaKey lv, Vector2 myPos) {
            areaID = lv.ID;
            areaMode = (int)lv.Mode;
            pos = myPos;
        }
    }

    public class Keys {
        public int areaID;
        public int areaMode;
        public EntityID entity;

        public Keys() {
        }

        public Keys(AreaKey lv, EntityID entity) {
            areaID = lv.ID;
            areaMode = (int)lv.Mode;
            this.entity = entity;
        }
    }
}
