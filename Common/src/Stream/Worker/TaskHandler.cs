﻿// This file is part of the ArmoniK project
// 
// Copyright (C) ANEO, 2021-2022. All rights reserved.
//   W. Kirschenmann   <wkirschenmann@aneo.fr>
//   J. Gurhem         <jgurhem@aneo.fr>
//   D. Dubuc          <ddubuc@aneo.fr>
//   L. Ziane Khodja   <lzianekhodja@aneo.fr>
//   F. Lemaitre       <flemaitre@aneo.fr>
//   S. Djebbar        <sdjebbar@aneo.fr>
//   J. Fonseca        <jfonseca@aneo.fr>
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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using ArmoniK.Api.gRPC.V1;
using ArmoniK.Api.gRPC.V1.Agent;
using ArmoniK.Api.gRPC.V1.Worker;
using ArmoniK.Core.Common.gRPC;
using ArmoniK.Core.Common.Injection.Options;
using ArmoniK.Core.Common.StateMachines;

using Google.Protobuf;

using Grpc.Core;

using Microsoft.Extensions.Logging;

namespace ArmoniK.Core.Common.Stream.Worker;


class Counter : IDisposable
{
  private readonly int[] counter_;

  public Counter(int[] counter)
  {
    counter_ = counter;
    Interlocked.Increment(ref counter_[0]);
  }


  public void Dispose()
  {
    Interlocked.Decrement(ref counter_[0]);
  }
}

class AutomaticCounter
{
  private readonly int[] counter_ =
  {
    0
  };

  public Counter GetCounter()
    => new(counter_);

  public bool IsZero
    => counter_[0] == 0;
}


public class TaskHandler : ITaskHandler
{
  private readonly CancellationToken    cancellationToken_;
  private readonly ILoggerFactory       loggerFactory_;
  private readonly ILogger<TaskHandler> logger_;

  private readonly IAsyncStreamReader<ProcessRequest> requestStream_;

  private ComputeRequestStateMachine?          crsm_;
  private IReadOnlyDictionary<string, byte[]>? dataDependencies_;
  private IList<string>?                       expectedResults_;

  private bool isInitialized_;

  private          byte[]?              payload_;
  private          string?              sessionId_;
  private          string?              taskId_;
  private          TaskOptions?         taskOptions_;
  private          Agent.AgentClient?   client_;
  private          ChannelBase?         channel_;
  private readonly AutomaticCounter     counter_ = new AutomaticCounter();
  private          GrpcChannelProvider? channelProvider_;


  private TaskHandler(IAsyncStreamReader<ProcessRequest> requestStream,
                      Configuration                      configuration,
                      CancellationToken                  cancellationToken,
                      ILoggerFactory                     loggerFactory)
  {
    requestStream_     = requestStream;
    cancellationToken_ = cancellationToken;
    loggerFactory_     = loggerFactory;
    Configuration      = configuration;
    logger_            = loggerFactory.CreateLogger<TaskHandler>();
  }

  /// <inheritdoc />
  public string SessionId
    => sessionId_ ?? throw TaskHandlerException(nameof(SessionId));

  /// <inheritdoc />
  public string TaskId
    => taskId_ ?? throw TaskHandlerException(nameof(TaskId));

  /// <inheritdoc />
  public TaskOptions TaskOptions
    => taskOptions_ ?? throw TaskHandlerException(nameof(TaskOptions));

  /// <inheritdoc />
  public byte[] Payload
    => payload_ ?? throw TaskHandlerException(nameof(Payload));

  /// <inheritdoc />
  public IReadOnlyDictionary<string, byte[]> DataDependencies
    => dataDependencies_ ?? throw TaskHandlerException(nameof(DataDependencies));

  /// <inheritdoc />
  public IList<string> ExpectedResults
    => expectedResults_ ?? throw TaskHandlerException(nameof(ExpectedResults));

  /// <inheritdoc />
  public Configuration Configuration { get; init; }

