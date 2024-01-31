// This file is part of the ArmoniK project
// 
// Copyright (C) ANEO, 2021-2024. All rights reserved.
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
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

using ArmoniK.Api.Common.Utils;
using ArmoniK.Utils;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ArmoniK.Core.Common.Pollster;

public class RunningTaskProcessor : BackgroundService
{
  private readonly ILogger<RunningTaskProcessor> logger_;
  private readonly PostProcessingTaskQueue       postProcessingTaskQueue_;
  private readonly RunningTaskQueue              runningTaskQueue_;

  public RunningTaskProcessor(RunningTaskQueue              runningTaskQueue,
                              PostProcessingTaskQueue       postProcessingTaskQueue,
                              ILogger<RunningTaskProcessor> logger)
  {
    runningTaskQueue_        = runningTaskQueue;
    postProcessingTaskQueue_ = postProcessingTaskQueue;
    logger_                  = logger;
  }

  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    logger_.LogDebug("Start running task processing service");
    while (!stoppingToken.IsCancellationRequested)
    {
      try
      {
        while (postProcessingTaskQueue_.RemoveException(out var exception))
        {
          runningTaskQueue_.AddException(exception);
        }

        var taskHandler = await runningTaskQueue_.ReadAsync(stoppingToken)
                                                 .ConfigureAwait(false);
        await using var taskHandlerDispose = new Deferrer(taskHandler);

        var taskInfo = taskHandler.GetAcquiredTaskInfo();

        using var _ = logger_.BeginPropertyScope(("messageHandler", taskInfo.MessageId),
                                                 ("taskId", taskInfo.TaskId),
                                                 ("sessionId", taskInfo.SessionId));
        await taskHandler.ExecuteTask()
                         .ConfigureAwait(false);
        await postProcessingTaskQueue_.WriteAsync(taskHandler,
                                                  stoppingToken)
                                      .ConfigureAwait(false);

        taskHandlerDispose.Reset();
      }
      catch (Exception e)
      {
        logger_.LogError(e,
                         "Error while executing task");
        runningTaskQueue_.AddException(ExceptionDispatchInfo.Capture(e)
                                                            .SourceException);
      }
    }

    logger_.LogWarning("End of running task processor; no more tasks will be executed");
  }
}
