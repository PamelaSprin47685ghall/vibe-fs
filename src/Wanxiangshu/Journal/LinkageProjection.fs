namespace Wanxiangshu.Journal

open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Fact
open Wanxiangshu.Kernel.Identity

/// EXEC-009: a handle's durable lifecycle has four states, and they must be
/// distinguishable.
///
/// The previous model held two maps (linked / unlinked), which cannot express
/// completed-awaiting-join. EXEC-005 requires `list` to show that state, so a
/// finished-but-unjoined child was reported as running. Abandoned is a durable
/// terminal that is never joinable and never reverts.
type HandleCompletion =
    {
        Kind: HandleCompletionKind
        /// Durable join payload. `None` for Cancelled and for 0.5.1 lines that
        /// predate the blob fields.
        CompletionRef: BlobRef option
        CompletionDigest: BlobDigest option
    }

type HandleLifecycle =
    /// Linked and not yet completed. `list` shows running or busy.
    | Active
    /// Completion is durable; nobody has consumed it yet. `list` shows
    /// CompletedAwaitingJoin, and `join` may still return it from the blob.
    | CompletedAwaitingJoin of HandleCompletion
    /// EXEC-009: durable abandon. Not joinable. Permanent until process-level
    /// bookkeeping ends; never reverts to Active or CompletedAwaitingJoin.
    | Abandoned of HandleAbandonReason
    /// EXEC-009 tombstone. Permanent. A retired id answers RetiredHandle forever
    /// and must never degrade into "treat the input as an agent name and fork
    /// again".
    | Retired

type HandleRecord =
    {
        Handle: HandleId
        /// The Host session this handle drives. EXEC-009: recovery must rebind the
        /// same handle id to the same session, and only the Host can issue that id.
        ChildSessionId: SessionId
        TargetAgent: string
        CanonicalRole: Role
        Lifecycle: HandleLifecycle
    }

/// Durable handle linkage for one parent session.
///
/// PERSIST-008: one map, keyed lookup, no history scan. Retired entries stay in
/// the map — that IS the tombstone. Removing them would make a retired id
/// indistinguishable from one that never existed, which is the exact confusion
/// EXEC-009 forbids. Abandoned entries stay for the same reason.
type AgentLinkageProjection =
    { Handles: Map<HandleId, HandleRecord> }

/// Why a lifecycle transition was refused.
type HandleTransitionRejection =
    | UnknownHandle
    /// EXEC-009: retired is terminal. Re-linking a retired id is the failure
    /// mode the tombstone exists to prevent.
    | HandleIsRetired
    /// EXEC-004: the completion cell is single-assignment; the first winner is
    /// the only winner.
    | AlreadyCompleted
    /// EXEC-009: abandon is single-assignment; a second abandon is refused.
    | AlreadyAbandoned
    /// `join` consumes a completion, and an active handle has none.
    | NotCompleted

