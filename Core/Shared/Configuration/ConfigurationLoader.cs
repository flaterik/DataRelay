using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.IO;

namespace MySpace.Configuration
{
    public static class ConfigurationLoader
    {
        private static XmlDocument configDoc = new XmlDocument();
        private static System.Collections.Concurrent.ConcurrentDictionary<string, XmlNode> Sections = new System.Collections.Concurrent.ConcurrentDictionary<string, XmlNode>();
        private static string mainConfigPath = AppDomain.CurrentDomain.SetupInformation.ConfigurationFile;
        public const string REMOTE_SERVER_SECTION = "remoteServerSection";
        public const string CONFIG_SOURCE = "configSource";
        private static FileSystemWatcher newWatcher;

        static ConfigurationLoader()
        {
            LoadConfig();
            newWatcher = new FileSystemWatcher(BaseFolder, MainConfigName);
            newWatcher.Changed += ConfigDirChanged;
            newWatcher.Filter = MainConfigName;
            newWatcher.EnableRaisingEvents = true;
        }


        private static void ConfigDirChanged(object source, FileSystemEventArgs e)
        {
            LoadConfig();
        }

        private static void LoadConfig()
        {
            using (StreamReader reader = new StreamReader(MainConfigPath))
            {
                lock (configDoc)
                {
                    configDoc.RemoveAll();

                    string xml = reader.ReadToEnd();
                    configDoc.LoadXml(xml);
                }

                lock (Sections)
                {
                    Sections.Clear();
                }
            }

        }

        public static string MainConfigPath
        {
            get
            {
                return mainConfigPath;
            }
        }

        public static string BaseFolder
        {
            get
            {
                return Path.GetDirectoryName(MainConfigPath);
            }
        }

        public static string MainConfigName
        {
            get
            {
                return Path.GetFileName(MainConfigPath);
            }
        }

        public static string GetSectionName(string sectionName)
        {
            return GetAttributeValue(sectionName, REMOTE_SERVER_SECTION, sectionName);
        }

        public static string GetConfigSource(string sectionName)
        {
            return GetAttributeValue(sectionName, CONFIG_SOURCE, null);
        }

        private static string GetAttributeValue(string section, string attributeName, string defaultValue)
        {
            XmlNode sectionNode = GetSectionXmlNode(section);

            if (sectionNode != null)
            {
                XmlAttribute sectionAttribute = sectionNode.Attributes[attributeName];
                if (sectionAttribute != null && string.IsNullOrWhiteSpace(sectionAttribute.Value) == false)
                {
                    return sectionAttribute.Value;
                }
            }

            return defaultValue;
        }


        public static XmlNode GetSectionXmlNode(string sectionName)
        {
            if (Sections.ContainsKey(sectionName))
            {
                return Sections[sectionName];
            }
            XmlNodeList section = configDoc.GetElementsByTagName(sectionName);

            XmlNode returnValue = null;
            if (section != null && section.Count > 0)
            {
                returnValue = section[0];
            }

            Sections[sectionName] = returnValue;

            return returnValue;
        }
    }
}
