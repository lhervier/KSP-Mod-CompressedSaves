using System;
using HarmonyLib;
using UnityEngine;

namespace com.github.lhervier.ksp.mod.compressedsaves
{
    /// <summary>
    /// Installs the patches once, when KSP starts: from then on, save games and craft files are read whether
    /// compressed or not, and written compressed when the settings say so.
    /// </summary>
    [KSPAddon(KSPAddon.Startup.Instantly, true)]
    public class KSPModCompressedSaves : MonoBehaviour
    {
        private const string HarmonyId = "com.github.lhervier.ksp.mod.compressedsaves";

        private void Start()
        {
            Settings.Load();
            Log.Info($"Version {typeof(KSPModCompressedSaves).Assembly.GetName().Version}, " +
                $"compress {Settings.Compress}, log level {Log.Level}");
            Harmony harmony = new Harmony(HarmonyId);
            try
            {
                ConfigNodePatches.Install(harmony, Settings.Compress);
                Log.Info("Save and craft files patched");
            }
            catch (Exception e)
            {
                Log.Error($"Could not patch ConfigNode: compressed saves cannot be read, nothing gets compressed: {e}");
                harmony.UnpatchAll(HarmonyId);
                return;
            }
            Install("SceneLoadSpeedBoost", () => DirectReadPatches.InstallSceneLoadSpeedBoost(harmony));
            Install("Workshop export", () => DirectReadPatches.InstallWorkshopExport(harmony));
        }

        private static void Install(string what, Action install)
        {
            try
            {
                install();
            }
            catch (Exception e)
            {
                Log.Error($"Could not patch the {what}: {e}");
            }
        }
    }
}
