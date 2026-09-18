// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Mcp.Tools.NetAppFiles.Models;

namespace Azure.Mcp.Tools.NetAppFiles.Services;

public interface INetAppFilesReplicationService
{
    Task ApproveAsync(
        string account,
        string pool,
        string volume,
        string remoteVolumeResourceId,
        string resourceGroup,
        string subscription,
        string? tenant = null,
        CancellationToken cancellationToken = default);

    Task<NetAppFilesReplicationStatus> GetStatusAsync(
        string account,
        string pool,
        string volume,
        string resourceGroup,
        string subscription,
        string? tenant = null,
        CancellationToken cancellationToken = default);

    Task ResumeAsync(
        string account,
        string pool,
        string volume,
        string resourceGroup,
        string subscription,
        string? tenant = null,
        CancellationToken cancellationToken = default);

    Task SuspendAsync(
        string account,
        string pool,
        string volume,
        bool forceBreakReplication,
        string resourceGroup,
        string subscription,
        string? tenant = null,
        CancellationToken cancellationToken = default);
}
