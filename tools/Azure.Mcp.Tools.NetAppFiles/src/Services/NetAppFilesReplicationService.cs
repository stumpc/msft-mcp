// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure;
using Azure.Core;
using Azure.Mcp.Core.Services.Azure;
using Azure.Mcp.Tools.NetAppFiles.Models;
using Azure.ResourceManager.NetApp;
using Azure.ResourceManager.NetApp.Models;

namespace Azure.Mcp.Tools.NetAppFiles.Services;

public class NetAppFilesReplicationService(IAzureService azureService)
    : BaseAzureService(azureService), INetAppFilesReplicationService
{
    public async Task ApproveAsync(
        string account,
        string pool,
        string volume,
        string remoteVolumeResourceId,
        string resourceGroup,
        string subscription,
        string? tenant = null,
        CancellationToken cancellationToken = default)
    {
        var volumeResource = await GetVolumeResourceAsync(
            account,
            pool,
            volume,
            resourceGroup,
            subscription,
            tenant,
            cancellationToken);
        var content = new NetAppVolumeAuthorizeReplicationContent
        {
            RemoteVolumeResourceId = new ResourceIdentifier(remoteVolumeResourceId)
        };

        var operation = await volumeResource.AuthorizeReplicationAsync(
            WaitUntil.Started,
            content,
            cancellationToken);

        await WaitForLroCompletionAsync(operation, cancellationToken);
    }

    public async Task<NetAppFilesReplicationStatus> GetStatusAsync(
        string account,
        string pool,
        string volume,
        string resourceGroup,
        string subscription,
        string? tenant = null,
        CancellationToken cancellationToken = default)
    {
        var volumeResource = await GetVolumeResourceAsync(
            account,
            pool,
            volume,
            resourceGroup,
            subscription,
            tenant,
            cancellationToken);
        var status = (await volumeResource.GetReplicationStatusAsync(cancellationToken)).Value;

        return new NetAppFilesReplicationStatus(
            status.IsHealthy,
            status.RelationshipStatus?.ToString(),
            status.MirrorState?.ToString(),
            status.TotalProgress,
            status.ErrorMessage);
    }

    public async Task ResumeAsync(
        string account,
        string pool,
        string volume,
        string resourceGroup,
        string subscription,
        string? tenant = null,
        CancellationToken cancellationToken = default)
    {
        var volumeResource = await GetVolumeResourceAsync(
            account,
            pool,
            volume,
            resourceGroup,
            subscription,
            tenant,
            cancellationToken);

        var operation = await volumeResource.ResyncReplicationAsync(
            WaitUntil.Started,
            cancellationToken);

        await WaitForLroCompletionAsync(operation, cancellationToken);
    }

    public async Task SuspendAsync(
        string account,
        string pool,
        string volume,
        bool forceBreakReplication,
        string resourceGroup,
        string subscription,
        string? tenant = null,
        CancellationToken cancellationToken = default)
    {
        var volumeResource = await GetVolumeResourceAsync(
            account,
            pool,
            volume,
            resourceGroup,
            subscription,
            tenant,
            cancellationToken);
        var content = new NetAppVolumeBreakReplicationContent
        {
            ForceBreakReplication = forceBreakReplication
        };

        var operation = await volumeResource.BreakReplicationAsync(
            WaitUntil.Started,
            content,
            cancellationToken);

        await WaitForLroCompletionAsync(operation, cancellationToken);
    }

    private async Task<NetAppVolumeResource> GetVolumeResourceAsync(
        string account,
        string pool,
        string volume,
        string resourceGroup,
        string subscription,
        string? tenant,
        CancellationToken cancellationToken)
    {
        var resourceGroupResource = await AzureService.GetResourceGroupResource(
            subscription,
            resourceGroup,
            tenant,
            cancellationToken: cancellationToken)
            ?? throw new KeyNotFoundException($"Resource group '{resourceGroup}' was not found.");

        var accountResource = resourceGroupResource.GetNetAppAccount(account, cancellationToken).Value;
        var poolResource = accountResource.GetCapacityPool(pool, cancellationToken).Value;
        return poolResource.GetNetAppVolume(volume, cancellationToken).Value;
    }
}
