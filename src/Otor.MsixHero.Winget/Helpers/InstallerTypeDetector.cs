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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Otor.MsixHero.Winget.Yaml.Entities;

namespace Otor.MsixHero.Winget.Helpers
{
    public class InstallerTypeDetector
    {
        // ReSharper disable once IdentifierTypo
        private static IEnumerable<byte?[]> GetNsisSignature()
        {
            yield return new byte?[] { 0x4e, 0x75, 0x6c, 0x6c, 0x73, 0x6f, 0x66, 0x74, 0x49, 0x6e, 0x73, 0x74 };
        }

        // ReSharper disable once IdentifierTypo
        private static IEnumerable<byte?[]> GetInnoSetupSignature()
        {
            yield return new byte?[] { 0x49, 0x6e, 0x6e, 0x6f, 0x20, 0x53, 0x65, 0x74, 0x75, 0x70 };
        }

        public async Task<YamlInstallerType> DetectSetupType(string fileName, CancellationToken cancellationToken = default)
        {
            if (fileName == null)
            {
                throw new ArgumentNullException(nameof(fileName));
            }

            var fileInfo = new FileInfo(fileName);
            if (!fileInfo.Exists)
            {
                throw new FileNotFoundException("File not found.", fileName);
            }

            if (string.Equals(fileInfo.Name, "AppxManifest.xml", StringComparison.OrdinalIgnoreCase))
            {
                return YamlInstallerType.msix;
            }
            
            switch (fileInfo.Extension.ToLowerInvariant())
            {
                case ".msix":
                // ReSharper disable once StringLiteralTypo
                case ".msixbundle":
                    return YamlInstallerType.msix;
                case ".appx":
                // ReSharper disable once StringLiteralTypo
                case ".appxbundle":
                    return YamlInstallerType.appx;
                case ".msi":
                    return YamlInstallerType.msi;
            }

            await using var fs = File.OpenRead(fileName);
            var recognized = await this.DetectSetupType(fs, cancellationToken).ConfigureAwait(false);
            if (recognized == YamlInstallerType.none && fileInfo.Extension.ToLowerInvariant() == ".exe")
            {
                return YamlInstallerType.exe;
            }

            return recognized;
        }

        public async Task<YamlInstallerType> DetectSetupType(Stream fileStream, CancellationToken cancellationToken = default)
        {
            foreach (var item in GetNsisSignature())
            {
                fileStream.Seek(0, SeekOrigin.Begin);
                var index = await this.Find(fileStream, item, cancellationToken).ConfigureAwait(false);
                if (index != -1)
                {
                    return YamlInstallerType.nullsoft;
                }
            }

            foreach (var item in GetInnoSetupSignature())
            {
                fileStream.Seek(0, SeekOrigin.Begin);
                var index = await this.Find(fileStream, item, cancellationToken).ConfigureAwait(false);
                if (index != -1)
                {
                    return YamlInstallerType.inno;
                }
            }

            return YamlInstallerType.none;
        }

        private async Task<long> Find(Stream stream, byte?[] toFind, CancellationToken cancellationToken = default)
        {
            var readBytesNumber = Math.Max(4096, toFind.Length * 20);
            var firstIndex = -1L;
            
            for (var i = 0; i < toFind.Length;)
            {
                // now advance position
                var advanceBy = toFind.Skip(i).TakeWhile(b => !b.HasValue).Count();
                i += advanceBy;
                stream.Seek(advanceBy, SeekOrigin.Current);

                // ReSharper disable once PossibleInvalidOperationException
                var takeBytes = toFind.Skip(i).TakeWhile(b => b.HasValue).Select(b => b.Value).ToArray();

                if (takeBytes.Length == 0)
                {
                    continue;
                }
                
                i += takeBytes.Length;

                var index = await FindChunk(stream, readBytesNumber, takeBytes, cancellationToken).ConfigureAwait(false);
                if (index == -1)
                {
                    if (firstIndex == -1)
                    {
                        return -1;
                    }

                    i = 0;

                    // we need to seek the stream back, so that we can restart searching the needle.
                    stream.Seek(firstIndex + 1, SeekOrigin.Begin);
                    firstIndex = -1;
                }
                else
                {
                    if (firstIndex == -1)
                    {
                        firstIndex = index;
                    }

                    stream.Seek(firstIndex + i, SeekOrigin.Begin);
                }
            }

            return firstIndex;
        }

        private static async Task<long> FindChunk(Stream stream, int readBytesNumber, byte[] toFind, CancellationToken cancellationToken = default)
        {
            var bm = new BoyerMoore(toFind);

            var searchBuffer = new byte[readBytesNumber];
            var excess = 0;
            var offset = stream.Position;

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                var read = await stream.ReadAsync(searchBuffer, excess, readBytesNumber - excess, cancellationToken).ConfigureAwait(false);
                if (read < 1)
                {
                    return -1;
                }

                var found = bm.Search(searchBuffer);
                if (found != -1)
                {
                    return offset + found;
                }

                excess = toFind.Length - 1;
                Array.Copy(searchBuffer, readBytesNumber - excess, searchBuffer, 0, excess);
                offset += readBytesNumber - excess;
            }
        }
    }
}
