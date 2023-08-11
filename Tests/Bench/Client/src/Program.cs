// This file is part of the ArmoniK project
// 
// Copyright (C) ANEO, 2021-2023. All rights reserved.
// 
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License as published
// by the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY, without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU Affero General Public License for more details.
// 
// You should have received a copy of the GNU Affero General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.

using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using ArmoniK.Api.Client.Options;
using ArmoniK.Api.Client.Submitter;
using ArmoniK.Api.Common.Utils;

using Armonik.Api.gRPC.V1;

using ArmoniK.Api.gRPC.V1;
using ArmoniK.Api.gRPC.V1.Events;
using ArmoniK.Api.gRPC.V1.Partitions;
using ArmoniK.Api.gRPC.V1.Results;
using ArmoniK.Api.gRPC.V1.SortDirection;
using ArmoniK.Api.gRPC.V1.Submitter;
using ArmoniK.Core.Common.Tests.Client;
using ArmoniK.Samples.Bench.Client.Options;
using ArmoniK.Utils;

using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;

using Grpc.Core;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

using Serilog;
using Serilog.Formatting.Compact;

using FilterField = ArmoniK.Api.gRPC.V1.Partitions.FilterField;
using Filters = ArmoniK.Api.gRPC.V1.Partitions.Filters;
using FiltersAnd = ArmoniK.Api.gRPC.V1.Partitions.FiltersAnd;
using TaskStatus = ArmoniK.Api.gRPC.V1.TaskStatus;

namespace ArmoniK.Samples.Bench.Client;

