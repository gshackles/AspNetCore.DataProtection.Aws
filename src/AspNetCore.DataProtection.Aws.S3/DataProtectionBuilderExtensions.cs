﻿using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNet.DataProtection;
using Microsoft.AspNet.DataProtection.Repositories;
using Amazon.S3;
// Copyright(c) 2016 Jeff Hotchkiss
// Licensed under the MIT License. See License.md in the project root for license information.

namespace AspNetCore.DataProtection.Aws.S3
{
    /// <summary>
    /// Extensions for configuring data protection using an <see cref="IDataProtectionBuilder"/>.
    /// </summary>
    /// <remarks>
    /// Taken almost verbatim from https://github.com/aspnet/DataProtection/blob/release/src/Microsoft.AspNetCore.DataProtection/DataProtectionBuilderExtensions.cs
    /// </remarks>
    public static class DataProtectionBuilderExtensions
    {
        /// <summary>
        /// Configures the data protection system to persist keys to a specified S3 bucket.
        /// </summary>
        /// <param name="builder">The <see cref="DataProtectionConfiguration"/>.</param>
        /// <param name="s3Client">S3 client configured with appropriate credentials.</param>
        /// <param name="config">The configuration object specifying how to write to S3.</param>
        /// <returns>A reference to the <see cref="DataProtectionConfiguration" /> after this operation has completed.</returns>
        public static DataProtectionConfiguration PersistKeysToAwsS3(this DataProtectionConfiguration builder, IAmazonS3 s3Client, S3XmlRepositoryConfig config)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            if (s3Client == null)
            {
                throw new ArgumentNullException(nameof(s3Client));
            }

            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            Use(builder.Services, ServiceDescriptor.Singleton<IXmlRepository>(services => new S3XmlRepository(s3Client, config, services)));
            return builder;
        }

        /// <summary>
        /// Configures the data protection system to persist keys to a specified S3 bucket.
        /// </summary>
        /// <param name="builder">The <see cref="DataProtectionConfiguration"/>.</param>
        /// <param name="config">The configuration object specifying how to write to S3.</param>
        /// <returns>A reference to the <see cref="DataProtectionConfiguration" /> after this operation has completed.</returns>
        public static DataProtectionConfiguration PersistKeysToAwsS3(this DataProtectionConfiguration builder, S3XmlRepositoryConfig config)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            Use(builder.Services, ServiceDescriptor.Singleton<IXmlRepository>(services => new S3XmlRepository(services.GetRequiredService<IAmazonS3>(), config, services)));
            return builder;
        }

        private static void RemoveAllServicesOfType(IServiceCollection services, Type serviceType)
        {
            // We go backward since we're modifying the collection in-place.
            for (int i = services.Count - 1; i >= 0; i--)
            {
                if (services[i]?.ServiceType == serviceType)
                {
                    services.RemoveAt(i);
                }
            }
        }

        private static void Use(IServiceCollection services, ServiceDescriptor descriptor)
        {
            RemoveAllServicesOfType(services, descriptor.ServiceType);
            services.Add(descriptor);
        }
    }
}
