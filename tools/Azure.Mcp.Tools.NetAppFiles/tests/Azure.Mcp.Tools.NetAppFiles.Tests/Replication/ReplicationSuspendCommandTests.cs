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

public class ReplicationSuspendCommandTests
    : SubscriptionCommandUnitTestsBase<ReplicationSuspendCommand, INetAppFilesReplicationService>
{
    [Fact]
    public void Constructor_InitializesCommandCorrectly()
    {
        var command = Command.GetCommand();

        Assert.Equal("suspend", command.Name);
        Assert.False(string.IsNullOrWhiteSpace(command.Description));
    }

    [Fact]
    public async Task ExecuteAsync_SuspendsReplicationWithoutForceByDefault()
    {
        var response = await ExecuteCommandAsync(
            "--account", "account1",
            "--pool", "pool1",
            "--volume", "volume1",
            "--resource-group", "rg",
            "--subscription", "sub",
            "--tenant", "tenant");

        var result = ValidateAndDeserializeResponse(
            response,
            NetAppFilesJsonContext.Default.ReplicationSuspendResult);
        Assert.True(result.Suspended);
        await Service.Received(1).SuspendAsync(
            "account1",
            "pool1",
            "volume1",
            false,
            "rg",
            "sub",
            "tenant",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_ForceBreakReplication_ForwardsTrue()
    {
        var response = await ExecuteCommandAsync(
            "--account", "account1",
            "--pool", "pool1",
            "--volume", "volume1",
            "--force-break-replication", "true",
            "--resource-group", "rg",
            "--subscription", "sub");

        ValidateAndDeserializeResponse(
            response,
            NetAppFilesJsonContext.Default.ReplicationSuspendResult);
        await Service.Received(1).SuspendAsync(
            "account1",
            "pool1",
            "volume1",
            true,
            "rg",
            "sub",
            null,
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("--account account/name --pool pool1 --volume volume1 --resource-group rg --subscription sub", "--account")]
    [InlineData("--account account1 --pool pool/name --volume volume1 --resource-group rg --subscription sub", "--pool")]
    [InlineData("--account account1 --pool pool1 --volume volume/name --resource-group rg --subscription sub", "--volume")]
    [InlineData("--account account1 --pool pool1 --volume -volume --resource-group rg --subscription sub", "--volume")]
    public async Task ExecuteAsync_InvalidOption_ReturnsBadRequest(string args, string expectedMessage)
    {
        var response = await ExecuteCommandAsync(args);

        Assert.Equal(HttpStatusCode.BadRequest, response.Status);
        Assert.Contains(expectedMessage, response.Message);
        Assert.Empty(Service.ReceivedCalls());
    }

    [Theory]
    [InlineData("--pool pool1 --volume volume1 --resource-group rg --subscription sub")]
    [InlineData("--account account1 --volume volume1 --resource-group rg --subscription sub")]
    [InlineData("--account account1 --pool pool1 --resource-group rg --subscription sub")]
    [InlineData("--account account1 --pool pool1 --volume volume1 --subscription sub")]
    [InlineData("--account account1 --pool pool1 --volume volume1 --resource-group rg")]
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
        Service.SuspendAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<bool>(),
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
    public async Task ExecuteAsync_BadRequest_ReturnsSafeMessage()
    {
        Service.SuspendAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<bool>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>())
            .ThrowsAsync(new RequestFailedException(
                (int)HttpStatusCode.BadRequest,
                "backend payload containing transfer metadata"));

        var response = await ExecuteCommandAsync(ValidArguments);

        Assert.Equal(HttpStatusCode.BadRequest, response.Status);
        Assert.Contains("--force-break-replication", response.Message);
        Assert.DoesNotContain("backend payload", response.Message);
    }

    [Fact]
    public async Task ExecuteAsync_Forbidden_ReturnsSafeMessage()
    {
        Service.SuspendAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<bool>(),
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

    [Fact]
    public async Task ExecuteAsync_NotFound_ReturnsSafeMessage()
    {
        Service.SuspendAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<bool>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>())
            .ThrowsAsync(new RequestFailedException(
                (int)HttpStatusCode.NotFound,
                "backend payload containing resource metadata"));

        var response = await ExecuteCommandAsync(ValidArguments);

        Assert.Equal(HttpStatusCode.NotFound, response.Status);
        Assert.Contains("destination Azure NetApp Files volume", response.Message);
        Assert.DoesNotContain("backend payload", response.Message);
    }

    private const string ValidArguments =
        "--account account1 --pool pool1 --volume volume1 --resource-group rg --subscription sub";
}
