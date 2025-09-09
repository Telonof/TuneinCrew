using System.Xml.Linq;

namespace TuneinCrew.Utilities
{
    internal class XMLUtil
    {
        public static string? GetNodeValue(XElement parent, string elementName)
        {
            return parent.Element(elementName)?.Value;
        }

        public static string GetNodeValueOrDefault(XElement parent, string elementName)
        {
            string value = GetNodeValue(parent, elementName);
            if (value == null || string.IsNullOrWhiteSpace(value))
                return "UNKNOWN";

            return value;
        }

        public static XElement? FindType(XElement element, string type, string attributeID, string attributeName)
        {
            return element.Descendants(type).FirstOrDefault(f => (string)f.Attribute(attributeID) == attributeName);
        }
    }
}
