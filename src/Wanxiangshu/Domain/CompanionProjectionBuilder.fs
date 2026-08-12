namespace Wanxiangshu.Domain

open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity

/// Which physical request the Companion is about to make.
[<RequireQualifiedAccess>]
type CompanionRequestKind =
    | Normal
    | Squash of frameCount: int

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

    let private kindLabel (kind: CompanionRequestKind) =
        match kind with
        | CompanionRequestKind.Normal -> "normal"
        | CompanionRequestKind.Squash _ -> "squash"

    /// ENFORCER-071: one low-trust previous tip message.
    /// Domain shape is (FieldName, CycleId); Journal maps RecentTips into this.
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
    /// Squash: paired tip+frame units over oldest k frames + squash instruction LAST.
    /// Tips cover normal / squash / restart / recovery / compaction rebuilds because
    /// every rebuild path calls this builder with the same RecentTips projection.
    ///
    /// HOST-010: last user message binds the outbound assistant. For normal that
    /// is the combined delta message; for squash it is the instruction message.
    let build
        (sha256: string -> string)
        (bloggerSessionId: SessionId)
        (frameEpoch: FrameEpochId)
        (kind: CompanionRequestKind)
        (frameBodies: (BlobDigest * string) list)
        (physicalDelta: (string * string) option)
        (previousTips: (string * string) list)
        : CompanionProjectionPlan =
        let selected =
            match kind with
            | CompanionRequestKind.Normal -> frameBodies
            | CompanionRequestKind.Squash count -> frameBodies |> List.truncate count

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

        match kind with
        | CompanionRequestKind.Normal ->
            let deltaMessages =
                match physicalDelta with
                | Some(messageId, toml) ->
                    [ { MessageId = messageId
                        Role = "user"
                        Text = CompanionPrompt.newWorkMessage toml
                        IsPhysical = true } ]
                | None -> []

            // Paired observation units then combined delta last (HOST-010).
            { Messages = pairedHistory @ deltaMessages }
        | CompanionRequestKind.Squash _ ->
            let instruction =
                { MessageId = CompanionIdentity.instructionMessageId sha256 bloggerSessionId frameEpoch (kindLabel kind)
                  Role = "user"
                  Text = CompanionPrompt.SquashInstruction
                  IsPhysical = false }

            { Messages = pairedHistory @ [ instruction ] }

    /// First-turn shape: one physical combined delta (instruction header + data), no frames.
    let isFirstTurnShape (plan: CompanionProjectionPlan) =
        match plan.Messages with
        | [ delta ] ->
            delta.IsPhysical
            && delta.Text.StartsWith("# Write the dense work-log continuation now")
        | _ -> false