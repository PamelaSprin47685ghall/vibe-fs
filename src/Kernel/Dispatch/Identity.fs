module Wanxiangshu.Kernel.Dispatch.Identity

open Wanxiangshu.Kernel.Primitives.Identity
open Fable.Core
open Fable.Core.JsInterop

/// Unique identity for one logical prompt dispatch request, scoped to a workspace
/// so multiple plugin instances in the same Node process cannot collide.
type DispatchId = private DispatchId of string

module DispatchId =
    let create (s: string) : DispatchId =
        if s = "" then
            failwith "DispatchId cannot be empty"
        else
            DispatchId s

    let value (DispatchId s) : string = s

    let newId () : DispatchId =
        DispatchId("dispatch-" + System.Guid.NewGuid().ToString("N"))

    let tryParse (s: string) : DispatchId option =
        if s = "" then None else Some(DispatchId s)

let private nowMs () : int64 =
    int64 (Fable.Core.JsInterop.emitJsExpr () "Date.now()")

/// What kind of owner produced the dispatch. One concrete case per code path
/// that sends a plugin-originated prompt. Used to attribute receipts, late
/// evidence, and reconciliation.
type DispatchKind =
    | Nudge
    | FallbackContinuation
    | SubsessionTurn
    | Reviewer
    | Notification
    | Compaction

module DispatchKind =
    let toString =
        function
        | Nudge -> "nudge"
        | FallbackContinuation -> "fallback_continuation"
        | SubsessionTurn -> "subsession_turn"
        | Reviewer -> "reviewer"
        | Notification -> "notification"
        | Compaction -> "compaction"

    let ofString (s: string) : DispatchKind option =
        match s with
        | "nudge" -> Some Nudge
        | "fallback_continuation" -> Some FallbackContinuation
        | "subsession_turn" -> Some SubsessionTurn
        | "reviewer" -> Some Reviewer
        | "notification" -> Some Notification
        | "compaction" -> Some Compaction
        | _ -> None

/// Authoritative identity carried alongside every prompt dispatch. The only
/// durable proof of attribution. Schema-versioned so old NDJSON rows remain
/// parseable.
type DispatchIdentity =
    {
        SchemaVersion: int
        DispatchId: DispatchId
        WorkspaceId: WorkspaceId
        PhysicalSessionId: string
        Kind: DispatchKind
        RunGeneration: int
        CancelGeneration: int
        Attempt: int
        /// TurnId of the logical turn the dispatch is part of (subsession TurnId,
        /// nudge id, continuation id). Carries correlation across receipts.
        LogicalTurnId: string
        HumanTurnId: string
        RequestedAtMs: int64
        ExpectedParentId: string
        Metadata: Map<string, string>
    }

module DispatchIdentity =
    let newId
        (workspace: WorkspaceId)
        (physicalSessionId: string)
        (kind: DispatchKind)
        (runGen: int)
        (cancelGen: int)
        (attempt: int)
        (logicalTurnId: string)
        (humanTurnId: string)
        (expectedParentId: string)
        : DispatchIdentity =
        { SchemaVersion = 1
          DispatchId = DispatchId.newId ()
          WorkspaceId = workspace
          PhysicalSessionId = physicalSessionId
          Kind = kind
          RunGeneration = runGen
          CancelGeneration = cancelGen
          Attempt = attempt
          LogicalTurnId = logicalTurnId
          HumanTurnId = humanTurnId
          RequestedAtMs = nowMs ()
          ExpectedParentId = expectedParentId
          Metadata = Map.empty }
