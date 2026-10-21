namespace Wanxiangshu.Execution.Delegation.Fork

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
    val DevOpsJoinTimeoutMs: int

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
    val decideRoute: role: Role option -> JoinRoute

    /// Whether this role's join carries a wait budget.
    val appliesWaitBudget: role: Role option -> bool

    /// The interrupt reason a join reports. A lapsed budget is the only
    /// reason that differs from the one the wait produced.
    val normalizeInterruptReason: reason: JoinInterruptReason -> JoinInterruptReason

    /// The lane a join drains from: a process-local fission membership, else
    /// the owner's own lane.
    val decideLane: fissionMembership: (string * int) option -> JoinLane

    /// The agent name: the durable byname first, the live runtime table second.
    /// A restarted process may only know the child from its durable handle.
    val resolveAgentName: identity: AgentIdentity -> agentId: string -> string

    /// The agent role: the payload's own role first, then the durable role,
    /// then the runtime role, then nothing.
    val resolveAgentRole: identity: AgentIdentity -> agentId: string -> fallback: Role option -> string

    /// The PTY label: the running command when it has one, else the terminal's name.
    val resolveTerminalLabel: command: string option -> terminalName: string option -> string

    /// Split a rendered batch into the items it delivered and the items left over.
    val windowOf: batch: NonEmptyBatch<JoinItem> -> remaining: JoinItem list -> BatchWindow

    /// The PTY tracks the delivered items hold; agent items carry none.
    val ptyTrackIds: items: JoinItem list -> string list

    /// The orchestrator mailbox failure → the stable wire code.
    val mapOrchestratorMailboxError: message: string -> string
