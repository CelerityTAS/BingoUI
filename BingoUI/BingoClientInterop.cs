using System;
using System.Collections;
using System.Reflection;

namespace Celeste.Mod.BingoUI {
    public class BingoClientInterop {
        public static BingoClientInterop Instance { get; private set; }

        public static bool isBingoClientLoaded;
        public static Type BingoClientType;
        public static Type BingoClientInstanceType;

        protected object BingoClientInstance;


        private static readonly EverestModuleMetadata bingoClientVersionMetadata = new EverestModuleMetadata() {
            Name = "BingoClient",
            Version = new Version(0, 1, 16)
        };

        public static void Initialise() {
            isBingoClientLoaded = Everest.Loader.DependencyLoaded(bingoClientVersionMetadata);
            Logger.Log(LogLevel.Warn, "BingoUI", "Optional Dependency 'BingoClient' is " + ((isBingoClientLoaded ? "loaded" : "not loaded")));
            if (Everest.Loader.TryGetDependency(bingoClientVersionMetadata, out EverestModule bingoClientModule)) {
                Assembly bingoClientAssembly = bingoClientModule.GetType().Assembly;
                BingoClientType = bingoClientAssembly.GetType("Celeste.Mod.BingoClient.BingoClient");
                FieldInfo obj = BingoClientType.GetField("Instance", BindingFlags.Static | BindingFlags.Public);
                BingoClientInstanceType = obj.FieldType;
            }
            Instance = new BingoClientInterop();
        }

        public object GetInstance() {
            if (BingoClientInstance != null) return BingoClientInstance;
            FieldInfo obj = BingoClientType.GetField("Instance", BindingFlags.Static | BindingFlags.Public);
            BingoClientInstance = obj.GetValue(null);
            return BingoClientInstance;
        }

        public bool isConnected() {
            object BingoClientInstance = GetInstance();
            if (!isBingoClientLoaded || BingoClientInstance == null) return false;
            return (bool)(BingoClientType?.GetField("Connected")?.GetValue(BingoClientInstance) ?? false);
        }

        public int GetObjectiveCount() {
            if (!isBingoClientLoaded || BingoClientInstance == null || BingoClientInstanceType == null) {
                Logger.Log(LogLevel.Error, "BingoUI", "BingoClient was not loaded or initialised");
                return 0;
            }
            MethodInfo ScoreFunction = BingoClientInstanceType
                        .GetMethod("Score",
                            BindingFlags.Public | BindingFlags.Instance,
                            null,
                            CallingConventions.Any,
                            new Type[] { },
                            null);
            if (ScoreFunction == null) {
                Logger.Log(LogLevel.Error, "BingoUI", "Did not find the function Score in BingoClient");
                return 0;
            }
            var ScoreReturn = ScoreFunction.Invoke(BingoClientInstance, new object[] { });
            if (ScoreReturn == null) {
                Logger.Log(LogLevel.Error, "BingoUI", "The Score function of BingoClient returned null");
                return 0;
            }
            Type ScoreType = ScoreFunction.ReturnType;
            Type TupleType = ScoreType.GetGenericArguments()[0];
            PropertyInfo ScoreProperty = TupleType.GetProperty("Item2");

            if (ScoreProperty == null) {
                Logger.Log(LogLevel.Error, "BingoUI", "The Score function of BingoClient did not return a tuple, SOMEHOW");
                return 0;
            }
            int ObjectiveCount = 0;
            foreach (var item in (IEnumerable)ScoreReturn) {
                Logger.Log(LogLevel.Debug, "BingoUI", item.ToString());
                ObjectiveCount += (int)ScoreProperty.GetValue(item);
            }
            return ObjectiveCount;
        }
    }
}
