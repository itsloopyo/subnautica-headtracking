using System;
using System.Reflection;
using CameraUnlock.Core.Reflection;
using UnityEngine;

namespace SubnauticaHeadTracking.Integration
{
    /// <summary>
    /// Centralizes reflection lookups for Subnautica game types.
    /// Call EnsureSearched() before accessing any member. All lookups run once, and every
    /// member read per frame is a compiled getter rather than FieldInfo/PropertyInfo.GetValue.
    /// A getter left null means the member was not found; that is logged once, here.
    /// </summary>
    internal static class GameTypeResolver
    {
        private const BindingFlags Instance = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        private const BindingFlags Static = BindingFlags.Public | BindingFlags.Static;

        private static bool _searched;

        // Player
        public static Type PlayerType { get; private set; }
        public static Func<object> PlayerMain { get; private set; }
        public static Func<object, int> MotorMode { get; private set; }
        public static Func<object, Component> PlayerPda { get; private set; }
        public static Func<object, GameObject> PlayerScubaMaskModel { get; private set; }

        // uGUI_MainMenu, IngameMenu
        public static Func<object> MainMenuMain { get; private set; }
        public static Func<object> IngameMenuMain { get; private set; }
        public static Func<object, object> IngameMenuSelected { get; private set; }

        // HandReticle
        public static Func<object> HandReticleMain { get; private set; }

        // PDA
        public static Func<object, object> PdaIsInUse { get; private set; }

        // PlayerMask
        public static Type PlayerMaskType { get; private set; }

        // uGUI_Pings
        public static Type PingsType { get; private set; }
        public static Func<object, RectTransform> PingCanvas { get; private set; }

        /// <summary>
        /// Attempts to resolve all game types from Assembly-CSharp.
        /// Safe to call every frame - exits immediately once resolved.
        /// Does not mark as searched until Player type is found (the fundamental type).
        /// </summary>
        public static void EnsureSearched()
        {
            if (_searched) return;

            PlayerType = FindType("Player");
            if (PlayerType == null) return;

            _searched = true;

            PlayerMain = StaticField(PlayerType, "main");
            FieldInfo motorMode = Field(PlayerType, "motorMode");
            if (motorMode != null) MotorMode = CompiledGetters.ForInstanceField<int>(motorMode);
            FieldInfo pda = Field(PlayerType, "pda");
            if (pda != null) PlayerPda = CompiledGetters.ForInstanceField<Component>(pda);
            // Player toggles this GameObject off whenever the player is not diving, and
            // FindObjectOfType skips inactive objects, so the mask is reached through it.
            FieldInfo scubaMask = Field(PlayerType, "scubaMaskModel");
            if (scubaMask != null) PlayerScubaMaskModel = CompiledGetters.ForInstanceField<GameObject>(scubaMask);

            Type mainMenu = FindType("uGUI_MainMenu");
            if (mainMenu != null) MainMenuMain = StaticField(mainMenu, "main");

            Type ingameMenu = FindType("IngameMenu");
            if (ingameMenu != null)
            {
                IngameMenuMain = StaticField(ingameMenu, "main");
                // Declared on uGUI_InputGroup as a property.
                IngameMenuSelected = Property(ingameMenu, "selected");
            }

            Type handReticle = FindType("HandReticle");
            if (handReticle != null) HandReticleMain = StaticField(handReticle, "main");

            Type pdaType = FindType("PDA");
            if (pdaType != null) PdaIsInUse = Property(pdaType, "isInUse");

            PlayerMaskType = FindType("PlayerMask");

            PingsType = FindType("uGUI_Pings");
            if (PingsType != null)
            {
                FieldInfo pingCanvas = Field(PingsType, "pingCanvas");
                if (pingCanvas != null) PingCanvas = CompiledGetters.ForInstanceField<RectTransform>(pingCanvas);
            }

            foreach (string missing in new[]
                     {
                         PlayerMain == null ? "Player.main" : null,
                         MotorMode == null ? "Player.motorMode (swim offset)" : null,
                         PlayerPda == null || PdaIsInUse == null ? "Player.pda / PDA.isInUse (PDA suppression)" : null,
                         PlayerScubaMaskModel == null || PlayerMaskType == null ? "Player.scubaMaskModel / PlayerMask (mask compensation)" : null,
                         MainMenuMain == null ? "uGUI_MainMenu.main" : null,
                         IngameMenuMain == null || IngameMenuSelected == null ? "IngameMenu.main / selected" : null,
                         HandReticleMain == null ? "HandReticle.main (reticle compensation)" : null,
                         PingsType == null || PingCanvas == null ? "uGUI_Pings.pingCanvas (ping compensation)" : null,
                     })
            {
                if (missing != null) HeadTrackingPlugin.ModLogger?.LogWarning("Game member not found: " + missing + " - that feature is disabled");
            }
        }

        private static FieldInfo Field(Type type, string name) => type.GetField(name, Instance);

        private static Func<object> StaticField(Type type, string name)
        {
            FieldInfo field = type.GetField(name, Static);
            return field == null ? null : CompiledGetters.ForStaticField(field);
        }

        private static Func<object, object> Property(Type type, string name)
        {
            PropertyInfo property = type.GetProperty(name, Instance);
            return property == null ? null : CompiledGetters.ForInstanceProperty(property);
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

        /// <summary>Player.main, or null while there is no live player.</summary>
        public static Component GetPlayer()
        {
            EnsureSearched();
            if (PlayerMain == null) return null;
            var player = PlayerMain() as Component;
            return player == null ? null : player;
        }
    }
}
