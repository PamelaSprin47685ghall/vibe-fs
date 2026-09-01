namespace Wanxiangshu.Context.Companion

open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Participant.Provider.Projection

[<RequireQualifiedAccess>]
type CompanionRequestKind =
    | Normal
    | Squash of frameCount: int

type CompanionProjectedMessage =
    { MessageId: string
      Role: string
      Text: string
      IsPhysical: bool }

type CompanionProjectionPlan =
    { Messages: CompanionProjectedMessage list }

[<RequireQualifiedAccess>]
module CompanionProjectionBuilder =
    val build:
        sha256: (string -> string) ->
        bloggerSessionId: SessionId ->
        frameEpoch: FrameEpochId ->
        kind: CompanionRequestKind ->
        frameBodies: (BlobDigest * string) list ->
        physicalDelta: (string * BloggerDeltaItem list) option ->
        previousTips: (string * string) list ->
        normalInstructionLines: string list ->
        squashInstructionLines: string list ->
            CompanionProjectionPlan

    val projectionIntent:
        sha256: (string -> string) ->
        bloggerSessionId: SessionId ->
        frameEpoch: FrameEpochId ->
        kind: CompanionRequestKind ->
        frameBodies: (BlobDigest * string) list ->
        physicalDelta: (string * BloggerDeltaItem list) option ->
        previousTips: (string * string) list ->
        normalInstructionLines: string list ->
        squashInstructionLines: string list ->
            ProjectionIntent option

    val isFirstTurnShape: plan: CompanionProjectionPlan -> bool
