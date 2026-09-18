// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using Azure;
using Azure.Core;
using Azure.Mcp.Core.Commands.Subscription;
using Azure.Mcp.Core.Services.Azure.Subscription;
using Azure.Mcp.Tools.NetAppFiles.Options.Replication;
using Azure.Mcp.Tools.NetAppFiles.Services;
using Azure.Mcp.Tools.NetAppFiles.Validation;
using Microsoft.Extensions.Logging;
using Microsoft.Mcp.Core.Commands;
using Microsoft.Mcp.Core.Models.Command;
using Microsoft.Mcp.Core.Options;

namespace Azure.Mcp.Tools.NetAppFiles.Commands.Replication;

[CommandMetadata(
    Id = "fd841f42-5c81-49cd-84e7-5b6c6d18a7cb",
    Name = "approve",
    Title = "Approve Azure NetApp Files Replication",
    Description = "Approves a replication connection on a source Azure NetApp Files volume. Requires the source account, capacity pool, volume, remote volume resource ID, resource group, and subscription. Returns whether the replication connection was approved.",
    OperationPlane = ToolOperationPlane.Control,
    Destructive = true,
    Idempotent = true,
    OpenWorld = false,
    ReadOnly = false,
    Secret = false,
    LocalRequired = false)]
public sealed class ReplicationApproveCommand(
    ILogger<ReplicationApproveCommand> logger,
    INetAppFilesReplicationService service,
    ISubscriptionResolver subscriptionResolver)
    : SubscriptionCommand<ReplicationApproveOptions, ReplicationApproveCommand.ReplicationApproveResult>(subscriptionResolver)
{
    private const string NetAppVolumeResourceType = "Microsoft.NetApp/netAppAccounts/capacityPools/volumes";

    private readonly ILogger<ReplicationApproveCommand> _logger = logger;
    private readonly INetAppFilesReplicationService _service = service;

    public override async Task<CommandResponse> ExecuteAsync(
        CommandContext context,
        ReplicationApproveOptions options,
        CancellationToken cancellationToken)
    {
        try
        {
            await _service.ApproveAsync(
                options.Account,
                options.Pool,
                options.Volume,
                options.RemoteVolumeResourceId,
                options.ResourceGroup,
                options.Subscription!,
                options.Tenant,
                cancellationToken);

            context.Response.Results = ResponseResult.Create(
                new ReplicationApproveResult(true),
                NetAppFilesJsonContext.Default.ReplicationApproveResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error approving Azure NetApp Files replication. Account: {Account}, Pool: {Pool}, Volume: {Volume}, ResourceGroup: {ResourceGroup}, Subscription: {Subscription}",
                options.Account,
                options.Pool,
                options.Volume,
                options.ResourceGroup,
                options.Subscription);
            HandleException(context, ex);
        }

        return context.Response;
    }

    public override void ValidateOptions(ReplicationApproveOptions options, ValidationResult validationResult)
    {
        base.ValidateOptions(options, validationResult);

        if (!AccountNameValidator.IsValid(options.Account))
        {
            validationResult.Errors.Add(AccountNameValidator.ErrorMessage);
        }

        if (!NetAppFilesChildResourceNameValidator.IsValid(options.Pool))
        {
            validationResult.Errors.Add("--pool must be 1-64 characters, start and end with an alphanumeric character, and contain only alphanumerics, underscores, and hyphens.");
        }

        if (!NetAppFilesChildResourceNameValidator.IsValid(options.Volume))
        {
            validationResult.Errors.Add("--volume must be 1-64 characters, start and end with an alphanumeric character, and contain only alphanumerics, underscores, and hyphens.");
        }

        if (!ResourceIdentifier.TryParse(options.RemoteVolumeResourceId, out var remoteVolumeId) ||
            remoteVolumeId is null ||
            !string.Equals(remoteVolumeId.ResourceType.ToString(), NetAppVolumeResourceType, StringComparison.OrdinalIgnoreCase))
        {
            validationResult.Errors.Add("--remote-volume-resource-id must be a valid Azure NetApp Files volume resource ID.");
        }
    }

    protected override string GetErrorMessage(Exception ex) => ex switch
    {
        KeyNotFoundException => "The resource group was not found. Verify it exists and you have access.",
        RequestFailedException requestFailedException when requestFailedException.Status == (int)HttpStatusCode.BadRequest =>
            "The replication connection could not be approved. Verify the source volume and remote volume resource ID are configured for replication.",
        RequestFailedException requestFailedException when requestFailedException.Status == (int)HttpStatusCode.Conflict =>
            "The replication connection could not be approved because of a resource conflict. Verify the replication state of both volumes.",
        RequestFailedException requestFailedException when requestFailedException.Status == (int)HttpStatusCode.Forbidden =>
            "Authorization failed approving Azure NetApp Files replication. Verify that you have the required RBAC permissions.",
        RequestFailedException requestFailedException when requestFailedException.Status == (int)HttpStatusCode.NotFound =>
            "The source Azure NetApp Files volume or remote volume was not found. Verify the account, capacity pool, volume, resource group, and remote volume resource ID.",
        _ => base.GetErrorMessage(ex)
    };

    public record ReplicationApproveResult(bool Approved);
}
