﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using otor.msixhero.lib.Domain.Appx.Signing;
using otor.msixhero.lib.Infrastructure.Interop;
using otor.msixhero.lib.Infrastructure.Logging;
using otor.msixhero.lib.Infrastructure.Progress;

namespace otor.msixhero.lib.BusinessLayer.Appx.Signing
{
    public class AppxSigningManager : IAppxSigningManager
    {
        private static readonly ILog Logger = LogManager.GetLogger();

        public Task<bool> ExtractCertificateFromMsix(
            string msixFile, 
            string outputFile,
            CancellationToken cancellationToken = default,
            IProgress<ProgressData> progress = null)
        {
            return this.ExtractCertificateFromMsix(msixFile, false, outputFile, cancellationToken, progress);
        }

        public async Task<List<PersonalCertificate>> GetCertificatesFromStore(CertificateStoreType certStoreType, CancellationToken cancellationToken = default, IProgress<ProgressData> progress = null)
        {
            StoreLocation loc;

            switch (certStoreType)
            {
                case CertificateStoreType.User:
                    loc = StoreLocation.CurrentUser;
                    break;
                case CertificateStoreType.Machine:
                    loc = StoreLocation.LocalMachine;
                    break;
                case CertificateStoreType.Both:
                    var list1 = await this.GetCertificatesFromStore(CertificateStoreType.User, cancellationToken, progress).ConfigureAwait(false);
                    var list2 = await this.GetCertificatesFromStore(CertificateStoreType.Machine, cancellationToken, progress).ConfigureAwait(false);
                    return list1.Concat(list2).ToList();

                default:
                    throw new ArgumentOutOfRangeException(nameof(certStoreType), certStoreType, null);
            }

            Logger.Info($"Getting the list of certificates from {loc} containing a private key and in a valid time range.");

            using var store = new X509Store(StoreName.My, loc);
            store.Open(OpenFlags.ReadOnly);

            var list = new List<PersonalCertificate>();
            foreach (var certificate in store.Certificates)
            {
                Logger.Debug("Processing certificate {0}...", certificate);
                cancellationToken.ThrowIfCancellationRequested();

                if (!certificate.HasPrivateKey)
                {
                    Logger.Debug("Skipping certificate because it has no private key.");
                    continue;
                }

                if (certificate.NotBefore > DateTime.UtcNow.ToLocalTime())
                {
                    Logger.Debug("Skipping certificate because it is inactive before {0}.", certificate.NotBefore);
                    continue;
                }

                if (certificate.NotAfter < DateTime.UtcNow.ToLocalTime())
                {
                    Logger.Debug("Skipping certificate because it has expired on {0}.", certificate.NotAfter);
                    continue;
                }

                var cert = new PersonalCertificate
                {
                    DisplayName = certificate.FriendlyName,
                    Date = certificate.NotAfter,
                    Issuer = certificate.Issuer,
                    Subject = certificate.Subject,
                    Thumbprint = certificate.Thumbprint,
                    SignatureAlgorithm = certificate.SignatureAlgorithm.FriendlyName,
                    StoreType = certStoreType
                };

                list.Add(cert);
            }

            store.Close();
            return list;
        }

        public async Task<bool> InstallCertificate(string certificateFile, CancellationToken cancellationToken = default, IProgress<ProgressData> progress = null)
        {
            try
            {
                var scriptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "scripts", "install-certificate.ps1");

                using var ps = await PowerShellSession.CreateForModule("PKI", true).ConfigureAwait(false);
                using var cmd = ps.AddCommand(scriptPath);
                using var paramCerOutputFileName = cmd.AddParameter("CerFileName", certificateFile);
                using var result = await ps.InvokeAsync(progress).ConfigureAwait(false);
                return true;
            }
            catch (COMException e)
            {
                Console.WriteLine("COM Exception " + e.HResult);
                if (e.HResult == -2146885623)
                {
                    // This is to catch COMException 0x80092009 file not found which may be thrown for invalid or missing cert files.
                    throw new Exception("Could not install certificate " + certificateFile + ". The file may be invalid, corrupted or missing. System error code: 0x" + e.HResult.ToString("X2"), e);
                }

                throw;
            }
        }

