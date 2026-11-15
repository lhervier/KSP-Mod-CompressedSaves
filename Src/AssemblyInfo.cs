// KSP's own assembly graph: KSP loads this mod only when these assemblies are loaded, at these versions or
// later, and loads them first. Names are the ones each mod declares in its own [KSPAssembly], not file names.
//
// KSPCommunityFixes 1.41.1 is the version the mod was tested with: the private members it patches or reads
// (ConfigNodePerf._doClean, SceneLoadSpeedBoost.LoadSFSFileCached) are looked up by name, and the order of its
// ConfigNode.Load prefix after ours was checked against it.
[assembly: KSPAssembly("KSPModCompressedSaves", 0, 1, 0)]
[assembly: KSPAssemblyDependency("HarmonyKSP", 1, 0, 0)]
[assembly: KSPAssemblyDependency("KSPCommunityFixes", 1, 41, 1)]
