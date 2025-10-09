using System.Diagnostics;
using System.Globalization;
using System.Xml.Linq;
using TuneinCrew.Utilities;

namespace TuneinCrew.Tools
{
    internal class FMODBuilder
    {
        public static void CreateFDP(Radio radio, string radioName, string fdpLocation, string assetsDirectory, bool jinglesUsed)
        {
            //This is assuming no one messed up anything inside the Assets folder like the !DO_NOT_TOUCH file asked.
            XDocument fdpFile = XDocument.Load(Path.Combine(assetsDirectory, "template.fdp"));

            //Add radio name to every location needed.
            fdpFile.Root.SetElementValue("name", radioName);
            fdpFile.Root.SetElementValue("currentbank", radioName);

            XElement area = fdpFile.Root.Element("eventgroup");
            area.SetElementValue("name", radioName);

            area = fdpFile.Root.Element("soundbank");
            area.SetElementValue("name", radioName);


            //Grab all the songs from the project XML to parse inside the FDP.
            for (int i = 0; i < radio.Songs.Count(); i++)
            {
                //Setup sounddef
                XDocument customs = XDocument.Load(Path.Combine(assetsDirectory, "fmod_sounddef.xml"));
                customs.Root.SetElementValue("name", $"/{Path.GetFileNameWithoutExtension(radio.Songs[i].Path)}");
                customs.Root.SetElementValue("guid", "{" + Guid.NewGuid() + "}");

                area = customs.Root.Element("waveform");
                area.SetElementValue("filename", radio.Songs[i].Path);
                area.SetElementValue("soundbankname", radioName);

                area = fdpFile.Root.Element("sounddeffolder");
                area.Add(customs.Root);

                //Setup soundbank
                area = fdpFile.Root.Element("soundbank");
                customs = XDocument.Load(Path.Combine(assetsDirectory, "fmod_waveform.xml"));
                customs.Root.SetElementValue("filename", radio.Songs[i].Path);
                customs.Root.SetElementValue("guid", "{" + Guid.NewGuid() + "}");
                area.Add(customs.Root);

                //Setup event
                customs = XDocument.Load(Path.Combine(assetsDirectory, "fmod_event.xml"));
                customs.Root.SetElementValue("name", (i + 1).ToString("D2"));
                customs.Root.SetElementValue("guid", "{" + Guid.NewGuid() + "}");
                area = customs.Root.Element("layer").Element("sound");
                area.SetElementValue("name", $"/{Path.GetFileNameWithoutExtension(radio.Songs[i].Path)}");

                //Setup full force trigger
                string force = Math.Min(300.0, (double)radio.Songs[i].Force / 300.0).ToString("F6", CultureInfo.InvariantCulture);

                //Formatted <#/300>,<#/1>,0,1 and there's two. Second one is what we need.
                area = customs.Root.Element("layer").Descendants("envelope").FirstOrDefault(node => (string)node.Element("name") == "env011");
                area = area.Elements("point").Last();
                string[] pointData = area.Value.Split(",");
                pointData[0] = force;
                area.Value = string.Join(",", pointData);

                area = fdpFile.Root.Element("eventgroup");
                area.Add(customs.Root);
            }

            area = fdpFile.Root.Element("eventgroup").Element("simpleevent");
            area.SetElementValue("bankname", radioName);

            if (!jinglesUsed)
            {
                fdpFile.Save(fdpLocation);
                return;
            }

            foreach (string jingleFile in radio.Jingles)
            {
                XDocument customs = XDocument.Load(Path.Combine(assetsDirectory, "fmod_waveform.xml"));
                customs.Root.SetElementValue("filename", jingleFile);
                customs.Root.SetElementValue("guid", "{" + Guid.NewGuid() + "}");
                area = fdpFile.Root.Element("soundbank");
                area.Add(customs.Root);

                customs = XDocument.Load(Path.Combine(assetsDirectory, "fmod_jingles_waveform.xml"));
                customs.Root.SetElementValue("filename", jingleFile);
                customs.Root.SetElementValue("soundbankname", radioName);
                area = fdpFile.Root.Element("sounddeffolder").Element("sounddeffolder").Element("sounddef");
                area.Add(customs.Root);
            }

            fdpFile.Save(fdpLocation);
        }

        public static void Build(string fdpLocation, string fileName, string projectDirectory, string prefix, string fmodLocation, string assetsDirectory)
        {
            string fmodArgs = $"-pc \"{Path.GetFullPath(fdpLocation)}\"";

            //For linux
            if (!string.IsNullOrWhiteSpace(prefix))
            {
                //Ask since FMOD Designer only works with windows pointers to files.
                Logger.Info($"Enter the wine path where file {Path.GetFullPath(fdpLocation)} is located (W:/C/file.fdp for example).");

                string drive = Console.ReadLine();
                fmodArgs = $"\"{fmodLocation}\" -pc {drive}";
                fmodLocation = prefix;
            }

            //Generate the actual fev and fsb
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = fmodLocation,
                Arguments = fmodArgs,
                UseShellExecute = true,
            };

            Process process = Process.Start(startInfo);
            process.WaitForExit();

            //Move the fev and fsb to the folder to pack into a DAT.
            //FMOD command line has an output location, but for Linux users I'd rather not ask them twice to input a folder location.
            Directory.CreateDirectory(Path.Combine("temp", "sound"));
            File.Move(Path.Combine(projectDirectory, $"{fileName}.fev"), Path.Combine("temp", "sound", $"{fileName}.fev"), true);
            File.Move(Path.Combine(projectDirectory, $"{fileName}.fsb"), Path.Combine("temp", "sound", $"{fileName}.fsb"), true);
        }
    }
}