  /// <inheritdoc />
  public async Task CreateTasksAsync(IEnumerable<TaskRequest> tasks,
                                     TaskOptions?             taskOptions = null)
  {
    using var counter = counter_.GetCounter();
    var       stream  = client_!.CreateTask();

    foreach (var createLargeTaskRequest in tasks.ToRequestStream(taskOptions,
                                                                 Configuration.DataChunkMaxSize))
    {
      await stream.RequestStream.WriteAsync(createLargeTaskRequest,
                                            CancellationToken.None)
                  .ConfigureAwait(false);
    }

    var reply = await stream.ResponseAsync.ConfigureAwait(false);
    if (reply.DataCase == CreateTaskReply.DataOneofCase.NonSuccessfullIds)
    {
      throw new AggregateException(reply.NonSuccessfullIds.Ids.Select(s => new InvalidOperationException($"Could not create task it id={s}")));
    }
  }

  /// <inheritdoc />
  public Task<byte[]> RequestResource(string key)
    => throw new NotImplementedException();

  /// <inheritdoc />
  public Task<byte[]> RequestCommonData(string key)
    => throw new NotImplementedException();

  /// <inheritdoc />
  public Task<byte[]> RequestDirectData(string key)
    => throw new NotImplementedException();

  /// <inheritdoc />
  public async Task SendResult(string key,
                               byte[] data)
  {
    using var counter = counter_.GetCounter();

    var fsm = new ProcessReplyResultStateMachine(logger_);

    var stream = client_!.SendResult();

    fsm.InitKey();

    await stream.RequestStream.WriteAsync(new Result
                                          {
                                            Init = new InitKeyedDataStream
                                                   {
                                                     Key = key,
                                                   },
                                          },
                                          CancellationToken.None)
                .ConfigureAwait(false);
    var start = 0;

    while (start < data.Length)
    {
      var chunkSize = Math.Min(Configuration.DataChunkMaxSize,
                               data.Length - start);

      fsm.AddDataChunk();
      await stream.RequestStream.WriteAsync(new Result
                                            {
                                              Data = new DataChunk
                                                     {
                                                       Data = ByteString.CopyFrom(data.AsMemory()
                                                                                      .Span.Slice(start,
                                                                                                  chunkSize)),
                                                     },
                                            },
                                            CancellationToken.None)
                  .ConfigureAwait(false);

      start += chunkSize;
    }

    fsm.CompleteData();
    await stream.RequestStream.WriteAsync(new Result
                                          {
                                            Data = new DataChunk
                                                   {
                                                     DataComplete = true,
                                                   },
                                          },
                                          CancellationToken.None)
                .ConfigureAwait(false);

    fsm.CompleteRequest();
    await stream.RequestStream.WriteAsync(new Result
                                          {
                                            Init = new InitKeyedDataStream
                                                   {
                                                     LastResult = true,
                                                   },
                                          },
                                          CancellationToken.None)
                .ConfigureAwait(false);

    var reply = await stream.ResponseAsync.ConfigureAwait(false);
    if (reply.TypeCase == ResultReply.TypeOneofCase.Error)
    {
      logger_.LogError(reply.Error);
      throw new InvalidOperationException($"Cannot send result id={key}");
    }
  }

  public static async Task<TaskHandler> Create(IAsyncStreamReader<ProcessRequest> requestStream,
                                               Configuration                      configuration,
                                               ILoggerFactory                     loggerFactory,
                                               CancellationToken                  cancellationToken)
  {
    var output = new TaskHandler(requestStream,
                                 configuration,
                                 cancellationToken,
                                 loggerFactory);
    await output.Init()
                .ConfigureAwait(false);
    return output;
  }

