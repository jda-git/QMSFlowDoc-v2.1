namespace QMSFlowDoc.Shared.DTOs;

public record SyncStatusDto(
    Guid DocumentId,
    string? RemoteEtag,
    string? LocalHash,
    DateTime LastSyncAt
);

public record SyncConflictDto(
    Guid DocumentId,
    string RemoteTitle,
    string LocalTitle,
    DateTime RemoteUpdatedAt,
    DateTime LocalUpdatedAt
);

