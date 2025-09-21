using System.IO.Compression;
using System.Xml.Linq;
using TuneinCrew.Tools;
using TuneinCrew.Utilities;

namespace TuneinCrew
{
    internal class ProjectToMod
    {
        private const string _tempModDirectory = "mod_folder";

        private readonly string _file;

        private readonly string _projectDirectory;

        private readonly string _assetsDirectory;
        
        private string _fmodLocation;

        private string _prefix;

        public ProjectToMod(string file, string assetsDirectory)
        {
            _file = file;
            _projectDirectory = Path.GetDirectoryName(file);
            _assetsDirectory = assetsDirectory;

            Directory.CreateDirectory(_tempModDirectory);
        }

        public void Run()
        {
            //First job is to validate the XML so we're not dealing with an invalid one.
            XElement? projectRoot = XMLUtil.LoadAndValidateXML(_file);
            if (projectRoot == null)
            {
                Logger.Error("XML is not valid.");
                return;
            }

            //Grab the fmod executable location to then use to create FSBs.
            _fmodLocation = StringUtil.FindAbsolutePath(XMLUtil.GetNodeValue(projectRoot, "fmod"), _projectDirectory);
            if (_fmodLocation == null || string.IsNullOrWhiteSpace(_fmodLocation) || !File.Exists(_fmodLocation))
            {
                Logger.Error("FMOD file not found.");
                return;
            }

            //For Linux. Allows you prefixing the FMOD command line executable with anything needed to get it to run on the machine.
            _prefix = XMLUtil.GetNodeValue(projectRoot, "prefix");

            List<Radio> radios = RadioParser.ParseRadios(projectRoot, _projectDirectory);

            if (radios.Count == 0)
            {
                Logger.Error("No radios found in project file.");
                return;
            }

            foreach (Radio radio in radios)
            {
                RadioBuilder(radio);
            }
        }

        public void RadioBuilder(Radio radio)
        {
            string fdpLocation = Path.Combine(_projectDirectory, Path.ChangeExtension(radio.InternalRadioName, ".fdp"));

            //Got ID and got name, setup localization data so game actually shows the string properly.
            XMLGenerator.GenerateLocalizationData(radio.Id, radio.Name, _assetsDirectory, _tempModDirectory);

            //Open up radio binary needed to add actual radio listings to The Crew.
            XDocument radioBin = XMLGenerator.GenerateBaseRadioBinary(radio, _assetsDirectory);

            //Add logo to radio if found.
            if (radio.LogoPath != null)
                GenerateLogo(radio, radioBin.Root);

            //Generate PitCrew song entity additions.
            //The Crew needs an entity for each song inside the game on top of a listing for the radio itself linking to these entities.
            XDocument songBin = XMLGenerator.GenerateBaseSongBinary();

            bool jinglesUsed = radio.Jingles.Count > 0;

            //Now we setup the FDP file itself to then parse into a fev/fsb that the game uses.
            //If an FDP of the same name already exists, use that one instead to allow custom FDPs.
            if (!File.Exists(fdpLocation))
            {
                FMODBuilder.CreateFDP(radio, radio.InternalRadioName, fdpLocation, _assetsDirectory, jinglesUsed);
            }
            else
            {
                Logger.Warning($"Using existing FDP found at {fdpLocation}.");
                Logger.Warning("Delete the file and re-run this tool to generate a new one.");
            }

            FMODBuilder.Build(fdpLocation, radio.InternalRadioName, _projectDirectory, _prefix, _fmodLocation, _assetsDirectory);

            //For every song existing, add an entity to the game and add a listing to the radio entity.
            for (int i = 1; i <= radio.Songs.Count(); i++)
            {
                XMLGenerator.AddSongsAndJingles(radio, radio.Songs[i - 1], i, radioBin.Root, songBin.Root.Element("add"), _assetsDirectory);
            }

            //Now if jingles were added to the FDP file, also add them to the entity and radio list.
            if (jinglesUsed)
                XMLGenerator.AddSongsAndJingles(radio, null, -1, radioBin.Root, songBin.Root.Element("add"), _assetsDirectory);

            //All files have been made, cleanup and package into an installable PitCrew mod.
            songBin.Save(Path.Combine(_tempModDirectory, $"{radio.InternalRadioName}_songs.xml"));
            radioBin.Save(Path.Combine(_tempModDirectory, $"{radio.InternalRadioName}_radio.xml"));
            PackageMod(radio);
        }

        private void GenerateLogo(Radio radio, XElement radioBin)
        {
            if (string.IsNullOrWhiteSpace(radio.LogoPath))
                return; 

            //Convert DDS to XBT
            byte[] xbtData = [0x54, 0x42, 0x58, 0x00, 0x0B, 0x00, 0x00, 0x00, 0x28, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00,
                          0x91, 0x54, 0x5C, 0x05, 0xB1, 0x21, 0x12, 0x88, 0x35, 0xD7, 0x87, 0x29, 0x00, 0x00, 0x00, 0x00,
                          0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00];

            byte[] imageData = File.ReadAllBytes(radio.LogoPath);

            List<byte> data = [];
            data.AddRange(xbtData.ToList());
            data.AddRange(imageData.ToList());

            //Save XBT to temp folder
            string radioLogoPath = Path.Combine("temp", "ui", "textures", "radiologos");
            Directory.CreateDirectory(radioLogoPath);
            File.WriteAllBytes(Path.Combine(radioLogoPath, $"{radio.InternalRadioName}.xbt".ToLower()), data.ToArray());

            //Set logo value inside radio binary.
            XElement logoValue = XMLUtil.FindType(radioBin, "field", "name", "Logo");
            logoValue.Value = StringUtil.Hash($"ui\\textures\\radiologos\\{radio.InternalRadioName}.xbt");
        }

        private void PackageMod(Radio radio)
        {
            BigFileUtil.RepackBigFile("temp", Path.Combine(_tempModDirectory, $"{radio.InternalRadioName}_data.fat"), "TuneinCrew");
            Directory.Delete("temp", true);

            //Create PitCrew metadata
            XMLGenerator.CreatePitCrewMData(radio, _assetsDirectory, _tempModDirectory);

            //Now add all files to zip and delete them.
            string outputZip = Path.Combine(_projectDirectory, $"TuneinCrew{radio.Id}.zip");

            if (File.Exists(outputZip))
                File.Delete(outputZip);

            using (var zip = ZipFile.Open(outputZip, ZipArchiveMode.Create))
            {
                foreach (string file in Directory.GetFiles(_tempModDirectory, "*", SearchOption.AllDirectories))
                {
                    string entryName = Path.GetFileName(file);
                    zip.CreateEntryFromFile(file, entryName);
                }
            }

            Directory.Delete(_tempModDirectory, true);
        }
    }
}
