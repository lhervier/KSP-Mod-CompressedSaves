# Compressed Saves

**⚠️ Not finished yet.** This is a new mod, not a finished one. The code and the conclusions on this page can still change.

A mod for KSP 1.12 that writes save games (`.sfs`) and craft files (`.craft`) gzip-compressed, under the same
name, and reads them back whether compressed or not. It is built on top of
[KSP Community Fixes](https://github.com/KSPModdingLibs/KSPCommunityFixes), which it needs. Here is what it is for:

> **A career with many vessels writes saves of tens of megabytes, and Steam Cloud keeps only so much per game.**
> A few dozen saves, their automatic backups and the craft files fill it up.

Saves are text, very repetitive: gzip shrinks them more than fifteen times. A large career save of 63 MB (about 700
vessels, 11,000 parts) comes down to 3.6 MB.

**How this was made.** The investigation and the code were written with Claude, Anthropic's AI assistant.
Everything here was reviewed and validated by a human — me. I am saying so up front, because contributions made
with an AI deserve a closer look than others, and because some people would rather stop reading here. That look
should be quick: the mod is seven short source files, about 500 lines, the stock and KSP Community Fixes code it touches is named
on this page, and every check below can be replayed by hand on any save.

## Before you install: compressed saves need the mod

A compressed save is no longer text. **Without this mod, or without KSP Community Fixes, KSP cannot read it**, and
neither can a text editor or a save editor. To go back to plain files:

1. Set `compress = False` in the [settings](#settings) and start KSP: compressed files are still read, and every
   file KSP writes from then on is plain.
2. Load and save each game, and each craft in the editor, that you want plain again.

A compressed file can also be opened by hand with any tool that reads gzip, such as 7-Zip, or
`gzip -dc file.sfs > plain.sfs`.

## What it does

- **What gets compressed.** Every `.sfs` and `.craft` file that KSP writes inside its `saves` folder: the
  persistent save, quicksaves, named saves, craft files saved in the editors. A file keeps its name and
  extension, so the game's lists of saves and crafts, and Steam Cloud, see the same files as before.
- **What comes along.** The game's automatic backups (`saves\<game>\Backup`) are copies of the save made before
  each new one: they are compressed when the save is. Mods that save through the game, as FMRS does, write
  compressed files too.
- **What stays plain.** Anything outside `saves`, and the small `.loadmeta` files that hold what the load dialogs
  show. A craft sent to the Steam Workshop is uploaded plain, so players without the mod can load it.
- **Reading.** A file is told compressed by its first two bytes (the gzip header), whatever its name. Plain files
  are read as before: the mod can be installed on an existing game, and its files get compressed one by one, as
  KSP writes them.

## How it works

Every save and craft file goes through `ConfigNode.Load` and `ConfigNode.Save`. KSP Community Fixes already
replaces both (its `ConfigNodePerf` patch): its `Load` reads the file itself, with a buffer sized on the length of
the file on disk, which a compressed file would cut short. The mod therefore goes first:

- **`ConfigNode.Load`.** A prefix that runs before KSP Community Fixes' one. For a `.sfs` or `.craft` file that
  starts with the gzip header, it decompresses the text, parses it with `ConfigNode.Parse` (which KSP Community
  Fixes also replaces, with the same parser as its `Load`), localizes it as stock `Load` does, and skips the rest:
  Harmony does not run a later prefix once one has returned `false`. Any other file goes on as before.
- **`ConfigNode.Save`.** A postfix: once the file is written, plain, by KSP Community Fixes or stock, the mod
  compresses it to a temporary file and swaps it in with one rename. If anything fails, the plain file stays.
- **KSP Community Fixes' `SceneLoadSpeedBoost`.** It keeps the persistent save in memory between scenes, and reads
  the first lines of the file to check it is still the same. The mod makes it read them through gzip: without
  that, it would reload the save from disk at every scene change.
- **`EditorLogic.preupdateSteamItem`.** It copies the craft file before uploading it to the Workshop. The mod
  makes that copy decompress.

## Checked

KSP 1.12.5 with KSP Community Fixes 1.41.1, the mod at `logLevel = Debug`, on a large career save with its mods:

- **A compressed save loads as its plain copy.** A quicksave and its gzip copy are each loaded, then saved under
  a new name; the plain one is loaded and saved a second time, for reference. Decompressed and compared, the save
  made from the gzip copy differs from the first plain one exactly where the two plain ones differ from each
  other: orbit elements in their last digits, random seeds and ids. Same nodes, same keys.
- **Times.** At `Debug`, the mod writes one line per file to `KSP.log`. For the 63 MB save:
  `Compressed …persistent.sfs in 376 ms`, and `Read compressed …persistent.sfs: 62912768 chars, decompressed in
  131 ms, parsed in 701 ms`.
- **KSP Community Fixes' cache.** After a scene change, `KSP.log` reads
  `[SceneLoadSpeedBoost] Save loaded from cache in 2ms`.
- **Backups.** Two quicksaves under the same name: the copy of the first one in `Backup` is compressed.
- **FMRS.** A launch, stages dropped, then a jump back to a dropped stage: FMRS writes its saves compressed, and
  reads them back.
- **Crafts.** A craft saved in the editor is compressed (199 KB down to 11 KB); loaded again from the compressed
  file, then saved again, it differs from the first save only as two loads of the same craft do: part ids
  renumbered, positions in their last digits. A compressed craft also launches from the space centre.
- **Steam Workshop.** A craft whose file is compressed is exported to the Workshop; the copy the game uploads,
  in `steamExport\<craft>\`, is plain text.

## Install

Copy the `GameData\KSPModCompressedSaves` folder into the `GameData` folder of KSP. It needs
[Harmony](https://github.com/KSPModdingLibs/HarmonyKSP) and
[KSP Community Fixes](https://github.com/KSPModdingLibs/KSPCommunityFixes) 1.41.1 or later: without them, KSP
does not load it.

## Settings

`GameData\KSPModCompressedSaves\PluginData\settings.cfg` is read once, when KSP starts:

```
// Writes save games (.sfs) and craft files (.craft) of the saves folder gzip-compressed, under the same
// name. Compressed files are read whatever this setting says: set it to False to go back to plain files,
// which KSP then writes from the next save on.
compress = True

// How much the mod writes to KSP.log: Error, Warning, Info or Debug. At Debug, one line for every file
// read or compressed, with the time it took.
logLevel = Info
```

## Build

Set `KSPDIR` to the folder of a KSP install with Harmony in its `GameData`, then run `build.bat`: it compiles
`GameData\KSPModCompressedSaves\KSPModCompressedSaves.dll`. Installing is up to you.

## License

[MIT](LICENSE).
