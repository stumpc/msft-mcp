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

public class ReplicationResumeCommandTests
    : SubscriptionCommandUnitTestsBase<ReplicationResumeCommand, INetAppFilesReplicationService>
{
    [Fact]
    public void Constructor_InitializesCommandCorrectly()
    {
        var command = Command.GetCommand();

        Assert.Equal("resume", command.Name);
        Assert.False(string.IsNullOrWhiteSpace(command.Description));
    }

    [Fact]
    public async Task ExecuteAsync_ResumesReplication()
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
            NetAppFilesJsonContext.Default.ReplicationResumeResult);
        Assert.True(result.Resumed);
        await Service.Received(1).ResumeAsync(
            "account1",
            "pool1",
            "volume1",
            "rg",
            "sub",
            "tenant",
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

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, "suspended replication relationship")]
    [InlineData(HttpStatusCode.Conflict, "resource conflict")]
    [InlineData(HttpStatusCode.Forbidden, "Authorization failed")]
    [InlineData(HttpStatusCode.NotFound, "destination Azure NetApp Files volume")]
    public async Task ExecuteAsync_RequestFailure_ReturnsSafeMessage(
        HttpStatusCode statusCode,
        string expectedMessage)
    {
        Service.ResumeAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>())
            .ThrowsAsync(new RequestFailedException(
                (int)statusCode,
                "backend payload containing internal replication metadata"));

        var response = await ExecuteCommandAsync(ValidArguments);

        Assert.Equal(statusCode, response.Status);
        Assert.Contains(expectedMessage, response.Message);
        Assert.DoesNotContain("backend payload", response.Message);
    }

    private const string ValidArguments =
        "--account account1 --pool pool1 --volume volume1 --resource-group rg --subscription sub";
}
