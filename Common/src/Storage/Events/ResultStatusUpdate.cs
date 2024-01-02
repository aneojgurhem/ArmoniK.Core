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

namespace ArmoniK.Core.Common.Storage.Events;

/// <summary>
///   Represents an status update for a result
/// </summary>
/// <param name="SessionId">The id of the session</param>
/// <param name="ResultId">The id of the result</param>
/// <param name="Status">The new status of the result</param>
public record ResultStatusUpdate(string       SessionId,
                                 string       ResultId,
                                 ResultStatus Status);
