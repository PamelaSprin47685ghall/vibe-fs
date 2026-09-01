namespace Wanxiangshu.Context.Companion.Blogger

open Wanxiangshu.Participant.Provider.Projection.ProviderProjection

type SemanticCursor =
    { TurnIndex: int
      PartIndex: int }

type BloggerDeltaChunk =
    { Items: BloggerDeltaItem list
      Toml: string
      NextCursor: SemanticCursor
      NextCoverableTurnCutoffExclusive: int }

[<RequireQualifiedAccess>]
module BloggerDelta =
    val DeltaLimitBytes: int

    val nextChunk:
        limitBytes: int ->
        cursor: SemanticCursor ->
        previousCutoff: int ->
        messages: SemanticMessage list ->
            BloggerDeltaChunk option
