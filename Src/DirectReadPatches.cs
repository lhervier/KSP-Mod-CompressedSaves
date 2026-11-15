using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace com.github.lhervier.ksp.mod.compressedsaves
{
    /// <summary>
    /// Patches the two places that open a save or a craft file without <c>ConfigNode.Load</c>, so that they
    /// read a compressed file as they read a plain one.
    /// </summary>
    internal static class DirectReadPatches
    {
        private static readonly ConstructorInfo StreamReaderCtor =
            AccessTools.Constructor(typeof(StreamReader), new[] { typeof(string) });
        private static readonly MethodInfo OpenText = AccessTools.Method(typeof(GzipFiles), nameof(GzipFiles.OpenText));
        private static readonly MethodInfo FileCopy =
            AccessTools.Method(typeof(File), nameof(File.Copy), new[] { typeof(string), typeof(string) });
        private static readonly MethodInfo CopyDecompressed =
            AccessTools.Method(typeof(GzipFiles), nameof(GzipFiles.CopyDecompressed));

        private static int replaced;

        /// <summary>
        /// Patches KSPCommunityFixes' SceneLoadSpeedBoost when present: it reads the first lines of
        /// persistent.sfs to tell whether its copy in memory is still current. Unpatched, it never finds its
        /// marker in a compressed file and reloads the save from disk at every scene change.
        /// </summary>
        public static void InstallSceneLoadSpeedBoost(Harmony harmony)
        {
            MethodInfo method = AccessTools.Method("KSPCommunityFixes.SceneLoadSpeedBoost:LoadSFSFileCached");
            if (method == null)
            {
                Log.Warning("KSPCommunityFixes.SceneLoadSpeedBoost.LoadSFSFileCached not found in this version of " +
                    "KSPCommunityFixes: with SceneLoadSpeedBoost on, it may reload compressed saves from disk");
                return;
            }
            Patch(harmony, method, nameof(StreamReaderTranspiler), "SceneLoadSpeedBoost reads compressed saves");
        }

        /// <summary>
        /// Patches the export of a craft to the Steam Workshop, which copies the craft file to upload it: the
        /// copy is decompressed, so that players without the mod can load it.
        /// </summary>
        public static void InstallWorkshopExport(Harmony harmony)
        {
            MethodInfo method = AccessTools.Method(typeof(EditorLogic), "preupdateSteamItem");
            Patch(harmony, method, nameof(FileCopyTranspiler), "Workshop exports are decompressed");
        }

        private static void Patch(Harmony harmony, MethodInfo method, string transpiler, string done)
        {
            replaced = 0;
            harmony.Patch(method, transpiler: new HarmonyMethod(typeof(DirectReadPatches), transpiler));
            if (replaced == 0)
            {
                Log.Warning($"Nothing to replace in {method.DeclaringType.FullName}.{method.Name}: it may " +
                    "fail on compressed files");
            }
            else
            {
                Log.Info($"{done} ({replaced} call(s) replaced in {method.DeclaringType.FullName}.{method.Name})");
            }
        }

        private static IEnumerable<CodeInstruction> StreamReaderTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            foreach (CodeInstruction instruction in instructions)
            {
                if (instruction.opcode == OpCodes.Newobj && (instruction.operand as ConstructorInfo) == StreamReaderCtor)
                {
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = OpenText;
                    replaced++;
                }
                yield return instruction;
            }
        }

        private static IEnumerable<CodeInstruction> FileCopyTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            foreach (CodeInstruction instruction in instructions)
            {
                if (instruction.Calls(FileCopy))
                {
                    instruction.operand = CopyDecompressed;
                    replaced++;
                }
                yield return instruction;
            }
        }
    }
}
