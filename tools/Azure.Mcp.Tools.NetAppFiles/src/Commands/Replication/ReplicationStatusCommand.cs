// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using Azure.Mcp.Core.Commands.Subscription;
using Azure.Mcp.Core.Services.Azure.Subscription;
using Azure.Mcp.Tools.NetAppFiles.Models;
using Azure.Mcp.Tools.NetAppFiles.Options.Replication;
using Azure.Mcp.Tools.NetAppFiles.Services;
using Azure.Mcp.Tools.NetAppFiles.Validation;
using Microsoft.Extensions.Logging;
using Microsoft.Mcp.Core.Commands;
using Microsoft.Mcp.Core.Models.Command;
using Microsoft.Mcp.Core.Options;

namespace Azure.Mcp.Tools.NetAppFiles.Commands.Replication;

[CommandMetadata(
    Id = "2126f6f5-7896-4891-bc49-bc91c8a8319b",
    Name = "status",
    Title = "Get Azure NetApp Files Replication Status",
    Description = "Gets the replication status for an Azure NetApp Files volume. Requires the account, capacity pool, volume, resource group, and subscription. Returns replication health, relationship status, mirror state, progress, and any replication error message.",
    OperationPlane = ToolOperationPlane.Control,
    Destructive = false,
    Idempotent = true,
    OpenWorld = false,
    ReadOnly = true,
    Secret = false,
    LocalRequired = false)]
public sealed class ReplicationStatusCommand(
    ILogger<ReplicationStatusCommand> logger,
    INetAppFilesReplicationService service,
    ISubscriptionResolver subscriptionResolver)
    : SubscriptionCommand<ReplicationStatusOptions, ReplicationStatusCommand.ReplicationStatusResult>(subscriptionResolver)
{
    private readonly ILogger<ReplicationStatusCommand> _logger = logger;
    private readonly INetAppFilesReplicationService _service = service;

    public override async Task<CommandResponse> ExecuteAsync(
        CommandContext context,
        ReplicationStatusOptions options,
        CancellationToken cancellationToken)
    {
        try
        {
            var status = await _service.GetStatusAsync(
                options.Account,
                options.Pool,
                options.Volume,
                options.ResourceGroup,
                options.Subscription!,
                options.Tenant,
                cancellationToken);

            context.Response.Results = ResponseResult.Create(
                new ReplicationStatusResult(status),
                NetAppFilesJsonContext.Default.ReplicationStatusResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error getting Azure NetApp Files replication status. Account: {Account}, Pool: {Pool}, Volume: {Volume}, ResourceGroup: {ResourceGroup}, Subscription: {Subscription}",
                options.Account,
                options.Pool,
                options.Volume,
                options.ResourceGroup,
                options.Subscription);
            HandleException(context, ex);
        }

        return context.Response;
    }

    public override void ValidateOptions(ReplicationStatusOptions options, ValidationResult validationResult)
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
    }

    protected override string GetErrorMessage(Exception ex) => ex switch
    {
        KeyNotFoundException => "The resource group was not found. Verify it exists and you have access.",
        Azure.RequestFailedException requestFailedException when requestFailedException.Status == (int)HttpStatusCode.Forbidden =>
            "Authorization failed getting the Azure NetApp Files replication status. Verify that you have the required RBAC permissions.",
        Azure.RequestFailedException requestFailedException when requestFailedException.Status == (int)HttpStatusCode.NotFound =>
            "The Azure NetApp Files volume or replication relationship was not found. Verify the account, capacity pool, volume, and resource group names.",
        _ => base.GetErrorMessage(ex)
    };

    public record ReplicationStatusResult(NetAppFilesReplicationStatus ReplicationStatus);
}
