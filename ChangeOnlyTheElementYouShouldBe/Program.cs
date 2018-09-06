namespace ChangeOnlyTheElementYouShouldBe
{
    using System.Xml;
    using System.Configuration;
    using System.IO;
    using System;
    using System.Linq;

    class Program
    {
        static void Main(string[] args)
        {
            UseIXmlLineInfo();
        }

        internal static void UseIXmlLineInfo()
        {
            var sourceFilePath = GetSourceFilePath();
            var targetFilePath = BuildTargetFilePath(nameof(UseIXmlLineInfo));

            var fileContent = File.ReadAllLines(sourceFilePath).ToList();
            var hadToModContent = false;

            var xmlDocument = new XmlDocument();
            xmlDocument.PreserveWhitespace = true;
            using (var inputStream = File.OpenRead(sourceFilePath))
            {
                var xmlReaderSettings = new XmlReaderSettings();
                xmlReaderSettings.IgnoreWhitespace = false;
                xmlReaderSettings.IgnoreComments = false;
                var xmlReader = XmlReader.Create(inputStream, xmlReaderSettings);

                while (xmlReader.Read())
                {
                    if (xmlReader.NodeType != XmlNodeType.Element)
                    {
                        continue;
                    }

                    if (xmlReader.Name.Equals("assemblyBinding") == false)
                    {
                        continue;
                    }

                    var startingLineInfo = GetLineInfo(xmlReader);
                    var asmBindingStartingLineNumber = startingLineInfo.LineNumber;
                    var asmBindingStartingLinePos = startingLineInfo.LinePosition;

                    var asmBindingElement = xmlReader.ReadOuterXml();

                    xmlReader.Skip();

                    var nextElementLineInfo = GetLineInfo(xmlReader);
                    var asmBindingEndingLineNumber = nextElementLineInfo.LineNumber - 1;

                    var rewrittenAsmBindingElement = RewriteAssemblyBindings(asmBindingElement);
                    if (asmBindingElement.Equals(rewrittenAsmBindingElement) == false)
                    {
                        var previousLineDistance = asmBindingEndingLineNumber - asmBindingStartingLineNumber;
                        for (int i = asmBindingEndingLineNumber - 1; i >= asmBindingStartingLineNumber - 1; i--)
                        {
                            fileContent.RemoveAt(i);
                        }

                        var rewrittenLines = rewrittenAsmBindingElement.Split(new[] { Environment.NewLine, "\n" }, StringSplitOptions.None);
                        rewrittenLines[0] = $"{new string(' ', asmBindingStartingLinePos - 2)}{rewrittenLines[0]}";
                        for (int i = 0; i < rewrittenLines.Length; i++)
                        {
                            fileContent.Insert(asmBindingStartingLineNumber - 1 + i, rewrittenLines[i]);
                        }

                        hadToModContent = true;
                    }
                }
            }

            if (hadToModContent)
            {
                //very important, touch only the files that needed it
                File.WriteAllLines(targetFilePath, fileContent);
            }
        }

        private static IXmlLineInfo GetLineInfo(XmlReader xmlReader)
        {
            var lineInfo = xmlReader as IXmlLineInfo;
            if (lineInfo?.HasLineInfo() == false)
            {
                throw new InvalidOperationException("Cant get line information from this xml reader");
            }

            return lineInfo;
        }

        private static string RewriteAssemblyBindings(string existingOuterXml)
        {
            //this could also take xmlReader.ReadSubtree() and iterate that way. It just 
            //  needs to return a string that is either exactly equal to the parameter value
            //  to indicate no changes were needed, or the actual changed XML
            var xdoc = new XmlDocument();
            xdoc.PreserveWhitespace = true;
            xdoc.LoadXml(existingOuterXml);
            var contentWasModified = false;
            for (int i = 0; i < xdoc.DocumentElement.ChildNodes.Count; i++)
            {
                var childNode = xdoc.DocumentElement.ChildNodes[i];
                if (childNode.NodeType != XmlNodeType.Element)
                {
                    continue;
                }
                //inspect the element and see if any changes are needed to it. 
                // This is just a forced change to see the result file only has this element modified in it
                var dumAttribute = xdoc.CreateAttribute("MuhDumAttrib");
                dumAttribute.Value = "cMeDum";
                childNode.Attributes.Append(dumAttribute);
                contentWasModified = true;
            }

            // if existing binding was not found, add it and set the contentWasModified = true; 


            // if am existing binding needs to be removed, remove it and set the contentWasModified = true;

            if (contentWasModified)
            {
                return xdoc.DocumentElement.OuterXml;
            }

            return existingOuterXml;
        }

        private static string GetSourceFilePath()
        {
            return ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None).FilePath;
        }

        internal static string BuildTargetFilePath(string attemptType)
        {
            var thisConfigPath = GetSourceFilePath();
            var targetFilePath = $"{thisConfigPath}.{attemptType}.xml";
            if (File.Exists(targetFilePath))
            {
                File.Delete(targetFilePath);
            }
            return targetFilePath;
        }
    }
}