        public async Task SignPackage(string package, bool updatePublisher, PersonalCertificate certificate, string timestampUrl = null, CancellationToken cancellationToken = default, IProgress<ProgressData> progress = null)
        {
            if (certificate == null)
            {
                throw new ArgumentNullException(nameof(certificate));
            }
            
            Logger.Info("Signing package {0} using personal certificate {1}.", package, certificate.Subject);
            
            StoreLocation loc;

            switch (certificate.StoreType)
            {
                case CertificateStoreType.User:
                    loc = StoreLocation.CurrentUser;
                    break;
                case CertificateStoreType.Machine:
                    loc = StoreLocation.LocalMachine;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            using var store = new X509Store(StoreName.My, loc);
            store.Open(OpenFlags.ReadOnly);

            var x509 = store.Certificates.Find(X509FindType.FindByThumbprint, certificate.Thumbprint, true);
            if (x509.Count < 1)
            {
                throw new ArgumentException("Certificate could not be located in the store.");
            }

            var localCopy = await this.PreparePackageForSigning(package, updatePublisher, x509[0], cancellationToken, progress).ConfigureAwait(false);

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                string type;
                if (x509[0].SignatureAlgorithm.FriendlyName.EndsWith("rsa", StringComparison.OrdinalIgnoreCase))
                {
                    type = x509[0].SignatureAlgorithm.FriendlyName.Substring(0, x509[0].SignatureAlgorithm.FriendlyName.Length - 3).ToUpperInvariant();
                }
                else
                {
                    throw new NotSupportedException($"Signature algorithm {x509[0].SignatureAlgorithm.FriendlyName} is not supported.");
                }

                Logger.Debug("Signing package {0} with algorithm {1}.", localCopy, x509[0].SignatureAlgorithm.FriendlyName);

                var sdk = new MsixSdkWrapper();
                await sdk.SignPackageWithPersonal(localCopy, type, certificate.Thumbprint, certificate.StoreType == CertificateStoreType.Machine, timestampUrl, cancellationToken).ConfigureAwait(false);
                await Task.Delay(500, cancellationToken).ConfigureAwait(false);

                Logger.Debug("Moving {0} to {1}.", localCopy, package);
                File.Copy(localCopy, package, true);
            }
            finally
            {
                try
                {
                    if (File.Exists(localCopy))
                    {
                        File.Delete(localCopy);
                    }
                }
                catch (Exception e)
                {
                    Logger.Warn(e, "Clean-up of a temporary file {0} failed.", localCopy);
                }
            }
        }

