namespace Wanxiangshu.Domain

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
/// System is NOT here — managed-agent config owns blogger-system.md (ENFORCER-030).
type CompanionProjectionPlan =
    { Messages: CompanionProjectedMessage list }

/// COMPANION-005 / CTX-012: build provider-visible messages from durable frames.
[<RequireQualifiedAccess>]
module CompanionProjectionBuilder =

    let private kindLabel (kind: CompanionRequestKind) =
        match kind with
        | CompanionRequestKind.Normal -> "normal"
        | CompanionRequestKind.Squash _ -> "squash"

    /// ENFORCER-071: low-trust previous tip messages (oldest → newest).
    /// Domain shape is (FieldName, CycleId); Journal maps RecentTips into this.
    let private tipMessages
        (sha256: string -> string)
        (bloggerSessionId: SessionId)
        (previousTips: (string * string) list)
        : CompanionProjectedMessage list =
        previousTips
        |> List.map (fun (tipField, cycleId) ->
            { MessageId = CompanionIdentity.previousTipMessageId sha256 bloggerSessionId cycleId
              Role = "assistant"
              Text = CompanionPrompt.previousTipMessage tipField cycleId
              IsPhysical = false })

    /// Normal: previous_enforcer_tip blocks + historic frames + one user delta.
    /// Squash: previous_enforcer_tip blocks + oldest k frames + squash instruction LAST.
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

        let tips = tipMessages sha256 bloggerSessionId previousTips

        let frameMessages =
            selected
            |> List.mapi (fun ordinal (digest, body) ->
                { MessageId = CompanionIdentity.frameMessageId sha256 bloggerSessionId frameEpoch ordinal digest
                  // Role only: body still `[[do_not_exec]] historic_frame = …`.
                  Role = "assistant"
                  Text = CompanionPrompt.workingRecordMessage body
                  IsPhysical = false })

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

            // Tips then frames then combined delta last (HOST-010).
            { Messages = tips @ frameMessages @ deltaMessages }
        | CompanionRequestKind.Squash _ ->
            let instruction =
                { MessageId = CompanionIdentity.instructionMessageId sha256 bloggerSessionId frameEpoch (kindLabel kind)
                  Role = "user"
                  Text = CompanionPrompt.SquashInstruction
                  IsPhysical = false }

            { Messages = tips @ frameMessages @ [ instruction ] }

    /// First-turn shape: one physical combined delta (instruction header + data), no frames.
    let isFirstTurnShape (plan: CompanionProjectionPlan) =
        match plan.Messages with
        | [ delta ] ->
            delta.IsPhysical
            && delta.Text.StartsWith("# Write the dense work-log continuation now")
        | _ -> false
