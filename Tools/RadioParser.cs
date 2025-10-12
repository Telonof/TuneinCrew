using System.Globalization;
using System.Xml.Linq;
using TuneinCrew.Utilities;

namespace TuneinCrew.Tools
{
    public record Radio
    {
        public string Id { get; set; }

        public string Name { get; set; }

        public string InternalRadioName { get; set; }

        public string? LogoPath { get; set; }

        public List<string> Jingles = [];

        public List<Song> Songs = [];
    }

    public record Song
    {
        public string Path { get; set; }

        public string? Name { get; set; }

        public string? Artist { get; set; }

        public string? Album { get; set; }

        public string? Year { get; set; }

        public string? Length { get; set; }

        public int Force { get; set; } = 80;

        public float Volume { get; set; } = 0;
    }

    internal class RadioParser
    {
        public static List<Radio> ParseRadios(XElement radiosElement, string projectDirectory)
        {
            List<Radio> radios = [];

            foreach (XElement radioElement in radiosElement.Elements("radio"))
            {
                Radio radio = new Radio();
                string? id = XMLUtil.GetNodeValue(radioElement, "id");
                string? name = XMLUtil.GetNodeValue(radioElement, "name");
                string? logo = XMLUtil.GetNodeValue(radioElement, "logo");
                logo = StringUtil.FindAbsolutePath(logo, projectDirectory);
                XElement jinglesElement = radioElement.Element("jingles");

                //ids should be 4 characters to ensure uniqueness and to not overflow project paths inside The Crew's entities file.
                if (id == null || id.Length != 4)
                {
                    Logger.Error($"Missing or invalid ID inside radio. Ensure {id} exists and is exactly 4 characters long.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(name))
                {
                    Logger.Error($"Could not find name node inside radio {id}.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(logo) || !File.Exists(logo))
                {
                    Logger.Warning("Could not find logo, skipping logo creation.");
                }

                //We cant actually test if the file exists or not since for Linux users these paths would be wine paths.
                if (jinglesElement != null)
                {
                    radio.Jingles = jinglesElement.Elements("file").Select(file => file.Value)
                            .Where(value => !string.IsNullOrWhiteSpace(value))
                            .ToList();
                }

                radio.Songs = ParseSongs(radioElement.Element("songs"), id);

                if (radio.Songs.Count == 0)
                    continue;

                radio.Id = id;
                radio.Name = name;
                radio.InternalRadioName = $"Radio_{id}";
                radio.LogoPath = logo;

                radios.Add(radio);
            }

            return radios;
        }

        private static List<Song> ParseSongs(XElement songsElement, string id)
        {
            List<Song> songs = [];

            if (songsElement == null || !songsElement.Elements("song").Any())
            {
                Logger.Error($"Could not find any songs inside {id}.");
                return songs;
            }

            foreach (XElement songElement in songsElement.Elements("song"))
            {
                Song song = new Song();

                song.Path = XMLUtil.GetNodeValue(songElement, "file");

                if (string.IsNullOrWhiteSpace(song.Path))
                {
                    Logger.Error($"Cannot find file path for song in {id}");
                    continue;
                }

                song.Name = XMLUtil.GetNodeValueOrDefault(songElement, "name");
                song.Artist = XMLUtil.GetNodeValueOrDefault(songElement, "artist");
                song.Album = XMLUtil.GetNodeValueOrDefault(songElement.Parent.Parent, "name");
                song.Year = XMLUtil.GetNodeValueOrDefault(songElement, "year");
                song.Length = XMLUtil.GetNodeValueOrDefault(songElement, "length");

                if (int.TryParse(XMLUtil.GetNodeValue(songElement, "force"), out int force))
                {
                    song.Force = force;
                }

                string volString = XMLUtil.GetNodeValue(songElement, "volume");
                if (volString != null && float.TryParse(volString.Replace(",", "."), CultureInfo.InvariantCulture, out float volume))
                {
                    song.Volume = volume;
                }

                songs.Add(song);
            }

            return songs;
        }
    }
}
