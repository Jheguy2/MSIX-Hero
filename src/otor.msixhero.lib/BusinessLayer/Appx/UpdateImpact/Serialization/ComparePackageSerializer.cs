﻿using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text;
using System.Xml.Serialization;
using otor.msixhero.lib.Domain.Appx.UpdateImpact.ComparePackage;
using File = System.IO.File;

namespace otor.msixhero.lib.BusinessLayer.Appx.UpdateImpact.Serialization
{
    public class ComparePackageSerializer
    {
        public SdkComparePackage Deserialize([NotNull] Stream stream)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }
            
            using (TextReader tr = new StreamReader(stream))
            {
                return this.Deserialize(tr);
            }
        }

        public SdkComparePackage Deserialize([NotNull] string xml)
        {
            if (xml == null)
            {
                throw new ArgumentNullException(nameof(xml));
            }

            using (TextReader tr = new StringReader(xml))
            {
                return this.Deserialize(tr);
            }
        }

        public SdkComparePackage Deserialize([NotNull] FileInfo file)
        {
            if (file == null)
            {
                throw new ArgumentNullException(nameof(file));
            }

            using (var fs = File.OpenRead(file.FullName))
            {
                return this.Deserialize(fs);
            }
        }

        public SdkComparePackage Deserialize([NotNull] TextReader reader)
        {
            if (reader == null)
            {
                throw new ArgumentNullException(nameof(reader));
            }

            var serializer = new XmlSerializer(typeof(SdkComparePackage));

            return (SdkComparePackage)serializer.Deserialize(reader);
        }

        public void Serialize([NotNull] SdkComparePackage package, [NotNull] Stream stream)
        {
            if (package == null)
            {
                throw new ArgumentNullException(nameof(package));
            }
         
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }
            
            using (TextWriter textWriter = new StreamWriter(stream))
            {
                this.Serialize(package, textWriter);
            }
        }

        public string Serialize([NotNull] SdkComparePackage package)
        {
            if (package == null)
            {
                throw new ArgumentNullException(nameof(package));
            }

            var stringBuilder = new StringBuilder();

            using (TextWriter textWriter = new StringWriter(stringBuilder))
            {
                this.Serialize(package, textWriter);
                return stringBuilder.ToString();
            }
        }

        public void Serialize([NotNull] SdkComparePackage package, [NotNull] TextWriter writer)
        {
            if (package == null)
            {
                throw new ArgumentNullException(nameof(package));
            }

            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }

            var serializer = new XmlSerializer(typeof(SdkComparePackage));
            serializer.Serialize(writer, package);
        }

        public void Serialize([NotNull] SdkComparePackage package, [NotNull] FileInfo file)
        {
            if (package == null)
            {
                throw new ArgumentNullException(nameof(package));
            }

            if (file == null)
            {
                throw new ArgumentNullException(nameof(file));
            }

            if (file.Directory != null && !file.Directory.Exists)
            {
                file.Directory.Create();
            }

            using (var fs = File.OpenWrite(file.FullName))
            {
                this.Serialize(package, fs);
            }
        }
    }
}
