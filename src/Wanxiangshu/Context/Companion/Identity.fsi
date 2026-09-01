namespace Wanxiangshu.Context.Companion

open Wanxiangshu.Foundation.Identity

[<RequireQualifiedAccess>]
module CompanionIdentity =
    val sealRoot:
        sha256: (string -> string) ->
        mainSessionId: SessionId ->
        basedOnEpoch: PrefixEpochId ->
        candidateCutoff: int ->
        candidateCoveredPrefixDigest: string ->
        candidateFrozenRecordPrefixDigest: BlobDigest ->
            string

    val companionMemoryMessageId: sha256: (string -> string) -> sealRoot: string -> string

    val frameMessageId:
        sha256: (string -> string) ->
        bloggerSessionId: SessionId ->
        frameEpoch: FrameEpochId ->
        frameOrdinal: int ->
        frameDigest: BlobDigest ->
            string

    val instructionMessageId:
        sha256: (string -> string) ->
        bloggerSessionId: SessionId ->
        frameEpoch: FrameEpochId ->
        requestKind: string ->
            string

    val newWorkMessageId: sha256: (string -> string) -> bloggerSessionId: SessionId -> deltaDigest: BlobDigest -> string
    val previousTipMessageId: sha256: (string -> string) -> bloggerSessionId: SessionId -> cycleId: string -> string
