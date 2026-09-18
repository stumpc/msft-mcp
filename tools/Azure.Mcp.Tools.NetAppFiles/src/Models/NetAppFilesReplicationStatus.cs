// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Mcp.Tools.NetAppFiles.Models;

public record NetAppFilesReplicationStatus(
    bool? Healthy,
    string? RelationshipStatus,
    string? MirrorState,
    string? TotalProgress,
    string? ErrorMessage);
