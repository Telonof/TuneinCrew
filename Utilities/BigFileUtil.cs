using Gibbed.Dunia2.FileFormats;
using Big = Gibbed.Dunia2.FileFormats.Big;
using Gibbed.IO;
using System.Globalization;
using System.Text;

namespace TuneinCrew.Utilities
{
    internal class BigFileUtil
    {
        /**
         * Truncated methods from Gibbed.Dunia2.Pack
         * Original Author: Rick (rick 'at' gibbed 'dot' us)
        */
        public static void RepackBigFile(string inputFolder, string outputFatFile, string? author = null, bool compress = true)
        {
            inputFolder = inputFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            byte[]? authorHex = null;
            SortedDictionary<ulong, PendingEntry> pendingEntries = [];
            int byteIndex = 0;

            if (!string.IsNullOrWhiteSpace(author))
                authorHex = Encoding.UTF8.GetBytes(author);

            //Get all files in folder and get their names in CRC64 format.
            foreach (string path in Directory.GetFiles(inputFolder, "*", SearchOption.AllDirectories))
            {
                PendingEntry pendingEntry;

                string fullPath = Path.GetFullPath(path);
                string partPath = path.Substring(inputFolder.Length + 1);

                pendingEntry.FullPath = fullPath;

                var pieces = partPath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                int index = 0;

                if (index >= pieces.Length)
                    continue;

                if (pieces[index].ToUpperInvariant().Equals("UNKNOWN"))
                {
                    var partName = Path.GetFileNameWithoutExtension(partPath);
                    partName = partName.Split(".")[0];

                    if (string.IsNullOrEmpty(partName))
                        continue;

                    if (partName.Length > 16)
                        partName = partName.Substring(0, 16);

                    pendingEntry.NameHash = ulong.Parse(partName, NumberStyles.AllowHexSpecifier);
                }
                else
                {
                    string name = string.Join("\\", pieces.Skip(index).ToArray()).ToLowerInvariant();
                    pendingEntry.NameHash = CRC64.Hash(name, true);
                }

                if (pendingEntries.ContainsKey(pendingEntry.NameHash))
                    continue;

                pendingEntries[pendingEntry.NameHash] = pendingEntry;
            }

            var fat = new BigFile
            {
                Version = 5,
                Platform = Big.Platform.PC,
                Unknown74 = 0
            };

            using (var output = File.Create(Path.ChangeExtension(outputFatFile, "dat")))
            {
                foreach (var pendingEntry in pendingEntries.Select(kv => kv.Value))
                {
                    var entry = new Big.Entry
                    {
                        NameHash = pendingEntry.NameHash,
                        Offset = output.Position,
                        author = 0
                    };

                    //Ingrain author into dummy values.
                    if (authorHex != null)
                    {
                        uint result = 0;
                        for (int i = 0; i < 4; i++)
                        {
                            if (authorHex.Length <= byteIndex)
                            {
                                byteIndex = 0;
                            }
                            result |= (uint)authorHex[byteIndex] << 24 - i * 8;
                            byteIndex++;
                        }
                        entry.author = result;
                    }

                    using (var input = File.OpenRead(pendingEntry.FullPath))
                    {
                        Big.EntryCompression.Compress(fat.Platform, ref entry, input, compress, output);
                        output.Seek(output.Position.Align(16), SeekOrigin.Begin);
                    }

                    fat.Entries.Add(entry);
                }
            }

            using (var output = File.Create(outputFatFile))
            {
                fat.Serialize(output);
            }
        }
    }

    internal struct PendingEntry
    {
        public ulong NameHash;
        public string FullPath;
    }
}
