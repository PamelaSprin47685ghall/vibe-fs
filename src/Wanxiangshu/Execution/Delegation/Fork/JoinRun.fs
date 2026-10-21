namespace Wanxiangshu.Execution.Delegation.Fork

open System
open Wanxiangshu.Execution.Session
open Wanxiangshu.Execution.Session.Wait
open Wanxiangshu.Foundation

/// The product rules a join run obeys, host-neutral.
/// A host adapter owns the wiring (where the runtime lives, which journal
/// answers, which mailbox waits); it owns no decision. The decisions live
/// here: which route the caller's role takes, which lane is drained, which
/// budget applies, whose name and role are reported, and what a completed
/// window leaves behind.
module JoinRun =

    /// The DevOps join budget in milliseconds. A DevOps join may not wait
    /// longer than this; every other role waits untimed.
    [<Literal>]
    let DevOpsJoinTimeoutMs = 10_000

    /// Which join path the caller's role selects.
    type JoinRoute =
        /// The authority role waits on the ManagerJob verdict mailbox.
        | OrchestratorRoute
        /// Every other role waits on its own children's completion batch.
        | AgentRoute

    /// The lane a join drains from.
    type JoinLane =
        /// The logical owner's own lane.
        | OwnerLane
        /// A process-local fission membership's lane.
        | FissionLane of groupId: string * laneIndex: int

    /// Durable and live identity evidence for the agents a join may report.
    /// The durable evidence is the journal handle's byname and canonical role;
    /// the live evidence is the process table's agent row.
    type AgentIdentity =
        { DurableByname: string -> string option
          LiveAgentName: string -> string option
          DurableRole: string -> Role option
          RuntimeRole: string -> Role option }

    /// The window a rendered batch covers: what it delivered and what is left.
    type BatchWindow =
        { Delivered: JoinItem list
          Remaining: JoinItem list }

    /// The route a role selects.
    let decideRoute (role: Role option) : JoinRoute =
        match role with
        | Some Role.Orchestrator -> OrchestratorRoute
        | _ -> AgentRoute

    /// Whether this role's join carries a wait budget.
    let appliesWaitBudget (role: Role option) : bool =
        match role with
        | Some Role.DevOps -> true
        | _ -> false

    /// The interrupt reason a join reports. A lapsed budget is the only
    /// reason that differs from the one the wait produced.
    let normalizeInterruptReason (reason: JoinInterruptReason) : JoinInterruptReason =
        match reason with
        | JoinInterruptReason.OperatorAbort
        | JoinInterruptReason.UserMessageArrived
        | JoinInterruptReason.DeadlineExpired -> reason

    /// The lane a join drains from: a process-local fission membership, else
    /// the owner's own lane.
    let decideLane (fissionMembership: (string * int) option) : JoinLane =
        match fissionMembership with
        | Some(groupId, laneIndex) -> FissionLane(groupId, laneIndex)
        | None -> OwnerLane

    /// The agent name: the durable byname first, the live runtime table second.
    /// A restarted process may only know the child from its durable handle.
    let resolveAgentName (identity: AgentIdentity) (agentId: string) : string =
        let durableByname =
            identity.DurableByname agentId
            |> Option.filter (String.IsNullOrWhiteSpace >> not)
            |> Option.map (fun byname -> byname.Trim())

        match durableByname with
        | Some byname -> byname
        | None -> identity.LiveAgentName agentId |> Option.defaultValue ""

    /// The agent role: the payload's own role first, then the durable role,
    /// then the runtime role, then nothing.
    let resolveAgentRole (identity: AgentIdentity) (agentId: string) (fallback: Role option) : string =
        match fallback with
        | Some role -> Roles.roleLabel role
        | None ->
            identity.DurableRole agentId
            |> Option.orElseWith (fun () -> identity.RuntimeRole agentId)
            |> Option.map Roles.roleLabel
            |> Option.defaultValue ""

    /// The PTY label: the running command when it has one, else the terminal's name.
    let resolveTerminalLabel (command: string option) (terminalName: string option) : string =
        match command with
        | Some text when not (String.IsNullOrWhiteSpace text) -> text.Trim()
        | _ -> terminalName |> Option.defaultValue ""

    /// Split a rendered batch into the items it delivered and the items left over.
    let windowOf (batch: NonEmptyBatch<JoinItem>) (remaining: JoinItem list) : BatchWindow =
        let deliveredCount = max 0 (NonEmptyBatch.length batch - List.length remaining)

        let delivered = NonEmptyBatch.toList batch |> List.take deliveredCount

        { Delivered = delivered
          Remaining = remaining }

    /// The PTY tracks the delivered items hold; agent items carry none.
    let ptyTrackIds (items: JoinItem list) : string list =
        items
        |> List.choose (fun item ->
            match item with
            | JoinItem.PtyItem ptyItem -> Some(PtyJoinItem.ptyId ptyItem)
            | JoinItem.AgentItem _ -> None)

    /// The orchestrator mailbox failure → the stable wire code.
    let mapOrchestratorMailboxError (message: string) : string =
        if message.StartsWith("JOIN_IN_PROGRESS", StringComparison.Ordinal) then
            "join-in-progress"
        else
            "not-ready"