        public async Task SignPackage(string package, bool updatePublisher, string pfxPath, SecureString password, string timestampUrl = null, CancellationToken cancellationToken = default, IProgress<ProgressData> progress = null)
        {
            Logger.Info("Signing package {0} using PFX {1}.", package, pfxPath);
            
            if (!File.Exists(pfxPath))
            {
                throw new FileNotFoundException($"File {pfxPath} does not exit.");
            }

            Logger.Debug("Analyzing given certificate...");
            var x509 = new X509Certificate2(await File.ReadAllBytesAsync(pfxPath, cancellationToken).ConfigureAwait(false), password);

            var localCopy = await this.PreparePackageForSigning(package, updatePublisher, x509, cancellationToken, progress).ConfigureAwait(false);

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                string type;
                if (x509.SignatureAlgorithm.FriendlyName.EndsWith("rsa", StringComparison.OrdinalIgnoreCase))
                {
                    type = x509.SignatureAlgorithm.FriendlyName.Substring(0, x509.SignatureAlgorithm.FriendlyName.Length - 3).ToUpperInvariant();
                }
                else
                {
                    throw new NotSupportedException($"Signature algorithm {x509.SignatureAlgorithm.FriendlyName} is not supported.");
                }

                var openTextPassword = new System.Net.NetworkCredential(string.Empty, password).Password;

                Logger.Debug("Signing package {0} with algorithm {1}.", localCopy, x509.SignatureAlgorithm.FriendlyName);

                var sdk = new MsixSdkWrapper();
                await sdk.SignPackageWithPfx(localCopy, type, pfxPath, openTextPassword, timestampUrl, cancellationToken).ConfigureAwait(false);
                await Task.Delay(500, cancellationToken).ConfigureAwait(false);

                Logger.Debug("Moving {0} to {1}.", localCopy, package);
                File.Copy(localCopy, package, true);
            }
            finally
            {
                try
                {
                    if (File.Exists(localCopy))
                    {
                        File.Delete(localCopy);
                    }
                }
                catch (Exception e)
                {
                    Logger.Warn(e, "Clean-up of a temporary file {0} failed.", localCopy);
                }
            }
        }

        public Task<bool> ImportCertificateFromMsix(
            string msixFile,
            CancellationToken cancellationToken = default,
            IProgress<ProgressData> progress = null)
        {
            return this.ExtractCertificateFromMsix(msixFile, true, null, cancellationToken, progress);
        }
        
        public async Task<bool> CreateSelfSignedCertificate(
            DirectoryInfo outputDirectory, 
            string publisherName, 
            string publisherDisplayName, 
            string password,
            CancellationToken cancellationToken = default,
            IProgress<ProgressData> progress = null)
        {
            var scriptPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "scripts", "create-certificate.ps1");

            using var ps = await PowerShellSession.CreateForModule("PKI", true).ConfigureAwait(false);
            using var cmd = ps.AddCommand(scriptPath, false);
            using var paramPublisherFriendlyName = cmd.AddParameter("PublisherFriendlyName", publisherDisplayName);
            using var paramPublisherName = cmd.AddParameter("PublisherName", publisherName);
            using var paramPassword = cmd.AddParameter("Password", password);
            using var paramOutputDirectory = cmd.AddParameter("OutputDirectory", outputDirectory.FullName);
            using var paramPfxOutputFileName = cmd.AddParameter("PfxOutputFileName", null);
            using var paramCerOutputFileName = cmd.AddParameter("CerOutputFileName", null);
            using var paramCreatePasswordFile = cmd.AddParameter("CreatePasswordFile");

            using var result = await ps.InvokeAsync(progress).ConfigureAwait(false);
            return true;
        }
        private async Task<bool> ExtractCertificateFromMsix(
            string msixFile,
            bool importToStore = false,
            string outputFile = null,
            CancellationToken cancellationToken = default,
            IProgress<ProgressData> progress = null)
        {
            var scriptPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "scripts", "extract-certificate-from-msix.ps1");

            using var ps = await PowerShellSession.CreateForModule("PKI", true).ConfigureAwait(false);
            using var cmd = ps.AddCommand(scriptPath, false);
            using var paramSourceMsixFile = cmd.AddParameter("SourceMsixFile", msixFile);

            if (outputFile != null)
            {
                using var paramCerOutputFileName = cmd.AddParameter("CerOutputFileName", outputFile);
            }

            if (importToStore)
            {
                using var paramImportToStore = cmd.AddParameter("ImportToStore");
            }

            using var result = await ps.InvokeAsync(progress).ConfigureAwait(false);
            return true;
        }

        private async Task<string> PreparePackageForSigning(string package, bool updatePublisher, X509Certificate certificate, CancellationToken cancellationToken = default, IProgress<ProgressData> progress = null)
        {
            if (!File.Exists(package))
            {
                throw new FileNotFoundException($"File {package} does not exit.");
            }

            var localCopy = Path.GetTempFileName() + Path.GetExtension(package);

            var sdk = new MsixSdkWrapper();
            if (updatePublisher)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Logger.Info("Updating Publisher property based on the PFX file...");

                var tempDirectory = Path.Combine(Path.GetTempPath(), "MSIX-Hero", Guid.NewGuid().ToString("N").Substring(0, 8).ToUpperInvariant());

                try
                {
                    Logger.Debug("Unpacking {0} to {1}.", package, tempDirectory);
                    await sdk.UnpackPackage(package, tempDirectory, cancellationToken);

                    var manifestFilePath = Path.Combine(tempDirectory, "AppxManifest.xml");
                    if (!File.Exists(manifestFilePath))
                    {
                        throw new FileNotFoundException($"Package {package} contains no XML manifest.");
                    }

                    string newXmlContent;

                    Logger.Debug("Opening manifest file {0}.", manifestFilePath);
                    using (var stream = File.OpenRead(manifestFilePath))
                    {
                        var xmlDocument = new XmlDocument();
                        xmlDocument.Load(stream);
                        var identity = xmlDocument.SelectSingleNode("/*[local-name()='Package']/*[local-name()='Identity']");

                        var publisher = identity.Attributes["Publisher"];
                        if (publisher == null)
                        {
                            publisher = xmlDocument.CreateAttribute("Publisher");
                            identity.Attributes.Append(publisher);
                        }

                        Logger.Info("Replacing Publisher '{0}' with '{1}'", publisher.InnerText, certificate.Subject);
                        publisher.InnerText = certificate.Subject;
                        newXmlContent = xmlDocument.OuterXml;
                    }

                    File.Delete(manifestFilePath);

                    cancellationToken.ThrowIfCancellationRequested();
                    await File.WriteAllTextAsync(manifestFilePath, newXmlContent, cancellationToken).ConfigureAwait(false);

                    if (File.Exists(localCopy))
                    {
                        File.Delete(localCopy);
                    }

                    Logger.Debug("Packing {0} to {1}.", tempDirectory, localCopy);
                    cancellationToken.ThrowIfCancellationRequested();
                    await sdk.PackPackage(tempDirectory, localCopy, cancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    try
                    {
                        Logger.Trace("Deleting temporary directory {0}.", tempDirectory);
                        Directory.Delete(tempDirectory, true);
                    }
                    catch (Exception e)
                    {
                        Logger.Warn(e, "Clean-up of temporary directory {0} failed.", tempDirectory);
                    }
                }
            }
            else
            {
                Logger.Debug("Copying {0} to {1}.", package, localCopy);
                File.Copy(package, localCopy, true);
            }

            return localCopy;
        }
    }
}