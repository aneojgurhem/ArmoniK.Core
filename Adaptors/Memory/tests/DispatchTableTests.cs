// This file is part of the ArmoniK project
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

using System.Collections.Concurrent;

using ArmoniK.Core.Common.Storage;
using ArmoniK.Core.Common.Tests;

using Microsoft.Extensions.DependencyInjection;

using NUnit.Framework;

namespace ArmoniK.Core.Adapters.Memory.Tests;

[TestFixture]
public class DispatchTableTests : DispatchTableTestBase
{
  public override void GetDispatchTableInstance()
  {
    var services = new ServiceCollection();

    services.AddTransient<IDispatchTable, DispatchTable>();
    services.AddTransient<ConcurrentDictionary<string, Dispatch>>();
    services.AddLogging();

    var provider = services.BuildServiceProvider(true);
    var scope    = provider.CreateScope();

    DispatchTable = scope.ServiceProvider.GetRequiredService<IDispatchTable>();

    /* Put a dispatch in the Table with a second delay w.r.t the current time,
     * that the given sessionId and taskId match at least one of the inserted
     * Dispatches in the SetUp of DispatchTableTestBase matters*/
    ((DispatchTable)DispatchTable).InitDispatchForTesting("SessionId",
                                                          "TaskId",
                                                          "InitDispatchId",
                                                          1);
    RunTests = true;
  }
}
