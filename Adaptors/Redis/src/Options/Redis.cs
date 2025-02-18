// This file is part of the ArmoniK project
// 
// Copyright (C) ANEO, 2021-2025. All rights reserved.
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

namespace ArmoniK.Core.Adapters.Redis.Options;

public class Redis
{
  public const string   SettingSection = nameof(Redis);
  public       string   InstanceName    { get; set; } = "";
  public       string   EndpointUrl     { get; set; } = "";
  public       string   ClientName      { get; set; } = "";
  public       string   SslHost         { get; set; } = "";
  public       int      Timeout         { get; set; }
  public       string   Password        { get; set; } = "";
  public       string   User            { get; set; } = "";
  public       bool     Ssl             { get; set; }
  public       string   CredentialsPath { get; set; } = "";
  public       string   CaPath          { get; set; } = "";
  public       int      MaxRetry        { get; set; } = 5;
  public       int      MsAfterRetry    { get; set; } = 500;
  public       TimeSpan TtlTimeSpan     { set; get; } = TimeSpan.MaxValue;
}
