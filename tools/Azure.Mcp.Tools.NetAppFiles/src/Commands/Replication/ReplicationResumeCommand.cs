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
    Id = "2490232e-e8de-4368-95c4-a67899583c6b",
    Name = "resume",
    Title = "Resume Azure NetApp Files Replication",
    Description = "Resumes a suspended replication connection on a destination Azure NetApp Files volume. Requires the destination account, capacity pool, volume, resource group, and subscription. Returns whether replication was resumed.",
    OperationPlane = ToolOperationPlane.Control,
    Destructive = true,
    Idempotent = true,
    OpenWorld = false,
    ReadOnly = false,
    Secret = false,
    LocalRequired = false)]
public sealed class ReplicationResumeCommand(
    ILogger<ReplicationResumeCommand> logger,
    INetAppFilesReplicationService service,
    ISubscriptionResolver subscriptionResolver)
    : SubscriptionCommand<ReplicationResumeOptions, ReplicationResumeCommand.ReplicationResumeResult>(subscriptionResolver)
{
    private readonly ILogger<ReplicationResumeCommand> _logger = logger;
    private readonly INetAppFilesReplicationService _service = service;

    public override async Task<CommandResponse> ExecuteAsync(
        CommandContext context,
        ReplicationResumeOptions options,
        CancellationToken cancellationToken)
    {
        try
        {
            await _service.ResumeAsync(
                options.Account,
                options.Pool,
                options.Volume,
                options.ResourceGroup,
                options.Subscription!,
                options.Tenant,
                cancellationToken);

            context.Response.Results = ResponseResult.Create(
                new ReplicationResumeResult(true),
                NetAppFilesJsonContext.Default.ReplicationResumeResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error resuming Azure NetApp Files replication. Account: {Account}, Pool: {Pool}, Volume: {Volume}, ResourceGroup: {ResourceGroup}, Subscription: {Subscription}",
                options.Account,
                options.Pool,
                options.Volume,
                options.ResourceGroup,
                options.Subscription);
            HandleException(context, ex);
        }

        return context.Response;
    }

    public override void ValidateOptions(ReplicationResumeOptions options, ValidationResult validationResult)
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
            "Replication could not be resumed. Verify that the destination volume has a suspended replication relationship.",
        RequestFailedException requestFailedException when requestFailedException.Status == (int)HttpStatusCode.Conflict =>
            "Replication could not be resumed because of a resource conflict. Verify the replication state of the destination volume.",
        RequestFailedException requestFailedException when requestFailedException.Status == (int)HttpStatusCode.Forbidden =>
            "Authorization failed resuming Azure NetApp Files replication. Verify that you have the required RBAC permissions.",
        RequestFailedException requestFailedException when requestFailedException.Status == (int)HttpStatusCode.NotFound =>
            "The destination Azure NetApp Files volume or replication relationship was not found. Verify the account, capacity pool, volume, and resource group names.",
        _ => base.GetErrorMessage(ex)
    };

    public record ReplicationResumeResult(bool Resumed);
}
