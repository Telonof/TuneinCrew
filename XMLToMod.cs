using Gibbed.Dunia2.FileFormats;
using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using TuneinCrew.Utilities;

namespace TuneinCrew
{
    internal class XMLToMod
    {
        private readonly string _file;

        private readonly string _projectDirectory;
        
        private string _fmodLocation;

        private string _prefix;

        public XMLToMod(string file)
        {
            _file = file;
            _projectDirectory = Path.GetDirectoryName(file);
        }

        public void Run()
        {
            //First job is to validate the XML so we're not dealing with an invalid one.
            XElement projectRoot;
            try
            {
                projectRoot = XDocument.Load(_file).Root;
            }
            catch (XmlException e)
            {
                Console.WriteLine("ERR2: XML is not valid.");
                Console.WriteLine(e.Message);
                return;
            }

            //Grab the fmod executable location to then use to create FSBs.
            _fmodLocation = XMLUtil.GetNodeValue(projectRoot, "fmod");
            if (_fmodLocation == null || string.IsNullOrWhiteSpace(_fmodLocation) || !File.Exists(_fmodLocation))
            {
                Console.WriteLine("ERR3: fmod file not found.");
                return;
            }
            
            _prefix = XMLUtil.GetNodeValue(projectRoot, "prefix");
            
            //We're not going to run the tool if the radio count inside the XML is 0.
            if (!projectRoot.Elements("radio").Any())
            {
                Console.WriteLine("ERR4: no radios found in XML.");
                return;
            }

            foreach (XElement element in projectRoot.Elements("radio"))
            {
                RadioParser(element);
            }
        }

        public void RadioParser(XElement element)
        {
            //ids should be 4 characters to ensure uniqueness and to not overflow project paths inside The Crew's entities file.
            string id = XMLUtil.GetNodeValue(element, "id");

            if (id == null)
            {
                Console.WriteLine($"ERR5: Could not find ID node inside radio.");
                return;
            }

            if (id.Length != 4)
            {
                Console.WriteLine($"ERR6: Please make your radio ID {id} 4 characters long.");
                return;
            }

            string radioName = $"Radio_{id}";
            string fdpLocation = Path.Combine(_projectDirectory, Path.ChangeExtension(radioName, ".fdp"));

            //Test if name exists
            string name = XMLUtil.GetNodeValue(element, "name");
            if (name == null)
            {
                Console.WriteLine($"ERR7: Could not find name node inside radio {radioName}.");
                return;
            }

            //Test if radio has no songs.
            XElement songsElement = element.Element("songs");
            if (songsElement == null || !songsElement.Elements("song").Any())
            {
                Console.WriteLine($"ERR8: Could not find any songs inside {radioName}.");
                return;
            }

            //Now we setup the FDP file itself to then parse into a fev/fsb that the game uses.
            //If an FDP of the same name already exists, use that one instead to allow custom FDPs.
            if (!File.Exists(fdpLocation))
            {
                CreateFDP(songsElement, radioName, fdpLocation);
                File.Copy(Path.Combine("Assets", "template.fdt"), Path.Combine(_projectDirectory, "template.fdt"), true);
            }
            else
            {
                Console.WriteLine($"Using existing FDP found at {fdpLocation}. Delete the file and re-run this tool to generate a new one.");
            }

            string fmodArgs = $"-pc {Path.GetFullPath(fdpLocation)}";
            //For linux
            if (!string.IsNullOrWhiteSpace(_prefix))
            {
                //Ask since FMOD Designer only works with windows pointers to files.
                Console.WriteLine($"Enter the wine path where file {Path.GetFullPath(fdpLocation)} is located (W:/C/file.fdp for example).");
                string drive = Console.ReadLine();
                fmodArgs = $"\"{_fmodLocation}\" -pc {drive}";
                _fmodLocation = _prefix;
            }
            
            //Generate the actual fev and fsb
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = _fmodLocation,
                Arguments = fmodArgs,
                UseShellExecute = true,
            };

            Process process = Process.Start(startInfo);
            process.WaitForExit();

