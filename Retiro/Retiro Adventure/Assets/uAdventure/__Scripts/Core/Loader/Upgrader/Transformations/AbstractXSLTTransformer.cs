﻿using System.IO;
using System.Xml;
using System.Xml.Xsl;
using uAdventure.Runner;
using UnityEngine;

namespace uAdventure.Core.XmlUpgrader
{
    public abstract class AbstractXsltTransformer : ITransformer
    {
        public abstract string TargetFile { get; }

        public abstract int TargetVersion { get; }

        public abstract int DestinationVersion { get; }

        protected abstract string XsltFile { get; }

        public string Upgrade(string input, string path, ResourceManager resourceManager)
        {
            return AfterUpgrade(ApplyXslt(BeforeUpgrade(input)));
        }

        protected string ApplyXslt(string input)
        {
            StringWriter sw = new StringWriter();
            var xslAsset = Resources.Load<TextAsset>(XsltFile);
            if(!xslAsset)
            {
                Debug.LogError("Coudn't load upgrader xsl file: " + XsltFile);
                return null;
            }
            XmlReaderSettings settings = new XmlReaderSettings()
            {
                ProhibitDtd = false
            };
            using (XmlReader xri = XmlReader.Create(new StringReader(input), settings))
            using (XmlReader xrt = XmlReader.Create(new StringReader(xslAsset.text), settings))
            using (XmlWriter xwo = XmlWriter.Create(sw))
            {

                XslCompiledTransform xslt = new XslCompiledTransform();
                xslt.Load(xrt);
                xslt.Transform(xri, xwo);
            }
            return sw.ToString();
        }

        protected virtual string BeforeUpgrade(string input) { return input; }

        protected virtual string AfterUpgrade(string input) { return input; }

    }
}