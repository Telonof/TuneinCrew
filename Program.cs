using TuneinCrew.Utilities;

namespace TuneinCrew
{
    internal class Program
    {
        public static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine($"Usage: {Path.GetFileName(Environment.ProcessPath)} <Radio XML>");
                return;
            }

            if (!File.Exists(args[0]))
            {
                Logger.Error("Radio XML cannot be found.");
                return;
            }

            //For drag-and-drop files
            Directory.SetCurrentDirectory(Path.GetDirectoryName(Environment.ProcessPath));

            new ProjectToMod(args[0], "Assets").Run();
        }
    }
}
