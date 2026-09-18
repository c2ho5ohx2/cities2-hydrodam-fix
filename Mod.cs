using System;
using Colossal.Logging;
using Game;
using Game.Modding;
using HarmonyLib;

namespace HydroReadbackFix
{
    public sealed class Mod : IMod
    {
        public const string HarmonyId =
            "summerborn.cities2.hydroreadbackfix";

        public const string Version = "0.1.0";

        public static readonly ILog Log =
            LogManager
                .GetLogger("HydroReadbackFix")
                .SetShowsErrorsInUI(false);

        private Harmony _harmony;

        public void OnLoad(UpdateSystem updateSystem)
        {
            Log.Info($"[HydroReadbackFix] v{Version} loading");

            try
            {
                _harmony = new Harmony(HarmonyId);

                if (HydroPatch.Install(_harmony))
                {
                    Log.Info(
                        "[HydroReadbackFix] Compatibility hooks installed");
                }
                else
                {
                    Log.Warn(
                        "[HydroReadbackFix] Required game methods were not found; " +
                        "no patch was installed");
                }
            }
            catch (Exception ex)
            {
                Log.Error(
                    $"[HydroReadbackFix] Failed during initialization: {ex}");
            }
        }

        public void OnDispose()
        {
            try
            {
                _harmony?.UnpatchAll(HarmonyId);
            }
            catch (Exception ex)
            {
                Log.Error(
                    $"[HydroReadbackFix] Error while removing patches: {ex}");
            }

            _harmony = null;

            Log.Info("[HydroReadbackFix] Unloaded");
        }
    }
}
