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
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using ArmoniK.Core.Adapters.QueueCommon;
using ArmoniK.Core.Base;
using ArmoniK.Core.Base.DataStructures;

using Microsoft.Extensions.Logging;

using RabbitMQ.Client;

namespace ArmoniK.Core.Adapters.RabbitMQ;

public class PushQueueStorage : QueueStorage, IPushQueueStorage
{
  private readonly ILogger<PushQueueStorage> logger_;

  public PushQueueStorage(Amqp                      options,
                          IConnectionRabbit         connectionRabbit,
                          ILogger<PushQueueStorage> logger)
    : base(options,
           connectionRabbit)
    => logger_ = logger;

  /// <inheritdoc />
  public async Task PushMessagesAsync(IEnumerable<MessageData> messages,
                                      string                   partitionId,
                                      CancellationToken        cancellationToken = default)
  {
    var priorityGroups = messages.GroupBy(msgData => msgData.Options.Priority);
    await Task.WhenAll(priorityGroups.Select(group => PushMessagesAsync(group,
                                                                        partitionId,
                                                                        group.Key,
                                                                        cancellationToken)))
              .ConfigureAwait(false);
  }

  private Task PushMessagesAsync(IEnumerable<MessageData> messages,
                                 string                   partitionId,
                                 int                      priority          = 1,
                                 CancellationToken        cancellationToken = default)
  {
    var task = Task.Run(() => PushMessages(messages,
                                           partitionId,
                                           priority),
                        cancellationToken);
    return task;
  }

  private void PushMessages(IEnumerable<MessageData> messages,
                            string                   partitionId,
                            int                      priority)
  {
    if (!IsInitialized)
    {
      throw new InvalidOperationException($"{nameof(PushQueueStorage)} should be initialized before calling this method.");
    }

    ConnectionRabbit.Channel!.ExchangeDeclare("ArmoniK.QueueExchange",
                                              "direct");


    foreach (var msg in messages)
    {
      var basicProperties = ConnectionRabbit.Channel!.CreateBasicProperties();
      basicProperties.Priority = Convert.ToByte(priority);
      basicProperties.MessageId = Guid.NewGuid()
                                      .ToString();
      ConnectionRabbit.Channel.BasicPublish("ArmoniK.QueueExchange",
                                            partitionId,
                                            basicProperties,
                                            Encoding.UTF8.GetBytes(msg.TaskId));
    }
  }
}
