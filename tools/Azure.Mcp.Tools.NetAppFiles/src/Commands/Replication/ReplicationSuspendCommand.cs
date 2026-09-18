// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using Azure;
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
    Id = "673faef1-f743-4cb3-b36b-19104a825c99",
    Name = "suspend",
    Title = "Suspend Azure NetApp Files Replication",
    Description = "Suspends the replication connection on a destination Azure NetApp Files volume. Requires the destination account, capacity pool, volume, resource group, and subscription. Optionally forces suspension while replication is transferring data. Returns whether replication was suspended.",
    OperationPlane = ToolOperationPlane.Control,
    Destructive = true,
    Idempotent = true,
    OpenWorld = false,
    ReadOnly = false,
    Secret = false,
    LocalRequired = false)]
public sealed class ReplicationSuspendCommand(
    ILogger<ReplicationSuspendCommand> logger,
    INetAppFilesReplicationService service,
    ISubscriptionResolver subscriptionResolver)
    : SubscriptionCommand<ReplicationSuspendOptions, ReplicationSuspendCommand.ReplicationSuspendResult>(subscriptionResolver)
{
    private readonly ILogger<ReplicationSuspendCommand> _logger = logger;
    private readonly INetAppFilesReplicationService _service = service;

    public override async Task<CommandResponse> ExecuteAsync(
        CommandContext context,
        ReplicationSuspendOptions options,
        CancellationToken cancellationToken)
    {
        try
        {
            await _service.SuspendAsync(
                options.Account,
                options.Pool,
                options.Volume,
                options.ForceBreakReplication,
                options.ResourceGroup,
                options.Subscription!,
                options.Tenant,
                cancellationToken);

            context.Response.Results = ResponseResult.Create(
                new ReplicationSuspendResult(true),
                NetAppFilesJsonContext.Default.ReplicationSuspendResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error suspending Azure NetApp Files replication. Account: {Account}, Pool: {Pool}, Volume: {Volume}, ResourceGroup: {ResourceGroup}, Subscription: {Subscription}",
                options.Account,
                options.Pool,
                options.Volume,
                options.ResourceGroup,
                options.Subscription);
            HandleException(context, ex);
        }

        return context.Response;
    }

    public override void ValidateOptions(ReplicationSuspendOptions options, ValidationResult validationResult)
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
        RequestFailedException requestFailedException when requestFailedException.Status == (int)HttpStatusCode.BadRequest =>
            "Replication could not be suspended. Verify that the destination volume has an active replication relationship, or use --force-break-replication if data is currently transferring.",
        RequestFailedException requestFailedException when requestFailedException.Status == (int)HttpStatusCode.Conflict =>
            "Replication could not be suspended because of a resource conflict. Verify the replication state of the destination volume.",
        RequestFailedException requestFailedException when requestFailedException.Status == (int)HttpStatusCode.Forbidden =>
            "Authorization failed suspending Azure NetApp Files replication. Verify that you have the required RBAC permissions.",
        RequestFailedException requestFailedException when requestFailedException.Status == (int)HttpStatusCode.NotFound =>
            "The destination Azure NetApp Files volume or replication relationship was not found. Verify the account, capacity pool, volume, and resource group names.",
        _ => base.GetErrorMessage(ex)
    };

    public record ReplicationSuspendResult(bool Suspended);
}
