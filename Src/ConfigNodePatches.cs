using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;
using KSP.Localization;

namespace com.github.lhervier.ksp.mod.compressedsaves
{
    /// <summary>
    /// Patches the two methods every save game and craft file goes through. <c>ConfigNode.Load</c> reads a
    /// gzip file itself and hands back the same node a plain file would give; <c>ConfigNode.Save</c>
    /// compresses the file once written, when the settings ask for it.
    /// </summary>
    internal static class ConfigNodePatches
    {
        // KSPCommunityFixes' ConfigNodePerf skips value cleanup while it reads a save or a craft file: we do
        // the same around Parse, which it also replaces. Null when a KSPCommunityFixes version no longer has it.
        private static FieldInfo kspcfDoClean;

        /// <summary>
        /// Installs the patches. The one on <c>Load</c> always: compressed files stay readable even with
        /// compression off. The one on <c>Save</c> only when <paramref name="compress"/>.
        /// </summary>
        public static void Install(Harmony harmony, bool compress)
        {
            Type configNodePerf = AccessTools.TypeByName("KSPCommunityFixes.Performance.ConfigNodePerf");
            kspcfDoClean = configNodePerf == null ? null : AccessTools.Field(configNodePerf, "_doClean");
            if (kspcfDoClean != null)
            {
                Log.Info("KSPCommunityFixes' ConfigNodePerf found: compressed files are parsed by it");
            }
            else
            {
                Log.Warning("KSPCommunityFixes.Performance.ConfigNodePerf._doClean not found in this version of " +
                    "KSPCommunityFixes: compressed files are parsed with value cleanup on");
            }

            // First, so that KSPCommunityFixes' prefix, which reads the file itself, never sees a gzip file:
            // Harmony skips a bool prefix once an earlier one has returned false.
            HarmonyMethod loadPrefix = new HarmonyMethod(typeof(ConfigNodePatches), nameof(LoadPrefix))
            {
                priority = Priority.First
            };
            harmony.Patch(AccessTools.Method(typeof(ConfigNode), nameof(ConfigNode.Load),
                new[] { typeof(string), typeof(bool) }), prefix: loadPrefix);

            if (compress)
            {
                harmony.Patch(AccessTools.Method(typeof(ConfigNode), nameof(ConfigNode.Save),
                    new[] { typeof(string), typeof(string) }),
                    postfix: new HarmonyMethod(typeof(ConfigNodePatches), nameof(SavePostfix)));
            }
        }

        private static bool LoadPrefix(string fileFullName, bool bypassLocalization, ref ConfigNode __result)
        {
            if (!GzipFiles.IsSaveOrCraft(fileFullName) || !GzipFiles.IsGzip(fileFullName))
            {
                return true;
            }
            Stopwatch watch = Stopwatch.StartNew();
            try
            {
                string text = GzipFiles.ReadGzipText(fileFullName);
                long readMs = watch.ElapsedMilliseconds;
                __result = Parse(text);
                // As stock Load does after parsing.
                if (__result != null && Localizer.Instance != null && !bypassLocalization)
                {
                    Localizer.TranslateBranch(__result);
                }
                Log.Debug($"Read compressed {fileFullName}: {text.Length} chars, " +
                    $"decompressed in {readMs} ms, parsed in {watch.ElapsedMilliseconds - readMs} ms");
            }
            catch (Exception e)
            {
                Log.Error($"Could not read compressed {fileFullName}: {e}");
                __result = null;
            }
            return false;
        }

        private static void SavePostfix(string fileFullName, bool __result)
        {
            if (!__result || !GzipFiles.ShouldCompress(fileFullName))
            {
                return;
            }
            Stopwatch watch = Stopwatch.StartNew();
            try
            {
                GzipFiles.CompressInPlace(fileFullName);
                Log.Debug($"Compressed {fileFullName} in {watch.ElapsedMilliseconds} ms");
            }
            catch (Exception e)
            {
                Log.Warning($"Could not compress {fileFullName}, it stays plain: {e}");
            }
        }

        /// <summary>
        /// Parses the text of a save or a craft file into the node <c>ConfigNode.Load</c> would return.
        /// <c>ConfigNode.Parse</c> splits and cleans lines as <c>Load</c> does on stock, and goes to the same
        /// parser as <c>Load</c> with KSPCommunityFixes.
        /// </summary>
        private static ConfigNode Parse(string text)
        {
            if (kspcfDoClean == null)
            {
                return ConfigNode.Parse(text);
            }
            object doClean = kspcfDoClean.GetValue(null);
            kspcfDoClean.SetValue(null, false);
            try
            {
                return ConfigNode.Parse(text);
            }
            finally
            {
                kspcfDoClean.SetValue(null, doClean);
            }
        }
    }
}
