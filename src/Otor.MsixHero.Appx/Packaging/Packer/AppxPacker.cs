﻿// MSIX Hero
// Copyright (C) 2021 Marcin Otorowski
// 
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// Full notice:
// https://github.com/marcinotorowski/msix-hero/blob/develop/LICENSE.md

using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Otor.MsixHero.Appx.Packaging.Packer.Enums;
using Otor.MsixHero.Infrastructure.Branding;
using Otor.MsixHero.Infrastructure.Progress;
using Otor.MsixHero.Infrastructure.ThirdParty.Sdk;

namespace Otor.MsixHero.Appx.Packaging.Packer
{
    public class AppxPacker : IAppxPacker
    {
        public async Task PackFiles(string directory, string packagePath, AppxPackerOptions options = 0, CancellationToken cancellationToken = default, IProgress<ProgressData> progress = default)
        {
            var manifestFile = Path.Combine(directory, "AppxManifest.xml");
            if (!File.Exists(manifestFile))
            {
                throw new FileNotFoundException("Manifest file has not been found.", manifestFile);
            }

            XDocument xmlDocument;
            using (var stream = File.OpenRead(manifestFile))
            {
                using (var streamReader = new StreamReader(stream))
                {
                    xmlDocument = await XDocument.LoadAsync(streamReader, LoadOptions.None, cancellationToken).ConfigureAwait(false);
                }
            }

            var injector = new MsixHeroBrandingInjector();
            injector.Inject(xmlDocument);

            cancellationToken.ThrowIfCancellationRequested();

            await File.WriteAllTextAsync(manifestFile, xmlDocument.ToString(), Encoding.UTF8, cancellationToken).ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();

            var compress = !options.HasFlag(AppxPackerOptions.NoCompress);
            var validate = !options.HasFlag(AppxPackerOptions.NoValidation);

            var allDirs = new DirectoryInfo(directory).EnumerateDirectories("*", SearchOption.AllDirectories);
            foreach (var emptyDir in allDirs.Where(d =>
                !d.EnumerateFiles("*", SearchOption.TopDirectoryOnly).Any() &&
                !d.EnumerateDirectories("*", SearchOption.TopDirectoryOnly).Any()))
            {
                // this means we have an empty folder, which requires some special handling
                await File.WriteAllBytesAsync(Path.Combine(emptyDir.FullName, "GeneratedFile.txt"), new byte[0], cancellationToken).ConfigureAwait(false);
            }

            await new MsixSdkWrapper().PackPackageDirectory(directory, packagePath, compress, validate, cancellationToken, progress).ConfigureAwait(false);
        }

        public async Task Pack(string directory, string packagePath, AppxPackerOptions options = 0, CancellationToken cancellationToken = default, IProgress<ProgressData> progress = null)
        {
            if (!Directory.Exists(directory))
            {
                throw new DirectoryNotFoundException($"Folder {directory} does not exist.");
            }

            var fileInfo = new FileInfo(packagePath);
            if (fileInfo.Directory == null)
            {
                throw new ArgumentException($"File path {packagePath} is not supported.", nameof(packagePath));
            }

            if (!fileInfo.Directory.Exists)
            {
                fileInfo.Directory.Create();
            }

            var tempFile = Path.GetTempFileName();
            var tempManifest = Path.GetTempFileName();
            var tempAutoGenerated = Path.GetTempFileName();
            try
            {
                var inputDirectory = new DirectoryInfo(directory);

                var stringBuilder = new StringBuilder();
                stringBuilder.AppendLine("[Files]");

                foreach (var item in inputDirectory.EnumerateFiles("*", SearchOption.AllDirectories))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var relativePath = Path.GetRelativePath(directory, item.FullName);

                    if (string.IsNullOrEmpty(relativePath))
                    {
                        continue;
                    }

                    // ReSharper disable once ConvertIfStatementToConditionalTernaryExpression
                    if (string.Equals("AppxManifest.xml", relativePath, StringComparison.OrdinalIgnoreCase))
                    {
                        stringBuilder.AppendLine($"\"{tempManifest}\"\t\"{relativePath}\"");
                        item.CopyTo(tempManifest, true);
                        continue;
                    }

                    if (
                        string.Equals("AppxBlockMap.xml", relativePath, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals("AppxSignature.p7x", relativePath, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                    
                    stringBuilder.AppendLine($"\"{item.FullName}\"\t\"{relativePath}\"");
                }

                var allDirs = inputDirectory.EnumerateDirectories("*", SearchOption.AllDirectories);
                foreach (var emptyDir in allDirs.Where(d =>
                    !d.EnumerateFiles("*", SearchOption.TopDirectoryOnly).Any() &&
                    !d.EnumerateDirectories("*", SearchOption.TopDirectoryOnly).Any()))
                {
                    // this means we have an empty folder, which requires some special handling
                    if (new FileInfo(tempAutoGenerated).Length == 0)
                    {
                        await File.WriteAllBytesAsync(tempAutoGenerated, new byte[0], cancellationToken).ConfigureAwait(false);
                    }

                    var relativePath = Path.GetRelativePath(inputDirectory.FullName, emptyDir.FullName);
                    stringBuilder.AppendLine($"\"{tempAutoGenerated}\"\t\"{relativePath}\\GeneratedFile.txt\"");
                }

                await File.WriteAllTextAsync(tempFile, stringBuilder.ToString(), Encoding.UTF8, cancellationToken).ConfigureAwait(false);

                var xmlDocument = XDocument.Load(tempManifest);
                var injector = new MsixHeroBrandingInjector();
                injector.Inject(xmlDocument);

                cancellationToken.ThrowIfCancellationRequested();

                await File.WriteAllTextAsync(tempManifest, xmlDocument.ToString(), Encoding.UTF8, cancellationToken).ConfigureAwait(false);

                cancellationToken.ThrowIfCancellationRequested();

                var compress = !options.HasFlag(AppxPackerOptions.NoCompress);
                var validate = !options.HasFlag(AppxPackerOptions.NoValidation);

                await new MsixSdkWrapper().PackPackageFiles(tempFile, packagePath, compress, validate, cancellationToken, progress).ConfigureAwait(false);
            }
            finally
            {
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }

                if (File.Exists(tempManifest))
                {
                    File.Delete(tempManifest);
                }

                if (File.Exists(tempAutoGenerated))
                {
                    File.Delete(tempAutoGenerated);
                }
            }
        }

        public Task Unpack(string packagePath, string directory, CancellationToken cancellationToken = default, IProgress<ProgressData> progress = null)
        {
            if (!File.Exists(packagePath))
            {
                throw new FileNotFoundException($"File {packagePath} does not exist.");
            }

            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            return new MsixSdkWrapper().UnpackPackage(packagePath, directory, cancellationToken, progress);
        }
    }
}