  private async Task Init()
  {
    crsm_ = new ComputeRequestStateMachine(logger_);
    if (!await requestStream_.MoveNext()
                             .ConfigureAwait(false))
    {
      throw new InvalidOperationException("Request stream ended unexpectedly.");
    }

    if (requestStream_.Current.Compute.TypeCase != ProcessRequest.Types.ComputeRequest.TypeOneofCase.InitRequest)
    {
      throw new InvalidOperationException("Expected a Compute request type with InitRequest to start the stream.");
    }

    crsm_.InitRequest();
    var initRequest = requestStream_.Current.Compute.InitRequest;
    sessionId_          = initRequest.SessionId;
    taskId_             = initRequest.TaskId;
    taskOptions_        = initRequest.TaskOptions;
    expectedResults_    = initRequest.ExpectedOutputKeys;

    logger_.LogDebug("Trying to create channel for {address}",
                     initRequest.AgentLocation);

    channelProvider_ = new GrpcChannelProvider(new GrpcChannel
                                               {
                                                 Address    = initRequest.AgentLocation,
                                                 SocketType = GrpcSocketType.UnixSocket,
                                               },
                                               loggerFactory_.CreateLogger<GrpcChannelProvider>());
    channel_ = channelProvider_.Get();

    client_ = new Agent.AgentClient(channel_);

    if (initRequest.Payload.DataComplete)
    {
      payload_ = initRequest.Payload.Data.ToByteArray();
    }
    else
    {
      var chunks    = new List<ByteString>();
      var dataChunk = initRequest.Payload;

      chunks.Add(dataChunk.Data);

      while (!dataChunk.DataComplete)
      {
        if (!await requestStream_.MoveNext(cancellationToken_)
                                 .ConfigureAwait(false))
        {
          throw new InvalidOperationException("Request stream ended unexpectedly.");
        }

        if (requestStream_.Current.Compute.TypeCase != ProcessRequest.Types.ComputeRequest.TypeOneofCase.Payload)
        {
          throw new InvalidOperationException("Expected a Compute request type with Payload to continue the stream.");
        }

        dataChunk = requestStream_.Current.Compute.Payload;

        chunks.Add(dataChunk.Data);
        crsm_.AddPayloadChunk();
      }


      var size = chunks.Sum(s => s.Length);

      var payload = new byte[size];

      var start = 0;

      foreach (var chunk in chunks)
      {
        chunk.CopyTo(payload,
                     start);
        start += chunk.Length;
      }

      payload_ = payload;
    }

    crsm_.CompletePayload();

    var dataDependencies = new Dictionary<string, byte[]>();

    ProcessRequest.Types.ComputeRequest.Types.InitData initData;
    do
    {
      if (!await requestStream_.MoveNext(cancellationToken_)
                               .ConfigureAwait(false))
      {
        throw new InvalidOperationException("Request stream ended unexpectedly.");
      }


      if (requestStream_.Current.Compute.TypeCase != ProcessRequest.Types.ComputeRequest.TypeOneofCase.InitData)
      {
        throw new InvalidOperationException("Expected a Compute request type with InitData to continue the stream.");
      }

      initData = requestStream_.Current.Compute.InitData;
      if (!string.IsNullOrEmpty(initData.Key))
      {
        crsm_.InitDataDependency();
        var chunks = new List<ByteString>();

        while (true)
        {
          if (!await requestStream_.MoveNext(cancellationToken_)
                                   .ConfigureAwait(false))
          {
            throw new InvalidOperationException("Request stream ended unexpectedly.");
          }

          if (requestStream_.Current.Compute.TypeCase != ProcessRequest.Types.ComputeRequest.TypeOneofCase.Data)
          {
            throw new InvalidOperationException("Expected a Compute request type with Data to continue the stream.");
          }

          var dataChunk = requestStream_.Current.Compute.Data;

          if (dataChunk.TypeCase == DataChunk.TypeOneofCase.Data)
          {
            chunks.Add(dataChunk.Data);
            crsm_.AddDataDependencyChunk();
          }

          if (dataChunk.TypeCase == DataChunk.TypeOneofCase.None)
          {
            throw new InvalidOperationException("Expected a Compute request type with a DataChunk Payload to continue the stream.");
          }

          if (dataChunk.TypeCase == DataChunk.TypeOneofCase.DataComplete)
          {
            break;
          }
        }

        var size = chunks.Sum(s => s.Length);

        var data = new byte[size];

        var start = 0;

        foreach (var chunk in chunks)
        {
          chunk.CopyTo(data,
                       start);
          start += chunk.Length;
        }

        dataDependencies[initData.Key] = data;
        crsm_.CompleteDataDependency();
      }
    } while (!string.IsNullOrEmpty(initData.Key));

    crsm_.CompleteRequest();
    dataDependencies_ = dataDependencies;
    isInitialized_    = true;
  }

  private Exception TaskHandlerException(string argumentName)
    => isInitialized_
         ? new InvalidOperationException($"Error in initalization: {argumentName} is null")
         : new InvalidOperationException("");

  public async ValueTask DisposeAsync()
  {
    if (!counter_.IsZero)
    {
      logger_.LogWarning("At least one request to the agent is running");
    }
    await (channel_?.ShutdownAsync()!).ConfigureAwait(false);
    channelProvider_?.Dispose();
  }
}
