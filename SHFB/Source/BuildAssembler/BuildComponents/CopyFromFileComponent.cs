// Copyright � Microsoft Corporation.
// This source file is subject to the Microsoft Permissive License.
// See http://www.microsoft.com/resources/sharedsource/licensingbasics/sharedsourcelicenses.mspx.
// All other rights reserved.

// 01/18/2013 - EFW - Moved CopyFromFileCommand into its own file.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.Xml.XPath;

using Microsoft.Ddue.Tools.Commands;

using Sandcastle.Core.BuildAssembler;
using Sandcastle.Core.BuildAssembler.BuildComponent;

namespace Microsoft.Ddue.Tools
{
    /// <summary>
    /// This build component copies elements from one or more constant XML files into the target document based
    /// on one or more copy commands that define the elements to copy and where to put them.
    /// </summary>
    public class CopyFromFileComponent : BuildComponentCore
    {
        #region Private data members
        //=====================================================================

        private List<CopyFromFileCommand> copyCommands = new List<CopyFromFileCommand>();
        private CustomContext context = new CustomContext();
        #endregion

        #region Constructor
        //=====================================================================

        /// <inheritdoc />
        public CopyFromFileComponent(BuildAssemblerCore assembler, XPathNavigator configuration) :
          base(assembler, configuration)
        {
            Dictionary<string, XPathDocument> dataFiles = new Dictionary<string, XPathDocument>();

            if(configuration == null)
                throw new ArgumentNullException("configuration");

            // Get information about the data files.  There may be more than one.  If so, each must have a
            // unique name.  If there is only one, the name can be omitted.
            XPathNodeIterator dataNodes = configuration.Select("data");

            foreach(XPathNavigator dataNode in dataNodes)
            {
                string dataFile = dataNode.GetAttribute("file", String.Empty);

                if(String.IsNullOrWhiteSpace(dataFile))
                    base.WriteMessage(MessageLevel.Error, "Data elements must have a file attribute specifying " +
                        "a file from which to load data");

                dataFile = Environment.ExpandEnvironmentVariables(dataFile);

                string dataName = dataNode.GetAttribute("name", String.Empty);

                if(String.IsNullOrWhiteSpace(dataName))
                    dataName = Guid.NewGuid().ToString();

                // Load a schema, if one is specified
                string schemaFile = dataNode.GetAttribute("schema", String.Empty);

                XmlReaderSettings settings = new XmlReaderSettings();

                if(!String.IsNullOrWhiteSpace(schemaFile))
                    settings.Schemas.Add(null, schemaFile);

                // Load the document
                base.WriteMessage(MessageLevel.Info, "Loading data file '{0}'.", dataFile);

                using(XmlReader reader = XmlReader.Create(dataFile, settings))
                {
                    dataFiles.Add(dataName, new XPathDocument(reader));
                }
            }

            if(dataFiles.Count == 0)
                base.WriteMessage(MessageLevel.Error, "At least one data element is required to specify the " +
                    "file from which to load data");

            // Get the source and target expressions for each copy command
            XPathNodeIterator copyNodes = configuration.Select("copy");

            foreach(XPathNavigator copyNode in copyNodes)
            {
                string sourceName = copyNode.GetAttribute("name", String.Empty);

                // If not specified, assume the last key is the one to use
                if(String.IsNullOrWhiteSpace(sourceName))
                    sourceName = dataFiles.Keys.Last();

                string sourceXPath = copyNode.GetAttribute("source", String.Empty);

                if(String.IsNullOrWhiteSpace(sourceXPath))
                    base.WriteMessage(MessageLevel.Error, "When instantiating a CopyFromFileComponent, you " +
                        "must specify a source XPath format using the source attribute");

                string targetXPath = copyNode.GetAttribute("target", String.Empty);

                if(String.IsNullOrEmpty(targetXPath))
                    base.WriteMessage(MessageLevel.Error, "When instantiating a CopyFromFileComponent, you " +
                        "must specify a target XPath format using the target attribute");

                copyCommands.Add(new CopyFromFileCommand(this, dataFiles[sourceName], sourceXPath, targetXPath));
            }

            base.WriteMessage(MessageLevel.Info, "Loaded {0} copy commands", copyCommands.Count);
        }
        #endregion

        #region Method overrides
        //=====================================================================

        /// <inheritdoc />
        public override void Apply(XmlDocument document, string key)
        {
            // Set the key in the XPath context
            context["key"] = key;

            // Perform each copy command
            foreach(CopyFromFileCommand copyCommand in copyCommands)
                copyCommand.Apply(document, context);
        }
        #endregion
    }
}