            //Move the fev and fsb to the folder to pack into a DAT
            Directory.CreateDirectory(Path.Combine("temp", "sound"));
            File.Move(Path.Combine(_projectDirectory, $"{radioName}.fev"), Path.Combine("temp", "sound", $"{radioName}.fev"), true);
            File.Move(Path.Combine(_projectDirectory, $"{radioName}.fsb"), Path.Combine("temp", "sound", $"{radioName}.fsb"), true);

            //Open up radio binary needed to add actual radio listings to The Crew.
            XDocument radioBin = XDocument.Load(Path.Combine("Assets", "radio_bin.xml"));

            //Add name to radio binary (unneeded since game uses localized string "User Radio")
            XElement value = XMLUtil.FindType(radioBin.Root, "field", "name", "Name");
            value.Value = ConvertToHexString(XMLUtil.GetNodeValueOrDefault(element, "name"), -1);

            //Add logo to radio if found.
            GenerateLogo(element, radioName, radioBin.Root);

            //Generate PitCrew song entity additions.
            //The Crew needs an entity for each song inside the game on top of a listing for the radio itself linking to these entities.
            XDocument songBin = new();
            XElement root = new XElement("root");
            root.SetAttributeValue("file", "entity/generated/archetypes.entities.bin");

            XElement add = new XElement("add");
            add.SetAttributeValue("depth", "root");

            value = XMLUtil.FindType(radioBin.Root, "object", "name", "Items");

            int count = 1;
            //For every song existing, add an entity to the game and add a listing to the radio entity.
            foreach (XElement song in songsElement.Elements())
            {
                AddSongToEntityAndRadioList(value, add, song, id, count, radioName);
                count++;
            }

            //Now that all files have been made, cleanup and package into a PitCrew mod easily installable.
            root.Add(add);
            songBin.Add(root);
            songBin.Save($"{radioName}_songs.xml");
            radioBin.Save($"{radioName}_radio.xml");
            PackageMod(id, radioName, name);
        }

        private void CreateFDP(XElement songsElement, string radioName, string fdpLocation)
        {
            //This is assuming no one messed up anything inside the Assets folder like the !!!README asked.
            XDocument fdpFile = XDocument.Load(Path.Combine("Assets", "template.fdp"));

            //Add radio name to every location needed.
            fdpFile.Root.SetElementValue("name", radioName);
            fdpFile.Root.SetElementValue("currentbank", radioName);

            XElement area = fdpFile.Root.Element("eventgroup");
            area.SetElementValue("name", radioName);

            area = fdpFile.Root.Element("soundbank");
            area.SetElementValue("name", radioName);

            
            //Grab all the songs from the project XML to parse inside the FDP.
            int songCount = songsElement.Elements("song").Count();

            for (int i = 0; i < songCount; i++)
            {
                XElement songElement = songsElement.Elements("song").ElementAtOrDefault(i);

                string songFileName = XMLUtil.GetNodeValue(songElement, "file");

                if (songFileName == null || !File.Exists(songFileName))
                {
                    Console.WriteLine($"ERR FDP1: Could not find file for song {i + 1} inside {radioName}.");
                    continue;
                }

                //Setup sounddef
                XDocument customs = XDocument.Load(Path.Combine("Assets", "fmod_sounddef.xml"));
                customs.Root.SetElementValue("name", $"/{Path.GetFileNameWithoutExtension(songFileName)}");
                customs.Root.SetElementValue("guid", "{" + Guid.NewGuid() + "}");

                area = customs.Root.Element("waveform");
                area.SetElementValue("filename", songFileName);
                area.SetElementValue("soundbankname", radioName);

                area = fdpFile.Root.Element("sounddeffolder");
                area.Add(customs.Root);

                //Setup soundbank
                area = fdpFile.Root.Element("soundbank");
                customs = XDocument.Load(Path.Combine("Assets", "fmod_waveform.xml"));
                customs.Root.SetElementValue("filename", songFileName);
                customs.Root.SetElementValue("guid", "{" + Guid.NewGuid() + "}");
                area.Add(customs.Root);

                //Setup event
                customs = XDocument.Load(Path.Combine("Assets", "fmod_event.xml"));
                customs.Root.SetElementValue("name", (i + 1).ToString("D2"));
                customs.Root.SetElementValue("guid", "{" + Guid.NewGuid() + "}");
                area = customs.Root.Element("layer").Element("sound");
                area.SetElementValue("name", $"/{Path.GetFileNameWithoutExtension(songFileName)}");

                //Setup full force trigger
                string force = (80.0 / 300.0).ToString("F6");
                string forceValue = XMLUtil.GetNodeValue(songElement, "force");
                if (forceValue != null && !string.IsNullOrWhiteSpace(forceValue))
                    force = (double.Parse(forceValue) / 300.0).ToString("F6");

                //Formatted <#/300>,<#/1>,0,1 and there's two. Second point is what we need.
                area = customs.Root.Element("layer").Descendants("envelope").FirstOrDefault(node => (string)node.Element("name") == "env011");
                area = area.Elements("point").Last();
                string[] pointData = area.Value.Split(",");
                pointData[0] = force;
                area.Value = string.Join(",", pointData);

                area = fdpFile.Root.Element("eventgroup");
                area.Add(customs.Root);
            }

            fdpFile.Save(fdpLocation);
        }

