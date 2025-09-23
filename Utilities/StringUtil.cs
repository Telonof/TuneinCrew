using Gibbed.Dunia2.FileFormats;
using System.Text;

namespace TuneinCrew.Utilities
{
    internal class StringUtil
    {
        public static string ConvertToHexString(string text, int resize = 8, bool localization = false)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(text);

            if (localization)
            {
                byte[] stringWithNulls = new byte[(bytes.Length * 2) + 1];
                for (int i = 0; i < bytes.Length; i++)
                {
                    stringWithNulls[i * 2] = bytes[i];
                    stringWithNulls[(i * 2) + 1] = 0x00;
                }
                bytes = stringWithNulls;
            }

            if (resize == -1)
                resize = bytes.Length + 1;

            Array.Resize(ref bytes, resize);
            return BitConverter.ToString(bytes).Replace("-", "");
        }

        public static string Hash(string data)
        {
            ulong hash = CRC64.Hash(data.ToLower(), true);
            byte[] bytes = BitConverter.GetBytes(hash);
            return BitConverter.ToString(bytes).Replace("-", "");
        }

        public static string? FindAbsolutePath(string path, string relativeRootDirectory)
        {
            if (string.IsNullOrWhiteSpace(path))
                return path;

            if (Path.IsPathRooted(path))
                return path;

            return Path.Combine(relativeRootDirectory, path);
        }
    }
}
