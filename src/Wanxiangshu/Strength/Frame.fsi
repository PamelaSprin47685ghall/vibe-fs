namespace Wanxiangshu.Strength

open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Participant.Provider.Projection

type StrengthToolExchange =
    { ToolName: string
      CanonicalArguments: string
      CanonicalResult: string }

type StrengthRequestBatch =
    { RequestOrdinal: int
      Exchanges: StrengthToolExchange list }

type StrengthFrameBundle =
    { Batches: StrengthRequestBatch list
      Digest: string
      ByteLength: int }

[<RequireQualifiedAccess>]
type StrengthFrameError =
    | EmptyBundle
    | EmptyBatch of requestOrdinal: int
    | InvalidRequestOrdinal of expected: int * actual: int
    | UnsupportedTool of toolName: string
    | ByteLimitExceeded of actualBytes: int * maxBytes: int

[<RequireQualifiedAccess>]
type StrengthMirrorError =
    | DuplicateToolCallId of ToolCallId
    | OrphanToolResultId of ToolCallId
    | MediaCannotCrossSession

module StrengthFrame =
    val isAllowedTool: toolName: string -> bool
    val utf8ByteCount: value: string -> int
    val canonicalText: batches: StrengthRequestBatch list -> string

    val tryBuild:
        sha256: (string -> string) ->
        maxBytes: int ->
        batches: StrengthRequestBatch list ->
            Result<StrengthFrameBundle, StrengthFrameError>

    val tryLocalizeMirror:
        sha256: (string -> string) ->
        decisionId: StrengthDecisionId ->
        semanticDigest: string ->
        messages: ProviderProjection.WireMessage list ->
            Result<ProviderProjection.WireMessage list, StrengthMirrorError>

    val hostMessageId:
        sha256: (string -> string) ->
        ownerSessionId: SessionId ->
        decisionId: StrengthDecisionId ->
        requestOrdinal: int ->
        half: string ->
        semanticDigest: string ->
            string

    val wireToolCallId:
        sha256: (string -> string) ->
        ownerSessionId: SessionId ->
        decisionId: StrengthDecisionId ->
        requestOrdinal: int ->
        exchangeOrdinal: int ->
        semanticDigest: string ->
            string
