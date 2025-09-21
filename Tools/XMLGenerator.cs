using System.Text;
using System.Xml.Linq;
using TuneinCrew.Utilities;

namespace TuneinCrew.Tools
{
    internal class XMLGenerator
    {
        public static void GenerateLocalizationData(string id, string radioName, string assetsDirectory, string outputFolder)
        {
            //Generate localization string in-game.
            XDocument doc = XDocument.Load(Path.Combine(assetsDirectory, "localization_string.xml"));

            XElement field = XMLUtil.FindType(doc.Root, "field", "hash", "29D6A3E8");
            field.Value = StringUtil.ConvertToHexString(radioName, -1, true);

            field = XMLUtil.FindType(doc.Root, "field", "hash", "BF396750");
            field.Value = StringUtil.ConvertToHexString(id, 4);

            doc.Save(Path.Combine(outputFolder, $"Radio_{id}_loc_string.xml"));

            //Now the text table file accessing the string.
            doc = XDocument.Load(Path.Combine(assetsDirectory, "localization_tat.xml"));

            field = XMLUtil.FindType(doc.Root, "field", "hash", "BF396750");
            field.Value = StringUtil.ConvertToHexString(id, 4);

            doc.Save(Path.Combine(outputFolder, $"Radio_{id}_loc_tat.xml"));
        }

        public static XDocument GenerateBaseRadioBinary(Radio radio, string assetsDirectory)
        {
            XDocument radioBin = XDocument.Load(Path.Combine(assetsDirectory, "radio_bin.xml"));

            //Add name to radio binary (unneeded since game uses localized string anyways)
            XElement value = XMLUtil.FindType(radioBin.Root, "field", "name", "Name");
            value.Value = StringUtil.ConvertToHexString(radio.Name, -1);

            //Add localized string id to reference to new string in game database.
            value = XMLUtil.FindType(radioBin.Root, "field", "name", "NameId");
            value.Value = StringUtil.ConvertToHexString(radio.Id, 4);

            return radioBin;
        }

        public static XDocument GenerateBaseSongBinary()
        {
            XDocument songBinary = new();
            XElement root = new XElement("root");
            root.SetAttributeValue("file", "entity/generated/archetypes.entities.bin");

            XElement add = new XElement("add");
            add.SetAttributeValue("depth", "root");
            root.Add(add);

            songBinary.Add(root);

            return songBinary;
        }

        public static void AddSongsAndJingles(Radio radio, Song song, int songNumber, XElement radioBinary, XElement songBinary, string assetsDirectory)
        {
            string uniqueName = $"{radio.Id}_zz";
            string eventName = "Jingle";

            //Reverse the ID since the binary tool prints them to the file backwards.
            char[] charArray = uniqueName.ToCharArray();
            Array.Reverse(charArray);
            uniqueName = StringUtil.ConvertToHexString(new string(charArray));

            //Allow up to 31k song ids by allowing uint16's in the ids.
            //No sane person should really be going over 1000 for one radio station.
            if (songNumber != -1)
            {
                eventName = songNumber.ToString("D2");
                uniqueName = songNumber.ToString("X4") + uniqueName.Substring(4);
            }

            //Add song entity.
            XDocument doc = XDocument.Load(Path.Combine(assetsDirectory, "song_entity.xml"));

            var field = XMLUtil.FindType(doc.Root, "field", "name", "ID");
            field.Value = uniqueName;

            field = XMLUtil.FindType(doc.Root, "field", "name", "EventName");
            field.Value = StringUtil.ConvertToHexString($"{radio.InternalRadioName}/{eventName}", -1);

            field = XMLUtil.FindType(doc.Root, "field", "name", "ProjectPathId");
            field.Value = StringUtil.Hash($"sound\\{radio.InternalRadioName}.fev");

            if (songNumber == -1)
            {
                field = XMLUtil.FindType(doc.Root, "field", "name", "FatherArchetypeID");
                field.Value = "1D45840700000000";
                songBinary.Add(doc.Root);

                field = XMLUtil.FindType(radioBinary, "field", "name", "Jingle");
                field.Value = uniqueName;
                return;
            }

            songBinary.Add(doc.Root);

            //Now add listing to radio entity.
            doc = XDocument.Load(Path.Combine(assetsDirectory, "individual_song.xml"));

            field = XMLUtil.FindType(doc.Root, "field", "name", "Item");
            field.Value = uniqueName;

            field = XMLUtil.FindType(doc.Root, "field", "name", "TrackName");
            field.Value = StringUtil.ConvertToHexString(song.Name, -1);

            field = XMLUtil.FindType(doc.Root, "field", "name", "ArtistName");
            field.Value = StringUtil.ConvertToHexString(song.Artist, -1);

            field = XMLUtil.FindType(doc.Root, "field", "name", "Album");
            field.Value = StringUtil.ConvertToHexString(song.Album, -1);

            field = XMLUtil.FindType(doc.Root, "field", "name", "Year");
            field.Value = StringUtil.ConvertToHexString(song.Year, -1);

            field = XMLUtil.FindType(doc.Root, "field", "name", "Length");
            field.Value = StringUtil.ConvertToHexString(song.Length, -1);

            field = XMLUtil.FindType(radioBinary, "object", "name", "Items");
            field.Add(doc.Root);
        }

        public static void CreatePitCrewMData(Radio radio, string assetsDirectory, string outputFolder)
        {
            XDocument mdata = XDocument.Load(Path.Combine(assetsDirectory, "mdata.xml"));
            mdata.Root.Element("names").Element("English").Value = radio.Name;

            XElement mdataFile = new XElement("file");
            mdataFile.SetAttributeValue("priority", "998");

            //dat
            mdataFile.SetAttributeValue("loc", $"{radio.InternalRadioName}_data");
            mdata.Root.Element("files").Add(new XElement(mdataFile));

            //song entities
            mdataFile.SetAttributeValue("loc", $"{radio.InternalRadioName}_songs.xml");
            mdata.Root.Element("files").Add(new XElement(mdataFile));

            //radio listing
            mdataFile.SetAttributeValue("loc", $"{radio.InternalRadioName}_radio.xml");
            mdata.Root.Element("files").Add(new XElement(mdataFile));

            //localization files
            mdataFile.SetAttributeValue("loc", $"{radio.InternalRadioName}_loc_tat.xml");
            mdata.Root.Element("files").Add(new XElement(mdataFile));

            mdataFile.SetAttributeValue("loc", $"{radio.InternalRadioName}_loc_string.xml");
            mdata.Root.Element("files").Add(new XElement(mdataFile));

            mdata.Save(Path.Combine(outputFolder, $"TuneinCrew{radio.Id}.mdata"));
        }
    }
}
