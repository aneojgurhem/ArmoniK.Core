﻿// This file is part of the ArmoniK project
// 
// Copyright (C) ANEO, 2021-2021. All rights reserved.
//   W. Kirschenmann   <wkirschenmann@aneo.fr>
//   J. Gurhem         <jgurhem@aneo.fr>
//   D. Dubuc          <ddubuc@aneo.fr>
//   L. Ziane Khodja   <lzianekhodja@aneo.fr>
//   F. Lemaitre       <flemaitre@aneo.fr>
//   S. Djebbar        <sdjebbar@aneo.fr>
// 
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License as published
// by the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU Affero General Public License for more details.
// 
// You should have received a copy of the GNU Affero General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Security;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

using ArmoniK.Core.Common;
using ArmoniK.Core.Common.Injection;
using ArmoniK.Core.Common.Injection.Options;
using ArmoniK.Core.Common.Storage;

using JetBrains.Annotations;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using StackExchange.Redis;

namespace ArmoniK.Core.Adapters.Redis
{
  public class ObjectStorage : IObjectStorage
  {
    private readonly IDatabaseAsync redis_;

    public ObjectStorage(IDatabaseAsync redis)
      => redis_ = redis;

    /// <inheritdoc />
    public async Task AddOrUpdateAsync(string key, IAsyncEnumerable<byte[]> valueChunks, CancellationToken cancellationToken = default)
    {
      await Task.WhenAll(await valueChunks.Select((chunk, i) => redis_.ListSetByIndexAsync(key,
                                                                                           i,
                                                                                           chunk)).ToListAsync(cancellationToken));
    }

    /// <inheritdoc />
    public async Task AddOrUpdateAsync(string key, IAsyncEnumerable<ReadOnlyMemory<byte>> valueChunks, CancellationToken cancellationToken = default)
    {
      await Task.WhenAll(await valueChunks.Select((chunk, i) => redis_.ListSetByIndexAsync(key,
                                                                                           i,
                                                                                           chunk)).ToListAsync(cancellationToken));
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<byte[]> TryGetValuesAsync(string key, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
      var res = await redis_.ListRangeAsync(key);
      foreach (var redisValue in res)
      {
        cancellationToken.ThrowIfCancellationRequested();
        yield return redisValue;
      }
    }

    /// <inheritdoc />
    public async Task<bool> TryDeleteAsync(string key, CancellationToken cancellationToken = default)
      => await redis_.KeyDeleteAsync(key);

    /// <inheritdoc />
    public IAsyncEnumerable<string> ListKeysAsync(CancellationToken cancellationToken = default)
      => throw new NotImplementedException();
  }

  public static class ServiceCollectionExt
  {
    [PublicAPI]
    public static IServiceCollection AddRedis(
      this IServiceCollection serviceCollection,
      ConfigurationManager    configuration,
      ILogger                 logger)
    {
      var components = configuration.GetSection(Components.SettingSection);

      if (components["ObjectStorage"] == "ArmoniK.Adapters.Redis.ObjectStorage")
      {
        serviceCollection.AddOption<Options.Redis>(configuration,
                                                   Options.Redis.SettingSection,
                                                   out var redisOptions);

        using var _ = logger.BeginNamedScope("Redis configuration",
                                             ("EndpointUrl", redisOptions.EndpointUrl));

        if (!string.IsNullOrEmpty(redisOptions.CredentialsPath))
        {
          configuration.AddJsonFile(redisOptions.CredentialsPath);

          serviceCollection.AddOption(configuration,
                                      Options.Redis.SettingSection,
                                      out redisOptions);

          logger.LogTrace("Loaded Redis credentials from file {path}",
                          redisOptions.CredentialsPath);
        }

        if (!string.IsNullOrEmpty(redisOptions.CaPath))
        {
          var                  localTrustStore       = new X509Store(StoreName.Root);
          var certificateCollection = new X509Certificate2Collection();
          try
          {
            certificateCollection.Import(redisOptions.CaPath);
            localTrustStore.Open(OpenFlags.ReadWrite);
            localTrustStore.AddRange(certificateCollection);
            logger.LogTrace("Imported Redis certificate from file {path}",
                            redisOptions.CaPath);
          }
          catch (Exception ex)
          {
            logger.LogError("Root certificate import failed: {error}",
                            ex.Message);
            throw;
          }
          finally
          {
            localTrustStore.Close();
          }
        }

        var config = new ConfigurationOptions()
        {
          ClientName           = redisOptions.ClientName,
          ReconnectRetryPolicy = new ExponentialRetry(10),
          Ssl                  = redisOptions.Ssl,
          AbortOnConnectFail   = true,
          SslHost              = redisOptions.SslHost,
          Password             = redisOptions.Password,
          User                 = redisOptions.User,
        };
        config.EndPoints.Add(redisOptions.EndpointUrl);

        if (redisOptions.Timeout > 0)
          config.ConnectTimeout = redisOptions.Timeout;

        logger.LogDebug("setup connection to Redis at {EndpointUrl} with user {user}",
                        redisOptions.EndpointUrl,
                        redisOptions.User);

        serviceCollection.AddSingleton<IDatabaseAsync>(_ => ConnectionMultiplexer.Connect(config,
                                                                                          TextWriter.Null).GetDatabase());
        serviceCollection.AddSingleton<IObjectStorage, ObjectStorage>();
      }

      return serviceCollection;
    }
  }
}