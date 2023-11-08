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

using System.Threading.Tasks;

using ArmoniK.Api.gRPC.V1.Agent;
using ArmoniK.Core.Common.Auth.Authorization;

using Grpc.Core;

namespace ArmoniK.Core.Common.gRPC.Services;

[IgnoreAuthentication]
public class GrpcAgentService : Api.gRPC.V1.Agent.Agent.AgentBase
{
  private IAgent? agent_;

  public Task Start(IAgent agent)
  {
    agent_ = agent;
    return Task.CompletedTask;
  }

  public Task Stop()
  {
    agent_ = null;
    return Task.CompletedTask;
  }

  public override async Task<CreateTaskReply> CreateTask(IAsyncStreamReader<CreateTaskRequest> requestStream,
                                                         ServerCallContext                     context)
  {
    if (agent_ != null)
    {
      return await agent_.CreateTask(requestStream,
                                     context.CancellationToken)
                         .ConfigureAwait(false);
    }

    return new CreateTaskReply
           {
             Error = "No task is accepting request",
           };
  }

  public override async Task<DataResponse> GetCommonData(DataRequest       request,
                                                         ServerCallContext context)
  {
    if (agent_ != null)
    {
      return await agent_.GetCommonData(request,
                                        context.CancellationToken)
                         .ConfigureAwait(false);
    }

    throw new RpcException(new Status(StatusCode.Unavailable,
                                      "No task is accepting request"),
                           "No task is accepting request");
  }

  public override async Task<DataResponse> GetResourceData(DataRequest       request,
                                                           ServerCallContext context)
  {
    if (agent_ != null)
    {
      return await agent_.GetResourceData(request,
                                          context.CancellationToken)
                         .ConfigureAwait(false);
    }

    throw new RpcException(new Status(StatusCode.Unavailable,
                                      "No task is accepting request"),
                           "No task is accepting request");
  }

  public override async Task<DataResponse> GetDirectData(DataRequest       request,
                                                         ServerCallContext context)
  {
    if (agent_ != null)
    {
      return await agent_.GetDirectData(request,
                                        context.CancellationToken)
                         .ConfigureAwait(false);
    }

    throw new RpcException(new Status(StatusCode.Unavailable,
                                      "No task is accepting request"),
                           "No task is accepting request");
  }

  public override async Task<NotifyResultDataResponse> NotifyResultData(NotifyResultDataRequest request,
                                                                        ServerCallContext       context)
  {
    if (agent_ != null)
    {
      return await agent_.NotifyResultData(request,
                                           context.CancellationToken)
                         .ConfigureAwait(false);
    }

    throw new RpcException(new Status(StatusCode.Unavailable,
                                      "No task is accepting request"),
                           "No task is accepting request");
  }

  public override async Task<CreateResultsMetaDataResponse> CreateResultsMetaData(CreateResultsMetaDataRequest request,
                                                                                  ServerCallContext            context)
  {
    if (agent_ != null)
    {
      return await agent_.CreateResultsMetaData(request,
                                                context.CancellationToken)
                         .ConfigureAwait(false);
    }

    throw new RpcException(new Status(StatusCode.Unavailable,
                                      "No task is accepting request"),
                           "No task is accepting request");
  }

  public override async Task<SubmitTasksResponse> SubmitTasks(SubmitTasksRequest request,
                                                              ServerCallContext  context)
  {
    if (agent_ != null)
    {
      return await agent_.SubmitTasks(request,
                                      context.CancellationToken)
                         .ConfigureAwait(false);
    }

    throw new RpcException(new Status(StatusCode.Unavailable,
                                      "No task is accepting request"),
                           "No task is accepting request");
  }

  public override async Task<CreateResultsResponse> CreateResults(CreateResultsRequest request,
                                                                  ServerCallContext    context)
  {
    if (agent_ != null)
    {
      return await agent_.CreateResults(request,
                                        context.CancellationToken)
                         .ConfigureAwait(false);
    }

    throw new RpcException(new Status(StatusCode.Unavailable,
                                      "No task is accepting request"),
                           "No task is accepting request");
  }
}
