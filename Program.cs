using System.CommandLine;

namespace TuneinCrew
{
    internal class Program
    {
        public static void Main(string[] args)
        {
            Option<bool> entityOption = new Option<bool>("--entities") { Description = "Disables radio creation only keeping the song entities." };
            Argument<FileInfo> input = new Argument<FileInfo>("Radio XML") { Description = "The XML containing all the data for the radio." }.AcceptExistingOnly();
            Argument<FileInfo> fmodcli = new Argument<FileInfo>("FMOD-CL Path") { Description = "An optional path to the FMOD cli directly.", Arity = ArgumentArity.ZeroOrOne}.AcceptExistingOnly();

            RootCommand command = new RootCommand("TuneInCrew") { Description = "A semi-automatic radio creator for The Crew." };
            command.Add(input);
            command.Add(entityOption);
            command.Add(fmodcli);

            //For drag-and-drop files
            Directory.SetCurrentDirectory(Path.GetDirectoryName(Environment.ProcessPath));

            if (args.Length == 0)
            {
                command.Parse("-h").Invoke();
                return;
            }

            command.SetAction(parseResult =>
            {
                string cliPath = "";
                FileInfo xml = parseResult.GetValue(input);
                FileInfo cli = parseResult.GetValue(fmodcli);
                if (cli != null)
                    cliPath = cli.FullName;

                bool entity = parseResult.GetValue(entityOption);

                new ProjectToMod(xml.FullName, "Assets", cliPath, entity).Run();
            });

            ParseResult result = command.Parse(args);
            result.Invoke();
        }
    }
}
