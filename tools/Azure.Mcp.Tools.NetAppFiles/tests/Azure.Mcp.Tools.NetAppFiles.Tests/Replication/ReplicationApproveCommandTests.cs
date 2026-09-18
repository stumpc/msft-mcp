// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using Azure;
using Azure.Mcp.Tests.Commands;
using Azure.Mcp.Tools.NetAppFiles.Commands;
using Azure.Mcp.Tools.NetAppFiles.Commands.Replication;
using Azure.Mcp.Tools.NetAppFiles.Services;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace Azure.Mcp.Tools.NetAppFiles.Tests.Replication;

public class ReplicationApproveCommandTests
    : SubscriptionCommandUnitTestsBase<ReplicationApproveCommand, INetAppFilesReplicationService>
{
    private const string RemoteVolumeResourceId =
        "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/remote-rg/providers/Microsoft.NetApp/netAppAccounts/remote-account/capacityPools/remote-pool/volumes/remote-volume";

    [Fact]
    public void Constructor_InitializesCommandCorrectly()
    {
        var command = Command.GetCommand();

        Assert.Equal("approve", command.Name);
        Assert.False(string.IsNullOrWhiteSpace(command.Description));
    }

    [Fact]
    public async Task ExecuteAsync_ApprovesReplication()
    {
        var response = await ExecuteCommandAsync(
            "--account", "account1",
            "--pool", "pool1",
            "--volume", "volume1",
            "--remote-volume-resource-id", RemoteVolumeResourceId,
            "--resource-group", "rg",
            "--subscription", "sub",
            "--tenant", "tenant");

        var result = ValidateAndDeserializeResponse(
            response,
            NetAppFilesJsonContext.Default.ReplicationApproveResult);
        Assert.True(result.Approved);
        await Service.Received(1).ApproveAsync(
            "account1",
            "pool1",
            "volume1",
            RemoteVolumeResourceId,
            "rg",
            "sub",
            "tenant",
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("--account account/name --pool pool1 --volume volume1 --remote-volume-resource-id /subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/remote-rg/providers/Microsoft.NetApp/netAppAccounts/remote-account/capacityPools/remote-pool/volumes/remote-volume --resource-group rg --subscription sub", "--account")]
    [InlineData("--account account1 --pool pool/name --volume volume1 --remote-volume-resource-id /subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/remote-rg/providers/Microsoft.NetApp/netAppAccounts/remote-account/capacityPools/remote-pool/volumes/remote-volume --resource-group rg --subscription sub", "--pool")]
    [InlineData("--account account1 --pool pool1 --volume volume/name --remote-volume-resource-id /subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/remote-rg/providers/Microsoft.NetApp/netAppAccounts/remote-account/capacityPools/remote-pool/volumes/remote-volume --resource-group rg --subscription sub", "--volume")]
    [InlineData("--account account1 --pool pool1 --volume volume1 --remote-volume-resource-id invalid --resource-group rg --subscription sub", "--remote-volume-resource-id")]
    [InlineData("--account account1 --pool pool1 --volume volume1 --remote-volume-resource-id /subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/remote-rg/providers/Microsoft.Storage/storageAccounts/account --resource-group rg --subscription sub", "--remote-volume-resource-id")]
    public async Task ExecuteAsync_InvalidOption_ReturnsBadRequest(string args, string expectedMessage)
    {
        var response = await ExecuteCommandAsync(args);

        Assert.Equal(HttpStatusCode.BadRequest, response.Status);
        Assert.Contains(expectedMessage, response.Message);
        Assert.Empty(Service.ReceivedCalls());
    }

    [Theory]
    [InlineData("--pool pool1 --volume volume1 --remote-volume-resource-id /subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/remote-rg/providers/Microsoft.NetApp/netAppAccounts/remote-account/capacityPools/remote-pool/volumes/remote-volume --resource-group rg --subscription sub")]
    [InlineData("--account account1 --volume volume1 --remote-volume-resource-id /subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/remote-rg/providers/Microsoft.NetApp/netAppAccounts/remote-account/capacityPools/remote-pool/volumes/remote-volume --resource-group rg --subscription sub")]
    [InlineData("--account account1 --pool pool1 --remote-volume-resource-id /subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/remote-rg/providers/Microsoft.NetApp/netAppAccounts/remote-account/capacityPools/remote-pool/volumes/remote-volume --resource-group rg --subscription sub")]
    [InlineData("--account account1 --pool pool1 --volume volume1 --resource-group rg --subscription sub")]
    [InlineData("--account account1 --pool pool1 --volume volume1 --remote-volume-resource-id /subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/remote-rg/providers/Microsoft.NetApp/netAppAccounts/remote-account/capacityPools/remote-pool/volumes/remote-volume --subscription sub")]
    [InlineData("--account account1 --pool pool1 --volume volume1 --remote-volume-resource-id /subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/remote-rg/providers/Microsoft.NetApp/netAppAccounts/remote-account/capacityPools/remote-pool/volumes/remote-volume --resource-group rg")]
    public async Task ExecuteAsync_MissingRequiredOption_ReturnsBadRequest(string args)
    {
        var response = await ExecuteCommandAsync(args);

        Assert.Equal(HttpStatusCode.BadRequest, response.Status);
        Assert.Contains("required", response.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(Service.ReceivedCalls());
    }

    [Fact]
    public async Task ExecuteAsync_ReplicationConflict_ReturnsSafeMessage()
    {
        Service.ApproveAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>())
            .ThrowsAsync(new RequestFailedException(
                (int)HttpStatusCode.Conflict,
                "backend payload containing internal replication metadata"));

        var response = await ExecuteCommandAsync(ValidArguments);

        Assert.Equal(HttpStatusCode.Conflict, response.Status);
        Assert.Contains("resource conflict", response.Message);
        Assert.DoesNotContain("backend payload", response.Message);
    }

    [Fact]
    public async Task ExecuteAsync_Forbidden_ReturnsSafeMessage()
    {
        Service.ApproveAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>())
            .ThrowsAsync(new RequestFailedException(
                (int)HttpStatusCode.Forbidden,
                "backend payload containing authorization metadata"));

        var response = await ExecuteCommandAsync(ValidArguments);

        Assert.Equal(HttpStatusCode.Forbidden, response.Status);
        Assert.Contains("Authorization failed", response.Message);
        Assert.DoesNotContain("backend payload", response.Message);
    }

    private const string ValidArguments =
        $"--account account1 --pool pool1 --volume volume1 --remote-volume-resource-id {RemoteVolumeResourceId} --resource-group rg --subscription sub";
}