module HandleProjection =

    let empty = { Handles = Map.empty }

    let link
        (handle: HandleId)
        (childSessionId: SessionId)
        (targetAgent: string)
        (role: Role)
        (current: AgentLinkageProjection)
        : Result<AgentLinkageProjection, HandleTransitionRejection> =
        match Map.tryFind handle current.Handles with
        | Some { Lifecycle = Retired } -> Error HandleIsRetired
        | Some { Lifecycle = Abandoned _ } -> Error AlreadyAbandoned
        | _ ->
            Ok
                { current with
                    Handles =
                        Map.add
                            handle
                            { Handle = handle
                              ChildSessionId = childSessionId
                              TargetAgent = targetAgent
                              CanonicalRole = role
                              Lifecycle = Active }
                            current.Handles }

    /// EXEC-004: terminal, send-failure and cancel race for one cell. Whoever
    /// arrives first wins; later arrivals are refused, not overwritten.
    let complete
        (handle: HandleId)
        (completion: HandleCompletion)
        (current: AgentLinkageProjection)
        : Result<AgentLinkageProjection, HandleTransitionRejection> =
        match Map.tryFind handle current.Handles with
        | None -> Error UnknownHandle
        | Some { Lifecycle = Retired } -> Error HandleIsRetired
        | Some { Lifecycle = Abandoned _ } -> Error AlreadyAbandoned
        | Some { Lifecycle = CompletedAwaitingJoin _ } -> Error AlreadyCompleted
        | Some record ->
            Ok
                { current with
                    Handles =
                        Map.add
                            handle
                            { record with
                                Lifecycle = CompletedAwaitingJoin completion }
                            current.Handles }

    /// EXEC-009: durable abandon. Active or CompletedAwaitingJoin → Abandoned.
    /// First winner wins; later abandons are refused.
    let abandon
        (handle: HandleId)
        (reason: HandleAbandonReason)
        (current: AgentLinkageProjection)
        : Result<AgentLinkageProjection, HandleTransitionRejection> =
        match Map.tryFind handle current.Handles with
        | None -> Error UnknownHandle
        | Some { Lifecycle = Retired } -> Error HandleIsRetired
        | Some { Lifecycle = Abandoned _ } -> Error AlreadyAbandoned
        | Some record ->
            Ok
                { current with
                    Handles =
                        Map.add
                            handle
                            { record with
                                Lifecycle = Abandoned reason }
                            current.Handles }

    /// `join` consumed the completion and wrote the tombstone (EXEC-004).
    /// Abandoned is not joinable and must not retire via this path.
    let retire
        (handle: HandleId)
        (current: AgentLinkageProjection)
        : Result<AgentLinkageProjection, HandleTransitionRejection> =
        match Map.tryFind handle current.Handles with
        | None -> Error UnknownHandle
        | Some { Lifecycle = Retired } -> Error HandleIsRetired
        | Some { Lifecycle = Active } -> Error NotCompleted
        | Some { Lifecycle = Abandoned _ } -> Error AlreadyAbandoned
        | Some record ->
            Ok
                { current with
                    Handles = Map.add handle { record with Lifecycle = Retired } current.Handles }

    let tryFind (handle: HandleId) (current: AgentLinkageProjection) = Map.tryFind handle current.Handles

    /// EXEC-009: the question `fork` must ask before treating an id as anything.
    let isRetired (handle: HandleId) (current: AgentLinkageProjection) =
        match Map.tryFind handle current.Handles with
        | Some { Lifecycle = Retired } -> true
        | _ -> false

    /// EXEC-009: abandoned is durable terminal and not joinable.
    let isAbandoned (handle: HandleId) (current: AgentLinkageProjection) =
        match Map.tryFind handle current.Handles with
        | Some { Lifecycle = Abandoned _ } -> true
        | _ -> false

    let private recordsWhere predicate (current: AgentLinkageProjection) =
        current.Handles |> Map.toList |> List.map snd |> List.filter predicate

    /// EXEC-005: `list` shows running, busy and completed-awaiting-join, never
    /// retired or abandoned.
    let listable (current: AgentLinkageProjection) =
        current
        |> recordsWhere (fun record ->
            match record.Lifecycle with
            | Retired
            | Abandoned _ -> false
            | Active
            | CompletedAwaitingJoin _ -> true)

    /// EXEC-004: what `join` may consume. Abandoned is never joinable.
    let joinable (current: AgentLinkageProjection) =
        current
        |> recordsWhere (fun record ->
            match record.Lifecycle with
            | CompletedAwaitingJoin _ -> true
            | Active
            | Abandoned _
            | Retired -> false)

    /// EXEC-009: parent abort cancels every owned resource individually, so the
    /// caller needs the actual handles rather than a count.
    let activeHandles (current: AgentLinkageProjection) =
        current
        |> recordsWhere (fun record ->
            match record.Lifecycle with
            | Active -> true
            | CompletedAwaitingJoin _
            | Abandoned _
            | Retired -> false)

    /// The handle driving a child session, retired ones included.
    ///
    /// Retired records are deliberately visible: EXEC-009 makes the tombstone
    /// permanent, and a caller asking "is this session one of my children" must get
    /// yes for a child that already finished. Filtering here would make a retired
    /// child look like one that never existed.
    let tryFindByChildSession (childSessionId: SessionId) (current: AgentLinkageProjection) =
        current.Handles
        |> Map.tryPick (fun _ record ->
            if record.ChildSessionId = childSessionId then
                Some record
            else
                None)

    /// Fork-child main is sealed for Blogger once joinable, abandoned, or retired.
    /// Human root (no handle) is never sealed by this rule.
    let lifecycleSealsBlogger (lifecycle: HandleLifecycle) : bool =
        match lifecycle with
        | CompletedAwaitingJoin _
        | Abandoned _
        | Retired -> true
        | Active -> false

    let recordSealsBlogger (record: HandleRecord) : bool = lifecycleSealsBlogger record.Lifecycle

    /// Every child session this parent has ever linked.
    ///
    /// Replaces the old `LinkedChildren` map. That map was keyed by child and held
    /// only live entries, so restart recovery and the retired-handle check needed
    /// two different structures; one list of records answers both.
    let linkedChildren (current: AgentLinkageProjection) =
        current.Handles |> Map.toList |> List.map snd
