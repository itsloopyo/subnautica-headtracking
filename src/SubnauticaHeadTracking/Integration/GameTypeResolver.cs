using System;
using System.Reflection;

namespace SubnauticaHeadTracking.Integration
{
    /// <summary>
    /// Centralizes reflection lookups for Subnautica game types.
    /// Call EnsureSearched() before accessing any field. All lookups run once
    /// and are cached in static fields for the lifetime of the process.
    /// </summary>
    internal static class GameTypeResolver
    {
        private static bool _searched;

        // Player
        public static Type PlayerType { get; private set; }
        public static FieldInfo PlayerMainField { get; private set; }
        public static FieldInfo MotorModeField { get; private set; }
        public static PropertyInfo MotorModeProp { get; private set; }

        // uGUI_MainMenu
        public static Type MainMenuType { get; private set; }
        public static FieldInfo MainMenuMainField { get; private set; }

        // IngameMenu
        public static Type IngameMenuType { get; private set; }
        public static FieldInfo IngameMenuMainField { get; private set; }
        public static FieldInfo IngameMenuSelectedField { get; private set; }

        // HandReticle
        public static Type HandReticleType { get; private set; }
        public static FieldInfo HandReticleMainField { get; private set; }

        // PDA
        public static Type PDAType { get; private set; }
        public static FieldInfo PDAIsInUseField { get; private set; }
        public static PropertyInfo PDAIsInUseProp { get; private set; }

        // PlayerMask
        public static Type PlayerMaskType { get; private set; }

        // uGUI_Pings
        public static Type PingsType { get; private set; }
        public static FieldInfo PingCanvasField { get; private set; }

        /// <summary>
        /// Attempts to resolve all game types from Assembly-CSharp.
        /// Safe to call every frame — exits immediately once resolved.
        /// Does not mark as searched until Player type is found (the fundamental type).
        /// </summary>
        public static void EnsureSearched()
        {
            if (_searched) return;

            PlayerType = FindType("Player");
            if (PlayerType == null) return;

            _searched = true;

            PlayerMainField = PlayerType.GetField("main",
                BindingFlags.Public | BindingFlags.Static);
            MotorModeField = PlayerType.GetField("motorMode",
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
            if (MotorModeField == null)
            {
                MotorModeProp = PlayerType.GetProperty("motorMode",
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
            }

            MainMenuType = FindType("uGUI_MainMenu");
            if (MainMenuType != null)
            {
                MainMenuMainField = MainMenuType.GetField("main",
                    BindingFlags.Public | BindingFlags.Static);
            }

            IngameMenuType = FindType("IngameMenu");
            if (IngameMenuType != null)
            {
                IngameMenuMainField = IngameMenuType.GetField("main",
                    BindingFlags.Public | BindingFlags.Static);
                IngameMenuSelectedField = IngameMenuType.GetField("selected",
                    BindingFlags.Public | BindingFlags.Instance);
            }

            HandReticleType = FindType("HandReticle");
            if (HandReticleType != null)
            {
                HandReticleMainField = HandReticleType.GetField("main",
                    BindingFlags.Public | BindingFlags.Static);
            }

            PDAType = FindType("PDA");
            if (PDAType != null)
            {
                PDAIsInUseField = PDAType.GetField("isInUse",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (PDAIsInUseField == null)
                {
                    PDAIsInUseProp = PDAType.GetProperty("isInUse",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                }
            }

            PlayerMaskType = FindType("PlayerMask");

            PingsType = FindType("uGUI_Pings");
            if (PingsType != null)
            {
                PingCanvasField = PingsType.GetField("pingCanvas",
                    BindingFlags.Public | BindingFlags.Instance);
            }
        }

        private static Type FindType(string name)
        {
            var type = Type.GetType(name + ", Assembly-CSharp");
            if (type != null) return type;

            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                type = asm.GetType(name);
                if (type != null) return type;
            }
            return null;
        }
    }
}