        private void GenerateLogo(XElement element, string radioName, XElement radioBin)
        {
            string logoPath = XMLUtil.GetNodeValue(element, "logo");

            if (logoPath == null || !File.Exists(logoPath))
            {
                Console.WriteLine("WARN LOG1: Could not find logo node, skipping logo creation.");
                return;
            }

            //Convert DDS to XBT
            byte[] xbtData = [0x54, 0x42, 0x58, 0x00, 0x0B, 0x00, 0x00, 0x00, 0x28, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00,
                          0x91, 0x54, 0x5C, 0x05, 0xB1, 0x21, 0x12, 0x88, 0x35, 0xD7, 0x87, 0x29, 0x00, 0x00, 0x00, 0x00,
                          0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00];

            byte[] imageData = File.ReadAllBytes(logoPath);

            List<byte> data = [];
            data.AddRange(xbtData.ToList());
            data.AddRange(imageData.ToList());

            //Save XBT to temp folder
            string radioLogoPath = Path.Combine("temp", "ui", "textures", "radiologos");
            Directory.CreateDirectory(radioLogoPath);
            File.WriteAllBytes(Path.Combine(radioLogoPath, $"{radioName}.xbt".ToLower()), data.ToArray());

            //Set logo value inside radio binary.
            XElement logoValue = XMLUtil.FindType(radioBin, "field", "name", "Logo");
            logoValue.Value = Hash($"ui\\textures\\radiologos\\{radioName}.xbt");
        }

        private void AddSongToEntityAndRadioList(XElement radioElement, XElement songElement, XElement songData, string id, int count, string radioName)
        {
            string uniqueName = id + count.ToString("D2");

            //Reverse the ID since the binary tool prints them to the file backwards.
            char[] charArray = uniqueName.ToCharArray();
            Array.Reverse(charArray);
            uniqueName = new string(charArray);

            string radioId = $"{radioName}/{count.ToString("D2")}";

            //Add song entity.
            XDocument doc = XDocument.Load(Path.Combine("Assets", "song_entity.xml"));

            var field = XMLUtil.FindType(doc.Root, "field", "name", "ID");
            field.Value = ConvertToHexString(uniqueName);

            field = XMLUtil.FindType(doc.Root, "field", "name", "EventName");
            field.Value = ConvertToHexString(radioId, -1);

            field = XMLUtil.FindType(doc.Root, "field", "name", "ProjectPathId");
            field.Value = Hash($"sound\\{radioName}.fev");

            songElement.Add(doc.Root);

            //Now add listing to radio entity.
            doc = XDocument.Load(Path.Combine("Assets", "individual_song.xml"));

            field = XMLUtil.FindType(doc.Root, "field", "name", "Item");
            field.Value = ConvertToHexString(uniqueName);

            field = XMLUtil.FindType(doc.Root, "field", "name", "TrackName");
            field.Value = ConvertToHexString(XMLUtil.GetNodeValueOrDefault(songData, "name"), -1);

            field = XMLUtil.FindType(doc.Root, "field", "name", "ArtistName");
            field.Value = ConvertToHexString(XMLUtil.GetNodeValueOrDefault(songData, "artist"), -1);

            field = XMLUtil.FindType(doc.Root, "field", "name", "Album");
            field.Value = ConvertToHexString(songData.Parent.Parent.Element("name").Value, -1);

            field = XMLUtil.FindType(doc.Root, "field", "name", "Year");
            field.Value = ConvertToHexString(XMLUtil.GetNodeValueOrDefault(songData, "year"), -1);

            field = XMLUtil.FindType(doc.Root, "field", "name", "Length");
            field.Value = ConvertToHexString(XMLUtil.GetNodeValueOrDefault(songData, "length"), -1);

            radioElement.Add(doc.Root);
        }