internal static class Program
{
  private static async Task Main()
  {
    var builder       = new ConfigurationBuilder().AddEnvironmentVariables();
    var configuration = builder.Build();
    var seriLogger = new LoggerConfiguration().ReadFrom.Configuration(configuration)
                                              .Enrich.FromLogContext()
                                              .WriteTo.Console(new CompactJsonFormatter())
                                              .CreateBootstrapLogger();

    var logger = new LoggerFactory().AddSerilog(seriLogger)
                                    .CreateLogger("Bench Program");

    var options = configuration.GetRequiredSection(GrpcClient.SettingSection)
                               .Get<GrpcClient>();
    logger.LogInformation("gRPC options : {@grpcOptions}",
                          options);
    var benchOptions = new BenchOptions();
    configuration.GetSection(BenchOptions.SettingSection)
                 .Bind(benchOptions);
    logger.LogInformation("bench options : {@benchOptions}",
                          benchOptions);
    using var _ = logger.BeginPropertyScope(("@benchOptions", benchOptions));

    var channelPool = new ObjectPool<ChannelBase>(() => GrpcChannelFactory.CreateChannel(options!));

    // Get List of partitions for logging purpose
    var partitions = await channelPool.WithInstanceAsync(async channel =>
                                                         {
                                                           var client = new Partitions.PartitionsClient(channel);
                                                           var req = new ListPartitionsRequest
                                                                     {
                                                                       Filters = new Filters
                                                                                 {
                                                                                   Or =
                                                                                   {
                                                                                     new FiltersAnd
                                                                                     {
                                                                                       And =
                                                                                       {
                                                                                         new FilterField
                                                                                         {
                                                                                           Field = new PartitionField
                                                                                                   {
                                                                                                     PartitionRawField = new PartitionRawField
                                                                                                                         {
                                                                                                                           Field = PartitionRawEnumField.Id,
                                                                                                                         },
                                                                                                   },
                                                                                           FilterString = new FilterString
                                                                                                          {
                                                                                                            Operator = FilterStringOperator.Equal,
                                                                                                            Value    = "",
                                                                                                          },
                                                                                         },
                                                                                         new FilterField
                                                                                         {
                                                                                           Field = new PartitionField
                                                                                                   {
                                                                                                     PartitionRawField = new PartitionRawField
                                                                                                                         {
                                                                                                                           Field = PartitionRawEnumField
                                                                                                                             .ParentPartitionIds,
                                                                                                                         },
                                                                                                   },
                                                                                           FilterArray = new FilterArray
                                                                                                         {
                                                                                                           Operator = FilterArrayOperator.Contains,
                                                                                                           Value    = "",
                                                                                                         },
                                                                                         },
                                                                                         new FilterField
                                                                                         {
                                                                                           Field = new PartitionField
                                                                                                   {
                                                                                                     PartitionRawField = new PartitionRawField
                                                                                                                         {
                                                                                                                           Field = PartitionRawEnumField.PodMax,
                                                                                                                         },
                                                                                                   },
                                                                                           FilterNumber = new FilterNumber
                                                                                                          {
                                                                                                            Operator = FilterNumberOperator.Equal,
                                                                                                            Value    = 0,
                                                                                                          },
                                                                                         },
                                                                                         new FilterField
                                                                                         {
                                                                                           Field = new PartitionField
                                                                                                   {
                                                                                                     PartitionRawField = new PartitionRawField
                                                                                                                         {
                                                                                                                           Field = PartitionRawEnumField.PodReserved,
                                                                                                                         },
                                                                                                   },
                                                                                           FilterNumber = new FilterNumber
                                                                                                          {
                                                                                                            Operator = FilterNumberOperator.Equal,
                                                                                                            Value    = 0,
                                                                                                          },
                                                                                         },
                                                                                         new FilterField
                                                                                         {
                                                                                           Field = new PartitionField
                                                                                                   {
                                                                                                     PartitionRawField = new PartitionRawField
                                                                                                                         {
                                                                                                                           Field = PartitionRawEnumField
                                                                                                                             .PreemptionPercentage,
                                                                                                                         },
                                                                                                   },
                                                                                           FilterNumber = new FilterNumber
                                                                                                          {
                                                                                                            Operator = FilterNumberOperator.Equal,
                                                                                                            Value    = 0,
                                                                                                          },
                                                                                         },
                                                                                         new FilterField
                                                                                         {
                                                                                           Field = new PartitionField
                                                                                                   {
                                                                                                     PartitionRawField = new PartitionRawField
                                                                                                                         {
                                                                                                                           Field = PartitionRawEnumField.Priority,
                                                                                                                         },
                                                                                                   },
                                                                                           FilterNumber = new FilterNumber
                                                                                                          {
                                                                                                            Operator = FilterNumberOperator.Equal,
                                                                                                            Value    = 0,
                                                                                                          },
                                                                                         },
                                                                                       },
                                                                                     },
                                                                                   },
                                                                                 },
                                                                       Sort = new ListPartitionsRequest.Types.Sort
                                                                              {
                                                                                Direction = SortDirection.Desc,
                                                                                Field = new PartitionField
                                                                                        {
                                                                                          PartitionRawField = new PartitionRawField
                                                                                                              {
                                                                                                                Field = PartitionRawEnumField.Id,
                                                                                                              },
                                                                                        },
                                                                              },
                                                                       PageSize = 10,
                                                                       Page     = 0,
                                                                     };
                                                           return await client.ListPartitionsAsync(req);
                                                         })
                                      .ConfigureAwait(false);

    logger.LogInformation("{@partitions}",
                          partitions);


    // Create a new session
    var start = Stopwatch.GetTimestamp();
    var createSessionReply = await channelPool.WithInstanceAsync(async channel =>
                                                                 {
                                                                   var client = new Submitter.SubmitterClient(channel);

                                                                   var req = new CreateSessionRequest
                                                                             {
                                                                               DefaultTaskOption = new TaskOptions
                                                                                                   {
                                                                                                     MaxDuration = Duration.FromTimeSpan(TimeSpan.FromHours(1)),
                                                                                                     MaxRetries  = 2,
                                                                                                     Priority    = 1,
                                                                                                     PartitionId = benchOptions.Partition,
                                                                                                     Options =
                                                                                                     {
                                                                                                       {
                                                                                                         "TaskDurationMs", benchOptions.TaskDurationMs.ToString()
                                                                                                       },
                                                                                                       {
                                                                                                         "TaskError", benchOptions.TaskError
                                                                                                       },
                                                                                                       {
                                                                                                         "TaskRpcException", benchOptions.TaskRpcException
                                                                                                       },
                                                                                                       {
                                                                                                         "PayloadSize", benchOptions.PayloadSize.ToString()
                                                                                                       },
                                                                                                       {
                                                                                                         "ResultSize", benchOptions.ResultSize.ToString()
                                                                                                       },
                                                                                                     },
                                                                                                   },
                                                                               PartitionIds =
                                                                               {
                                                                                 benchOptions.Partition,
                                                                               },
                                                                             };
                                                                   return await client.CreateSessionAsync(req);
                                                                 })
                                              .ConfigureAwait(false);
    var sessionCreated = Stopwatch.GetTimestamp();
    logger.LogInformation("Session Id : {sessionId}",
                          createSessionReply.SessionId);

    var cts       = new CancellationTokenSource();
    var eventTask = Task.CompletedTask;
    if (benchOptions.ShowEvents)
    {
      eventTask = Task.Factory.StartNew(async () =>
                                        {
                                          await using var channel = await channelPool.GetAsync(cts.Token)
                                                                                     .ConfigureAwait(false);
                                          var eventsClient = new Events.EventsClient(channel);

                                          using var eventsCall = eventsClient.GetEvents(new EventSubscriptionRequest
                                                                                        {
                                                                                          SessionId = createSessionReply.SessionId,
                                                                                        });

                                          while (await eventsCall.ResponseStream.MoveNext(cts.Token)
                                                                 .ConfigureAwait(false))
                                          {
                                            logger.LogInformation("{@eventUpdate}",
                                                                  eventsCall.ResponseStream.Current);
                                          }
                                        },
                                        cts.Token)
                      .Unwrap();
    }

    Console.CancelKeyPress += (_,
                               args) =>
                              {
                                args.Cancel = true;
                                using var channel = channelPool.Get();

                                var submitterClient = new Submitter.SubmitterClient(channel);
                                submitterClient.CancelSession(new Session
                                                              {
                                                                Id = createSessionReply.SessionId,
                                                              });
                                Environment.Exit(0);
                              };

    var resultChunk = await Enumerable.Range(0,
                                             benchOptions.NTasks)
                                      .Chunk(benchOptions.BatchSize)
                                      .ParallelSelect(new ParallelTaskOptions(benchOptions.DegreeOfParallelism),
                                                      async req =>
                                                      {
                                                        var rnd = new Random();
                                                        await using var channel = await channelPool.GetAsync(CancellationToken.None)
                                                                                                   .ConfigureAwait(false);

                                                        var resultClient    = new Results.ResultsClient(channel);
                                                        var submitterClient = new Submitter.SubmitterClient(channel);

                                                        var resultReq = new CreateResultsMetaDataRequest
                                                                        {
                                                                          SessionId = createSessionReply.SessionId,
                                                                          Results =
                                                                          {
                                                                            req.Select(i => new CreateResultsMetaDataRequest.Types.ResultCreate
                                                                                            {
                                                                                              Name = $"root {i}",
                                                                                            }),
                                                                          },
                                                                        };
                                                        var resultResp = await resultClient.CreateResultsMetaDataAsync(resultReq);
                                                        var resultIds = resultResp.Results.Select(raw => raw.ResultId)
                                                                                  .ToList();

                                                        var taskReq = resultIds.Select(resultId =>
                                                                                       {
                                                                                         var dataBytes = new byte[benchOptions.PayloadSize * 1024];
                                                                                         rnd.NextBytes(dataBytes);
                                                                                         return new TaskRequest
                                                                                                {
                                                                                                  ExpectedOutputKeys =
                                                                                                  {
                                                                                                    resultId,
                                                                                                  },
                                                                                                  Payload = UnsafeByteOperations.UnsafeWrap(dataBytes),
                                                                                                };
                                                                                       });
                                                        var taskResp = await submitterClient.CreateTasksAsync(createSessionReply.SessionId,
                                                                                                              null,
                                                                                                              taskReq,
                                                                                                              CancellationToken.None)
                                                                                            .ConfigureAwait(false);

                                                        if (logger.IsEnabled(LogLevel.Debug))
                                                        {
                                                          foreach (var status in taskResp.CreationStatusList.CreationStatuses)
                                                          {
                                                            logger.LogDebug("task created {taskId}",
                                                                            status.TaskInfo.TaskId);
                                                          }
                                                        }

                                                        return resultIds;
                                                      })
                                      .ToListAsync(CancellationToken.None)
                                      .ConfigureAwait(false);
    var results = resultChunk.SelectMany(x => x)
                             .ToList();

    var taskCreated = Stopwatch.GetTimestamp();

    await results.ParallelForEach(new ParallelTaskOptions(benchOptions.DegreeOfParallelism),
                                  async resultId =>
                                  {
                                    await using var channel = await channelPool.GetAsync(CancellationToken.None)
                                                                               .ConfigureAwait(false);

                                    var submitterClient = new Submitter.SubmitterClient(channel);
                                    var resultRequest = new ResultRequest
                                                        {
                                                          ResultId = resultId,
                                                          Session  = createSessionReply.SessionId,
                                                        };

#pragma warning disable CS0612 // Type or member is obsolete
                                    var availabilityReply = await submitterClient.WaitForAvailabilityAsync(resultRequest);
#pragma warning restore CS0612 // Type or member is obsolete

                                    switch (availabilityReply.TypeCase)
                                    {
                                      case AvailabilityReply.TypeOneofCase.None:
                                        throw new Exception("Issue with Server !");
                                      case AvailabilityReply.TypeOneofCase.Ok:
                                        break;
                                      case AvailabilityReply.TypeOneofCase.Error:
                                        throw new Exception($"Task in Error - {availabilityReply.Error.TaskId} : {availabilityReply.Error.Errors}");
                                      case AvailabilityReply.TypeOneofCase.NotCompletedTask:
                                        throw new Exception($"Task not completed - result id {resultId}");
                                      default:
                                        throw new ArgumentOutOfRangeException(nameof(availabilityReply.TypeCase));
                                    }
                                  })
                 .ConfigureAwait(false);

    var resultsAvailable = Stopwatch.GetTimestamp();

    var countRes = 0;

    await results.ParallelForEach(new ParallelTaskOptions(benchOptions.DegreeOfParallelism),
                                  async resultId =>
                                  {
                                    for (var i = 0; i < benchOptions.MaxRetries; i++)
                                    {
                                      await using var channel = await channelPool.GetAsync(CancellationToken.None)
                                                                                 .ConfigureAwait(false);
                                      try
                                      {
                                        var resultRequest = new ResultRequest
                                                            {
                                                              ResultId = resultId,
                                                              Session  = createSessionReply.SessionId,
                                                            };

                                        var client = new Submitter.SubmitterClient(channel);

                                        var result = await client.GetResultAsync(resultRequest,
                                                                                 CancellationToken.None)
                                                                 .ConfigureAwait(false);

                                        // A good a way to process results would be to process them individually as soon as they are
                                        // retrieved. They may be stored in a ConcurrentBag or a ConcurrentDictionary but you need to
                                        // be careful to not overload your memory. If you need to retrieve a lot of results to apply
                                        // post-processing on, consider doing so with sub-tasking so that the client-side application
                                        // has to do less work.

                                        if (result.Length != benchOptions.ResultSize * 1024)
                                        {
                                          logger.LogInformation("Received length {received}, expected length {expected}",
                                                                result.Length,
                                                                benchOptions.ResultSize * 1024);
                                          throw new InvalidOperationException("The result size from the task should have the same size as the one specified");
                                        }

                                        Interlocked.Increment(ref countRes);
                                        // If successful, return
                                        return;
                                      }
                                      catch (RpcException e) when (e.StatusCode == StatusCode.Unavailable)
                                      {
                                        logger.LogWarning(e,
                                                          "Error during result retrieving, retrying to get {resultId}",
                                                          resultId);
                                      }
                                    }

                                    // in this case, retries are all made so we need to tell that it did not work
                                    throw new InvalidOperationException("Too many retries");
                                  })
                 .ConfigureAwait(false);

    logger.LogInformation("Results retrieved {number}",
                          countRes);
    if (countRes != results.Count)
    {
      throw new InvalidOperationException("All results were not retrieved");
    }

    var resultsReceived = Stopwatch.GetTimestamp();

    var countAll = await channelPool.WithInstanceAsync(async channel =>
                                                       {
                                                         var client = new Submitter.SubmitterClient(channel);
                                                         var req = new TaskFilter
                                                                   {
                                                                     Session = new TaskFilter.Types.IdsRequest
                                                                               {
                                                                                 Ids =
                                                                                 {
                                                                                   createSessionReply.SessionId,
                                                                                 },
                                                                               },
                                                                   };
                                                         return await client.CountTasksAsync(req);
                                                       },
                                                       CancellationToken.None)
                                    .ConfigureAwait(false);

    var countFinished = Stopwatch.GetTimestamp();


    var stats = new ExecutionStats
                {
                  ElapsedTime          = TimeSpan.FromTicks((resultsReceived  - start)            / 100),
                  SubmissionTime       = TimeSpan.FromTicks((taskCreated      - sessionCreated)   / 100),
                  ResultRetrievingTime = TimeSpan.FromTicks((resultsReceived  - resultsAvailable) / 100),
                  TasksExecutionTime   = TimeSpan.FromTicks((resultsAvailable - taskCreated)      / 100),
                  CountExecutionTime   = TimeSpan.FromTicks((countFinished    - resultsReceived)  / 100),
                  TotalTasks           = countAll.Values.Sum(count => count.Count),
                  CompletedTasks = countAll.Values.Where(count => count.Status == TaskStatus.Completed)
                                           .Sum(count => count.Count),
                  ErrorTasks = countAll.Values.Where(count => count.Status == TaskStatus.Error)
                                       .Sum(count => count.Count),
                  CancelledTasks = countAll.Values.Where(count => count.Status is TaskStatus.Cancelled or TaskStatus.Cancelling)
                                           .Sum(count => count.Count),
                };
    logger.LogInformation("executions stats {@stats}",
                          stats);

    await channelPool.WithInstanceAsync(async channel => await channel.LogStatsFromSessionAsync(createSessionReply.SessionId,
                                                                                                logger)
                                                                      .ConfigureAwait(false),
                                        CancellationToken.None)
                     .ConfigureAwait(false);

    if (benchOptions.ShowEvents)
    {
      cts.CancelAfter(TimeSpan.FromSeconds(1));
      try
      {
        await eventTask.WaitAsync(CancellationToken.None)
                       .ConfigureAwait(false);
      }
      catch (RpcException e) when (e.StatusCode == StatusCode.Cancelled)
      {
        logger.LogWarning(e,
                          $"{nameof(Events.EventsClient.GetEvents)} interrupted.");
      }
    }
  }
}
