namespace Wanxiangshu.Next.Domain

open Wanxiangshu.Next.Kernel.Identity

/// Which physical request the Companion is about to make.
///
/// The two kinds differ in three ways at once — which frames are projected, which
/// instruction is sent, and whether a delta follows — so a bool would leave the
/// caller to keep three decisions consistent.
[<RequireQualifiedAccess>]
type CompanionRequestKind =
    | Normal
    | Squash of frameCount: int

/// One message in the Companion's provider-visible projection.
///
/// `IsPhysical` marks the message the Host actually persisted. Everything else is
/// synthetic and exists only for the duration of one request, which is what makes
/// the frame history replayable without a physical transcript (PERSIST-010).
type CompanionProjectedMessage =
    { MessageId: string
      Role: string
      Text: string
      IsPhysical: bool }

/// COMPANION-005: the whole message list for one Companion request.
type CompanionProjectionPlan =
    { System: string
      Messages: CompanionProjectedMessage list }

/// COMPANION-005 / CTX-012: build the Companion's provider-visible message list.
///
/// Pure. Frame bodies arrive already resolved from blobs, because reading a
/// `BlobRef` is a Host concern and this module must stay callable from a layer-1
/// test (VERIFY-008).
[<RequireQualifiedAccess>]
module CompanionProjectionBuilder =

    /// The request-kind label that keys the instruction message id (COMPANION-013).
    let private kindLabel (kind: CompanionRequestKind) =
        match kind with
        | CompanionRequestKind.Normal -> "normal"
        | CompanionRequestKind.Squash _ -> "squash"

    let private instructionFor (kind: CompanionRequestKind) =
        match kind with
        | CompanionRequestKind.Normal -> CompanionPrompt.NormalInstruction
        | CompanionRequestKind.Squash _ -> CompanionPrompt.SquashInstruction

    /// COMPANION-005 / CTX-012.
    ///
    /// Normal:  system, every frame, normal instruction, physical delta LAST.
    /// Squash:  system, the oldest `frameCount` frames, squash instruction LAST.
    ///
    /// The delta is last on a normal request because that keeps the physical message
    /// the provider sees last as well. HOST-010's binding does not require it — the
    /// Host resolves `parentID` from the pre-transform message list — but any other
    /// order would let the Host and the provider disagree about which message is this
    /// turn's new material.
    ///
    /// A squash carries no delta at all: CTX-012 forbids showing it the current delta
    /// or the later frames, because a rewrite that saw them would fold unconsumed
    /// material into a frame claiming to summarise only the old ones.
    ///
    /// Consecutive user messages are deliberate and accepted (COMPANION-005).
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
                  Text = body
                  IsPhysical = false })

        let instruction =
            { MessageId = CompanionIdentity.instructionMessageId sha256 bloggerSessionId frameEpoch (kindLabel kind)
              Role = "user"
              Text = instructionFor kind
              IsPhysical = false }

        let tail =
            match kind, physicalDelta with
            | CompanionRequestKind.Normal, Some(messageId, toml) ->
                [ { MessageId = messageId
                    Role = "user"
                    Text = toml
                    IsPhysical = true } ]
            // A squash ignores any delta it was handed rather than trusting the
            // caller not to pass one: the exclusion is a clause, so it is enforced
            // where the projection is built, not documented at the call site.
            | _ -> []

        { System = CompanionPrompt.System
          Messages = frameMessages @ [ instruction ] @ tail }

    /// COMPANION-005 first-turn degeneration: no frames yet.
    ///
    /// Not a special case in `build` — an empty frame list produces exactly this — but
    /// named so the clause has something to point at, and so a test can assert that
    /// the ordering is identical rather than merely similar.
    let isFirstTurnShape (plan: CompanionProjectionPlan) =
        match plan.Messages with
        | [ instruction; delta ] -> not instruction.IsPhysical && delta.IsPhysical
        | _ -> false
