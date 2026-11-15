using System;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace com.github.lhervier.ksp.mod.compressedsaves
{
    /// <summary>
    /// The file operations of the mod: tell a gzip file from a plain one, read one as text whatever its
    /// format, compress a plain one in place. A compressed file keeps its name and extension.
    /// </summary>
    internal static class GzipFiles
    {
        private const int BufferSize = 1 << 20;

        /// <summary>Whether <paramref name="path"/> names a save game or a craft file, by its extension.</summary>
        public static bool IsSaveOrCraft(string path)
        {
            string extension = Path.GetExtension(path);
            return string.Equals(extension, ".sfs", StringComparison.OrdinalIgnoreCase)
                || string.Equals(extension, ".craft", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Whether the mod compresses <paramref name="path"/> once KSP has written it: a save game or a craft
        /// file inside the saves folder of KSP. Files elsewhere (GameData, mission folders) stay plain.
        /// </summary>
        public static bool ShouldCompress(string path)
        {
            if (!IsSaveOrCraft(path))
            {
                return false;
            }
            string savesFolder = Path.GetFullPath(Path.Combine(KSPUtil.ApplicationRootPath, "saves"))
                .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            return Path.GetFullPath(path).StartsWith(savesFolder, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Whether <paramref name="path"/> starts with the two bytes of a gzip header. False for a missing or
        /// empty file. A plain save starts with text, never with these bytes.
        /// </summary>
        public static bool IsGzip(string path)
        {
            if (!File.Exists(path))
            {
                return false;
            }
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                return stream.ReadByte() == 0x1F && stream.ReadByte() == 0x8B;
            }
        }

        /// <summary>Returns the whole text of the gzip file <paramref name="path"/>, decompressed, as UTF-8.</summary>
        public static string ReadGzipText(string path)
        {
            using (StreamReader reader = OpenGzipText(path))
            {
                return reader.ReadToEnd();
            }
        }

        /// <summary>
        /// Opens <paramref name="path"/> as text whether it is compressed or not. Stands in for
        /// <c>new StreamReader(path)</c> in code that reads a save line by line.
        /// </summary>
        public static StreamReader OpenText(string path)
        {
            return IsGzip(path) ? OpenGzipText(path) : new StreamReader(path);
        }

        /// <summary>
        /// Copies <paramref name="source"/> to <paramref name="destination"/>, decompressed when it is a gzip
        /// file. Stands in for <see cref="File.Copy(string, string)"/> where the copy leaves KSP and must stay
        /// readable without the mod; like it, fails when the destination exists.
        /// </summary>
        public static void CopyDecompressed(string source, string destination)
        {
            if (!IsGzip(source))
            {
                File.Copy(source, destination);
                return;
            }
            using (FileStream input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (GZipStream gzip = new GZipStream(input, CompressionMode.Decompress))
            using (FileStream output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write))
            {
                gzip.CopyTo(output, BufferSize);
            }
        }

        /// <summary>
        /// Replaces the plain file <paramref name="path"/> by its gzip-compressed version, under the same
        /// name. Does nothing to a file already compressed. On failure, the plain file is left as it was and
        /// the exception goes up.
        /// </summary>
        public static void CompressInPlace(string path)
        {
            if (IsGzip(path))
            {
                return;
            }
            string temp = path + ".gz.tmp";
            try
            {
                using (FileStream input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (FileStream output = new FileStream(temp, FileMode.Create, FileAccess.Write))
                using (GZipStream gzip = new GZipStream(output, CompressionMode.Compress))
                {
                    input.CopyTo(gzip, BufferSize);
                }
                // One rename: the save is never missing, plain until here, compressed after.
                File.Replace(temp, path, null);
            }
            finally
            {
                if (File.Exists(temp))
                {
                    File.Delete(temp);
                }
            }
        }

        private static StreamReader OpenGzipText(string path)
        {
            FileStream input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            return new StreamReader(new GZipStream(input, CompressionMode.Decompress), Encoding.UTF8, true, BufferSize);
        }
    }
}