        private void PackageMod(string id, string radioName, string modName)
        {
            BigFileUtil.RepackBigFile("temp", $"{radioName}_data.fat", "TuneinCrew");
            Directory.Delete("temp", true);

            //Create PitCrew metadata
            XDocument mdata = XDocument.Load(Path.Combine("Assets", "mdata.xml"));
            mdata.Root.Element("names").Element("English").Value = modName;

            XElement file = new XElement("file");
            file.SetAttributeValue("priority", "998");

            //dat
            file.SetAttributeValue("loc", $"{radioName}_data");
            mdata.Root.Element("files").Add(new XElement(file));

            //song entities
            file.SetAttributeValue("loc", $"{radioName}_songs.xml");
            mdata.Root.Element("files").Add(new XElement(file));

            //radio listing
            file.SetAttributeValue("loc", $"{radioName}_radio.xml");
            mdata.Root.Element("files").Add(new XElement(file));

            mdata.Save($"TuneinCrew{id}.mdata");

            //Now add all files to zip and delete them.
            string outputZip = Path.Combine(_projectDirectory, $"TuneinCrew{id}.zip");

            if (File.Exists(outputZip))
                File.Delete(outputZip);

            using (var zip = ZipFile.Open(outputZip, ZipArchiveMode.Create))
            {
                zip.CreateEntryFromFile($"{radioName}_radio.xml", $"{radioName}_radio.xml");
                zip.CreateEntryFromFile($"{radioName}_songs.xml", $"{radioName}_songs.xml");
                zip.CreateEntryFromFile($"{radioName}_data.fat", $"{radioName}_data.fat");
                zip.CreateEntryFromFile($"{radioName}_data.dat", $"{radioName}_data.dat");
                zip.CreateEntryFromFile($"TuneinCrew{id}.mdata", $"TuneinCrew{id}.mdata");
            }

            File.Delete($"{radioName}_radio.xml");
            File.Delete($"{radioName}_songs.xml");
            File.Delete($"{radioName}_data.fat");
            File.Delete($"{radioName}_data.dat");
            File.Delete($"TuneinCrew{id}.mdata");
        }

        /// <param name="text">Text to convert</param>
        /// <param name="resize">Truncuates text to integer. Set to -1 to add null terminator.</param>
        /// <returns>Hexademical values in string format.</returns>
        private string ConvertToHexString(string text, int resize = 8)
        {
            if (resize == -1)
                resize = text.Length + 1;

            byte[] bytes = Encoding.ASCII.GetBytes(text);
            Array.Resize(ref bytes, resize);
            return BitConverter.ToString(bytes).Replace("-", "");
        }

        private string Hash(string data)
        {
            ulong hash = CRC64.Hash(data.ToLower(), true);
            byte[] bytes = BitConverter.GetBytes(hash);
            return BitConverter.ToString(bytes).Replace("-", "");
        }
    }
}
