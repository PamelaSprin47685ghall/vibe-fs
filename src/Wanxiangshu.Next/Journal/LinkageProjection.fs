namespace Wanxiangshu.Next.Journal

open Wanxiangshu.Next.Kernel
open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.Kernel.Identity

/// EXEC-009: a handle's durable lifecycle has three states, and they must be
/// distinguishable.
///
/// The previous model held two maps (linked / unlinked), which cannot express
/// completed-awaiting-join. EXEC-005 requires `list` to show that state, so a
/// finished-but-unjoined child was reported as running.
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
/// EXEC-009 forbids.
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

    /// `join` consumed the completion and wrote the tombstone (EXEC-004).
    let retire
        (handle: HandleId)
        (current: AgentLinkageProjection)
        : Result<AgentLinkageProjection, HandleTransitionRejection> =
        match Map.tryFind handle current.Handles with
        | None -> Error UnknownHandle
        | Some { Lifecycle = Retired } -> Error HandleIsRetired
        | Some { Lifecycle = Active } -> Error NotCompleted
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

    let private recordsWhere predicate (current: AgentLinkageProjection) =
        current.Handles |> Map.toList |> List.map snd |> List.filter predicate

    /// EXEC-005: `list` shows running, busy and completed-awaiting-join, never
    /// retired.
    let listable (current: AgentLinkageProjection) =
        current
        |> recordsWhere (fun record ->
            match record.Lifecycle with
            | Retired -> false
            | Active
            | CompletedAwaitingJoin _ -> true)

    /// EXEC-004: what `join` may consume.
    let joinable (current: AgentLinkageProjection) =
        current
        |> recordsWhere (fun record ->
            match record.Lifecycle with
            | CompletedAwaitingJoin _ -> true
            | Active
            | Retired -> false)

    /// EXEC-009: parent abort cancels every owned resource individually, so the
    /// caller needs the actual handles rather than a count.
    let activeHandles (current: AgentLinkageProjection) =
        current
        |> recordsWhere (fun record ->
            match record.Lifecycle with
            | Active -> true
            | CompletedAwaitingJoin _
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

    /// Every child session this parent has ever linked.
    ///
    /// Replaces the old `LinkedChildren` map. That map was keyed by child and held
    /// only live entries, so restart recovery and the retired-handle check needed
    /// two different structures; one list of records answers both.
    let linkedChildren (current: AgentLinkageProjection) =
        current.Handles |> Map.toList |> List.map snd
