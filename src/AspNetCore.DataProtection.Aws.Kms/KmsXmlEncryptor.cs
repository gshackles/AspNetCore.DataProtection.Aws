﻿// Copyright(c) 2016 Jeff Hotchkiss
// Licensed under the MIT License. See License.md in the project root for license information.
using Amazon.KeyManagementService;
using Amazon.KeyManagementService.Model;
using Microsoft.AspNet.DataProtection.XmlEncryption;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace AspNetCore.DataProtection.Aws.Kms
{
    public class KmsXmlEncryptor : IXmlEncryptor
    {
        private readonly ILogger _logger;

        /// <summary>
        /// Creates a <see cref="KmsXmlEncryptor"/> for encrypting ASP.NET keys with a KMS master key
        /// </summary>
        /// <param name="kmsClient">The KMS client.</param>
        /// <param name="config">The configuration object specifying which key data in KMS to use.</param>
        public KmsXmlEncryptor(IAmazonKeyManagementService kmsClient, KmsXmlEncryptorConfig config)
            : this(kmsClient, config, services: null)
        {
        }

        /// <summary>
        /// Creates a <see cref="KmsXmlEncryptor"/> for encrypting ASP.NET keys with a KMS master key
        /// </summary>
        /// <param name="kmsClient">The KMS client.</param>
        /// <param name="config">The configuration object specifying which key data in KMS to use.</param>
        /// <param name="services">An optional <see cref="IServiceProvider"/> to provide ancillary services.</param>
        public KmsXmlEncryptor(IAmazonKeyManagementService kmsClient, KmsXmlEncryptorConfig config, IServiceProvider services)
        {
            if (kmsClient == null)
            {
                throw new ArgumentNullException(nameof(kmsClient));
            }

            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            KmsClient = kmsClient;
            Config = config;
            Services = services;
            _logger = services?.GetService<ILoggerFactory>()?.CreateLogger<KmsXmlEncryptor>();
        }

        /// <summary>
        /// The configuration of how Kms will encrypt the XML data.
        /// </summary>
        public KmsXmlEncryptorConfig Config { get; }

        /// <summary>
        /// The <see cref="IServiceProvider"/> provided to the constructor.
        /// </summary>
        protected IServiceProvider Services { get; }

        /// <summary>
        /// The <see cref="IAmazonKeyManagementService"/> provided to the constructor.
        /// </summary>
        protected IAmazonKeyManagementService KmsClient { get; }

        public EncryptedXmlInfo Encrypt(XElement plaintextElement)
        {
            // Due to time constraints, Microsoft didn't make the interfaces async
            // https://github.com/aspnet/DataProtection/issues/124
            // so loft the heavy lifting into a thread which enables safe async behaviour with some additional cost
            // Overhead should be acceptable since key management isn't a frequent thing
            return Task.Run(() => EncryptAsync(plaintextElement, CancellationToken.None)).Result;
        }

        public async Task<EncryptedXmlInfo> EncryptAsync(XElement plaintextElement, CancellationToken ct)
        {
            _logger?.LogDebug("Encrypting plaintext DataProtection key using AWS key {0}", Config.KeyId);

            // Some implementations of this e.g. DpapiXmlEncryptor go to enormous lengths to
            // create a memory stream, use unsafe code to zero it, and so on.
            //
            // Currently not doing so here, as this appears to neglect that the XElement above is in memory,
            // is a managed construct containing ultimately a System.String, and therefore the plaintext is
            // most assuredly already in several places in memory. Even ignoring that, the following code
            // sending a MemoryStream out over the web is likely about to buffer the plaintext in all sorts of ways.
            //
            // The primary objective of this encryption is to deny a reader of the _external storage_ access to the key.
            //
            // If we'd been starting with SecureString, there'd be a good case for handling the memory carefully.
            using (var memoryStream = new MemoryStream())
            {
                plaintextElement.Save(memoryStream);
                memoryStream.Seek(0, SeekOrigin.Begin);

                var response = await KmsClient.EncryptAsync(new EncryptRequest
                {
                    EncryptionContext = Config.EncryptionContext,
                    GrantTokens = Config.GrantTokens,
                    KeyId = Config.KeyId,
                    Plaintext = memoryStream
                }, ct).ConfigureAwait(false);

                var element = new XElement("encryptedKey",
                    new XComment(" This key is encrypted with AWS Key Management Service. "),
                    new XElement("value", Convert.ToBase64String(response.CiphertextBlob.ToArray())));

                return new EncryptedXmlInfo(element, typeof(KmsXmlDecryptor));
            }
        }
    }
}
