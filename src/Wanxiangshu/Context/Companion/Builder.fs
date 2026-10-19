namespace Wanxiangshu.Context.Companion

open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Projection

/// Which physical request the Companion is about to make.
[<RequireQualifiedAccess>]
type CompanionRequestKind = | Normal

/// One message in the Companion's provider-visible projection.
type CompanionProjectedMessage =
    { MessageId: string
      Role: string
      Text: string
      IsPhysical: bool }

/// COMPANION-005: message list for one Companion request.
/// System is NOT here — managed-agent config owns PromptResources Blogger Role Law (ENFORCER-030).
type CompanionProjectionPlan =
    { Messages: CompanionProjectedMessage list }

/// COMPANION-005 / CTX-012: build provider-visible messages from durable frames.
[<RequireQualifiedAccess>]
module CompanionProjectionBuilder =

    /// ENFORCER-071: one low-trust previous tip message.
    /// Domain shape is (TipName, CycleId); Journal maps RecentTips into this.
    let private tipMessage
        (sha256: string -> string)
        (bloggerSessionId: SessionId)
        (tipField: string, cycleId: string)
        : CompanionProjectedMessage =
        { MessageId = CompanionIdentity.previousTipMessageId sha256 bloggerSessionId cycleId
          Role = "assistant"
          Text = CompanionPrompt.previousTipMessage tipField cycleId
          IsPhysical = false }

    /// Pair tips with frames into interleaved tip+frame observation units (oldest → newest).
    /// Zip from the front: tipᵢ then frameᵢ while both remain; leftover tips or frames
    /// append unpaired. Prefer this over tips∥frames parallel streams (rulebook §2).
    let private pairTipFrameUnits
        (tips: CompanionProjectedMessage list)
        (frames: CompanionProjectedMessage list)
        : CompanionProjectedMessage list =
        let rec loop tipRest frameRest acc =
            match tipRest, frameRest with
            | t :: ts, f :: fs -> loop ts fs (f :: t :: acc)
            | ts, [] -> List.rev acc @ ts
            | [], fs -> List.rev acc @ fs

        loop tips frames []

    /// Normal: paired previous_enforcer_tip + historic_frame units + one user delta.
    /// Tips cover normal / restart / recovery / compaction rebuilds because
    /// every rebuild path calls this builder with the same RecentTips projection.
    ///
    /// HOST-010: last user message binds the outbound assistant (the combined delta message).
    let build
        (sha256: string -> string)
        (bloggerSessionId: SessionId)
        (frameEpoch: FrameEpochId)
        (kind: CompanionRequestKind)
        (frameBodies: (BlobDigest * string) list)
        (physicalDelta: (string * BloggerDeltaItem list) option)
        (previousTips: (string * string) list)
        (normalInstructionLines: string list)
        : CompanionProjectionPlan =
        let selected =
            match kind with
            | CompanionRequestKind.Normal -> frameBodies

        let tipMsgs = previousTips |> List.map (tipMessage sha256 bloggerSessionId)

        let frameMessages =
            selected
            |> List.mapi (fun ordinal (digest, body) ->
                { MessageId = CompanionIdentity.frameMessageId sha256 bloggerSessionId frameEpoch ordinal digest
                  // Role only: body still `[[do_not_exec]] historic_frame = …`.
                  Role = "assistant"
                  Text = CompanionPrompt.workingRecordMessage body
                  IsPhysical = false })

        let pairedHistory = pairTipFrameUnits tipMsgs frameMessages

        let deltaMessagesForPhysical =
            match physicalDelta with
            | Some(messageId, items) ->
                [ { MessageId = messageId
                    Role = "user"
                    Text = CompanionPrompt.newWorkMessage normalInstructionLines items
                    IsPhysical = true } ]
            | None -> []

        match kind with
        | CompanionRequestKind.Normal ->
            // Paired observation units then combined delta last (HOST-010).
            { Messages = pairedHistory @ deltaMessagesForPhysical }

    let private projectionKey (bloggerSessionId: SessionId) (frameEpoch: FrameEpochId) (kind: CompanionRequestKind) =
        let request =
            match kind with
            | CompanionRequestKind.Normal -> "normal"

        $"companion:{SessionId.value bloggerSessionId}:{FrameEpochId.value frameEpoch}:{request}"

    let private ownerProjectionIntent fullRebuild key rows =
        if fullRebuild then
            ProjectionIntent.replaceMessageBase key rows
        else
            ProjectionIntent.insertMessageRows key (ProjectionMessageAnchor.BeforeMessageIndex 1) rows

    let private projectionRow (message: CompanionProjectedMessage) : ProjectionMessageRow =
        { Message =
            { Role = message.Role
              Parts = [ ProviderProjection.WireText message.Text ] }
          HostMessageId = Some message.MessageId
          HostIsPhysical = message.IsPhysical }

    /// Materialize Companion-owned message shape and identity before crossing
    /// into the provider's generic projection algebra.
    let projectionIntent
        (sha256: string -> string)
        (bloggerSessionId: SessionId)
        (frameEpoch: FrameEpochId)
        (kind: CompanionRequestKind)
        (frameBodies: (BlobDigest * string) list)
        (physicalDelta: (string * BloggerDeltaItem list) option)
        (previousTips: (string * string) list)
        (normalInstructionLines: string list)
        : ProjectionIntent option =
        let fullRebuild = Option.isSome physicalDelta || not (List.isEmpty previousTips)

        if List.isEmpty frameBodies && not fullRebuild then
            None
        else
            let rows =
                build
                    sha256
                    bloggerSessionId
                    frameEpoch
                    kind
                    frameBodies
                    physicalDelta
                    previousTips
                    normalInstructionLines
                |> fun plan -> plan.Messages
                |> List.map projectionRow

            let key = projectionKey bloggerSessionId frameEpoch kind
            ownerProjectionIntent fullRebuild key rows |> Some

    /// First-turn shape: one physical user message whose body is an ARCH-010 comment header.
    /// Language-agnostic — do not match English prose (PROMPT-019).
    let isFirstTurnShape (plan: CompanionProjectionPlan) =
        match plan.Messages with
        | [ delta ] -> delta.IsPhysical && delta.Text.StartsWith("# ")
        | _ -> false
