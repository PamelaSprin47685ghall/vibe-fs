namespace Wanxiangshu.Next.Journal

open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.Kernel.Identity

/// EXEC-009: a handle's durable lifecycle has three states, and they must be
/// distinguishable.
///
/// The previous model held two maps (linked / unlinked), which cannot express
/// completed-awaiting-join. EXEC-005 requires `list` to show that state, so a
/// finished-but-unjoined child was reported as running.
type HandleLifecycle =
    /// Linked and not yet completed. `list` shows running or busy.
    | Active
    /// A completion landed in the mailbox; nobody consumed it yet. `list` shows
    /// CompletedAwaitingJoin, and `join` may still return it.
    | CompletedAwaitingJoin of HandleCompletionKind
    /// EXEC-009 tombstone. Permanent. A retired id answers RetiredHandle forever
    /// and must never degrade into "treat the input as an agent name and fork
    /// again".
    | Retired

type HandleRecord =
    { Handle: HandleId
      TargetAgent: string
      CanonicalRole: string option
      Lifecycle: HandleLifecycle }

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

    let private normalizedRole role =
        role
        |> Option.bind (fun value ->
            if System.String.IsNullOrWhiteSpace value then
                None
            else
                Some value)

    let link
        (handle: HandleId)
        (targetAgent: string)
        (role: string option)
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
                              TargetAgent = targetAgent
                              CanonicalRole = normalizedRole role
                              Lifecycle = Active }
                            current.Handles }

    /// EXEC-004: terminal, send-failure and cancel race for one cell. Whoever
    /// arrives first wins; later arrivals are refused, not overwritten.
    let complete
        (handle: HandleId)
        (kind: HandleCompletionKind)
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
                                Lifecycle = CompletedAwaitingJoin kind }
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
