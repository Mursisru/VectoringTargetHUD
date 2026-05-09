using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;

namespace VectoringTargetHUD_Engine
{
    [BepInPlugin("com.at747.nuclearoption.vectoringtargethud", "Vectoring Target HUD", "1.0.0")]
    internal sealed class VectoringTargetHUDPlugin : BaseUnityPlugin
    {
        internal static ConfigEntry<float> UpdateRateHz;
        internal static ConfigEntry<float> HoldWindowSeconds;
        internal static ConfigEntry<float> PositionSmoothing;
        internal static ConfigEntry<float> MaxScreenStepPx;
        internal static ConfigEntry<float> SwitchHysteresisPx;
        internal static ConfigEntry<float> NoseDotDistanceMeters;
        internal static ConfigEntry<float> NoseDotDistanceByRangeFactor;
        internal static ConfigEntry<float> NoseDotDistanceMaxMeters;
        internal static ConfigEntry<float> NoseDotDistanceMinMeters;
        internal static ConfigEntry<float> NearDistanceMeters;
        internal static ConfigEntry<float> NearDistanceScale;
        internal static ConfigEntry<float> LineThicknessPx;
        internal static ConfigEntry<float> LineLengthPx;
        internal static ConfigEntry<float> LineAlpha;
        internal static ConfigEntry<string> ShapeMode;
        internal static ConfigEntry<float> PrismBaseWidthPx;
        internal static ConfigEntry<float> PrismTipWidthPx;
        internal static ConfigEntry<float> PrismDepthSkew;
        internal static ConfigEntry<float> PrismAlphaGradient;
        internal static ConfigEntry<float> PrismMinLengthPx;
        internal static ConfigEntry<float> PrismBaseOffsetPx;
        internal static ConfigEntry<float> SpeedLengthFactor;
        internal static ConfigEntry<float> MaxSpeedForLength;
        internal static ConfigEntry<float> PerspectiveThicknessBoost;
        internal static ConfigEntry<float> MinLineLengthPx;
        internal static ConfigEntry<float> LiveColorR;
        internal static ConfigEntry<float> LiveColorG;
        internal static ConfigEntry<float> LiveColorB;
        internal static ConfigEntry<float> HoldColorR;
        internal static ConfigEntry<float> HoldColorG;
        internal static ConfigEntry<float> HoldColorB;
        internal static ConfigEntry<bool> DebugMode;

        private Harmony _harmony;

        private void Awake()
        {
            UpdateRateHz = Config.Bind("General", "UpdateRateHz", 20f, "Selection/telemetry update frequency.");
            HoldWindowSeconds = Config.Bind("General", "HoldWindowSeconds", 1f, "Hold time in seconds after temporary target loss.");
            PositionSmoothing = Config.Bind("General", "PositionSmoothing", 0.2f, "Line endpoint smoothing (0..1).");
            MaxScreenStepPx = Config.Bind("General", "MaxScreenStepPx", 260f, "Maximum endpoint movement per second in screen pixels.");
            SwitchHysteresisPx = Config.Bind("General", "SwitchHysteresisPx", 28f, "Switch hysteresis in pixels.");
            NoseDotDistanceMeters = Config.Bind("General", "NoseDotDistanceMeters", 35f, "Distance of invisible nose dot in front of target.");
            NoseDotDistanceByRangeFactor = Config.Bind("General", "NoseDotDistanceByRangeFactor", 0.02f, "Additional nose-dot distance per meter to camera.");
            NoseDotDistanceMaxMeters = Config.Bind("General", "NoseDotDistanceMaxMeters", 550f, "Upper clamp for dynamic nose-dot distance.");
            NoseDotDistanceMinMeters = Config.Bind("General", "NoseDotDistanceMinMeters", 20f, "Lower clamp for dynamic nose-dot distance.");
            NearDistanceMeters = Config.Bind("General", "NearDistanceMeters", 1200f, "Distance threshold where near scaling is strongest.");
            NearDistanceScale = Config.Bind("General", "NearDistanceScale", 0.55f, "Scale multiplier near target distance (0.1..1).");
            LineThicknessPx = Config.Bind("Visual", "LineThicknessPx", 2.5f, "HUD line thickness in pixels.");
            LineLengthPx = Config.Bind("Visual", "LineLengthPx", 90f, "HUD line length in pixels (constant).");
            LineAlpha = Config.Bind("Visual", "LineAlpha", 0.9f, "HUD line alpha.");
            ShapeMode = Config.Bind("Visual", "ShapeMode", "Prism", "Rendering mode: Prism or Line.");
            PrismBaseWidthPx = Config.Bind("Prism", "PrismBaseWidthPx", 18f, "Base width of the prism pointer.");
            PrismTipWidthPx = Config.Bind("Prism", "PrismTipWidthPx", 2.5f, "Tip width of the prism pointer.");
            PrismDepthSkew = Config.Bind("Prism", "PrismDepthSkew", 0.35f, "Depth skew factor for pseudo-3D.");
            PrismAlphaGradient = Config.Bind("Prism", "PrismAlphaGradient", 0.45f, "Alpha gradient amount from base to tip.");
            PrismMinLengthPx = Config.Bind("Prism", "PrismMinLengthPx", 10f, "Minimum visible length for prism.");
            PrismBaseOffsetPx = Config.Bind("Prism", "PrismBaseOffsetPx", 14f, "Forward screen-space offset from target center to prism base.");
            SpeedLengthFactor = Config.Bind("Visual", "SpeedLengthFactor", 0.35f, "Additional length from target speed (0..2).");
            MaxSpeedForLength = Config.Bind("Visual", "MaxSpeedForLength", 500f, "Speed at which speed-based length bonus reaches max.");
            PerspectiveThicknessBoost = Config.Bind("Visual", "PerspectiveThicknessBoost", 0.4f, "Thickness boost by camera-depth angle (0..2).");
            MinLineLengthPx = Config.Bind("Visual", "MinLineLengthPx", 8f, "Hide line if shorter than this.");
            LiveColorR = Config.Bind("Visual", "LiveColorR", 0.2f, "Live line red channel.");
            LiveColorG = Config.Bind("Visual", "LiveColorG", 1.0f, "Live line green channel.");
            LiveColorB = Config.Bind("Visual", "LiveColorB", 0.35f, "Live line blue channel.");
            HoldColorR = Config.Bind("Visual", "HoldColorR", 1.0f, "Hold line red channel.");
            HoldColorG = Config.Bind("Visual", "HoldColorG", 0.95f, "Hold line green channel.");
            HoldColorB = Config.Bind("Visual", "HoldColorB", 0.1f, "Hold line blue channel.");
            DebugMode = Config.Bind("Debug", "DebugMode", false, "Show current target and state text.");

            _harmony = new Harmony("com.at747.nuclearoption.vectoringtargethud.harmony");
            _harmony.PatchAll();
        }

        private void OnDestroy()
        {
            if (_harmony != null)
            {
                _harmony.UnpatchSelf();
                _harmony = null;
            }
        }

        [HarmonyPatch(typeof(FlightHud), "Awake")]
        private static class FlightHudAwakePatch
        {
            private static void Postfix(FlightHud __instance)
            {
                if (__instance == null)
                {
                    return;
                }

                if (__instance.GetComponent<TargetHudLineController>() == null)
                {
                    __instance.gameObject.AddComponent<TargetHudLineController>();
                }
            }
        }
    }
}
