namespace Wanxiangshu.Next.Domain

open Wanxiangshu.Next.Kernel.Identity

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

    let private instructionFor (kind: CompanionRequestKind) =
        match kind with
        | CompanionRequestKind.Normal -> CompanionPrompt.NormalInstruction
        | CompanionRequestKind.Squash _ -> CompanionPrompt.SquashInstruction

    /// Normal: [[do_not_exec]] historic frames + [[new_work_to_record]] delta + instruction LAST.
    /// Squash: oldest k historic frames + squash instruction LAST (no delta).
    let build
        (sha256: string -> string)
        (bloggerSessionId: SessionId)
        (frameEpoch: FrameEpochId)
        (kind: CompanionRequestKind)
        (frameBodies: (BlobDigest * string) list)
        (physicalDelta: (string * string) option)
        : CompanionProjectionPlan =
        let selected =
            match kind with
            | CompanionRequestKind.Normal -> frameBodies
            | CompanionRequestKind.Squash count -> frameBodies |> List.truncate count

        let frameMessages =
            selected
            |> List.mapi (fun ordinal (digest, body) ->
                { MessageId = CompanionIdentity.frameMessageId sha256 bloggerSessionId frameEpoch ordinal digest
                  Role = "user"
                  Text = CompanionPrompt.workingRecordMessage body
                  IsPhysical = false })

        let deltaMessages =
            match kind, physicalDelta with
            | CompanionRequestKind.Normal, Some(messageId, toml) ->
                [ { MessageId = messageId
                    Role = "user"
                    Text = CompanionPrompt.newWorkMessage toml
                    IsPhysical = true } ]
            | _ -> []

        let instruction =
            { MessageId = CompanionIdentity.instructionMessageId sha256 bloggerSessionId frameEpoch (kindLabel kind)
              Role = "user"
              Text = instructionFor kind
              IsPhysical = false }

        // Instruction is always last (HOST-010 parent binding).
        { Messages = frameMessages @ deltaMessages @ [ instruction ] }

    /// First-turn shape: delta + instruction, no historic frames.
    let isFirstTurnShape (plan: CompanionProjectionPlan) =
        match plan.Messages with
        | [ delta; instruction ] ->
            delta.IsPhysical
            && not instruction.IsPhysical
            && instruction.Text.StartsWith("# Write the dense work-log continuation now")
        | _ -> false
