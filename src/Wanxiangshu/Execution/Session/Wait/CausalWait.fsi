namespace Wanxiangshu.Execution.Session.Wait

open System
open System.Threading.Tasks

module JoinBatch =
    val Max: int
    val MaxJoinBatch: int

type NonEmptyBatch<'item> = private NonEmptyBatch of head: 'item * tail: 'item list

module NonEmptyBatch =
    val ofHeadTail: head: 'item -> tail: 'item list -> NonEmptyBatch<'item>
    val tryOfList: ('item list -> NonEmptyBatch<'item> option)
    val toList: NonEmptyBatch<'item> -> 'item list
    val length: NonEmptyBatch<'item> -> int
    val map: f: ('a -> 'b) -> NonEmptyBatch<'a> -> NonEmptyBatch<'b>

[<RequireQualifiedAccess>]
type JoinInterruptReason =
    | OperatorAbort
    | UserMessageArrived
    | DeadlineExpired

type JoinWaitOutcome<'item> =
    | ResultsAvailable of NonEmptyBatch<'item>
    | Interrupted of JoinInterruptReason

type MailboxWakeReason =
    | CompletionMayBeAvailable
    | LocalInterrupt of JoinInterruptReason
    | MailboxCancelled

type JoinInterrupt =
    { Wait: Task<JoinInterruptReason>
      Signal: JoinInterruptReason -> unit }

type ICompletionMailbox<'agent, 'pty, 'interrupt, 'wake> =
    abstract PulseAgent: 'agent -> unit
    abstract PublishPty: 'pty -> unit
    abstract PulseWake: unit -> unit
    abstract WaitForWake: unit -> Task<'wake>
    abstract WaitForSignal: Task<'interrupt> -> Task<'wake>
    abstract DrainAgents: int -> 'agent list
    abstract DrainPtys: int -> 'pty list
    abstract Cancel: unit -> bool
    abstract PendingCount: int
    abstract PendingPtyCount: int
    abstract IsCancelled: bool

type CausalOwnerRef =
    { Kind: string
      Identity: (string * string) list }

type CausalProducerRef =
    | WorkflowProducer of CausalOwnerRef
    | ExternalProducer of kind: string * identity: (string * string) list

type WaitEscape =
    | DeadlineAt of DateTimeOffset
    | CancelledBy of CausalOwnerRef
    | ProcessLifetime
    | SessionLifetime
    | OpenEndedExternal

type DiagnosticWait =
    { WaitKind: string
      Owner: CausalOwnerRef
      Subject: (string * string) list
      Producer: CausalProducerRef
      Escapes: WaitEscape list
      Source: string }

type DiagnosticWaitExit =
    | WaitResolved
    | WaitFailed
    | WaitCancelled
    | WaitTimedOut
    | WaitDisposed

type WaitTransitionKind =
    | Entered
    | Left

type WaitTransition =
    { Sequence: int64
      Kind: WaitTransitionKind
      Wait: DiagnosticWait
      Exit: DiagnosticWaitExit option }

type DiagnosticWaitSnapshot =
    { Active: DiagnosticWait list
      History: WaitTransition list
      Sequence: int64 }

type IWaitLease =
    inherit IDisposable
    abstract MarkExit: DiagnosticWaitExit -> unit

type IWaitObserver =
    abstract Enter: DiagnosticWait -> IWaitLease

type IWaitSnapshotReader =
    abstract Snapshot: unit -> DiagnosticWaitSnapshot

type IWaitDiagnosticSink =
    abstract Publish: DiagnosticWaitSnapshot -> unit

module CausalOwner =
    val key: owner: CausalOwnerRef -> string
    val create: kind: string -> identity: (string * string) list -> CausalOwnerRef

module CausalProducer =
    val key: producer: CausalProducerRef -> string
    val asOwner: producer: CausalProducerRef -> CausalOwnerRef option

module DiagnosticWait =
    val create:
        waitKind: string ->
        owner: CausalOwnerRef ->
        subject: (string * string) list ->
        producer: CausalProducerRef ->
        escapes: WaitEscape list ->
        source: string ->
            DiagnosticWait

type CausalFrontierKind =
    | ExternalProducerFrontier
    | BrokenCausalEdge
    | ProducerRunningWithoutWait
    | CausalWaitCycle
    | Empty

type CausalFrontierNode =
    { Owner: CausalOwnerRef
      Wait: DiagnosticWait option }

type CausalFrontier =
    { Kind: CausalFrontierKind
      Chain: CausalFrontierNode list
      FrontierProducer: CausalProducerRef option
      Cycle: CausalOwnerRef list
      Detail: string }

module CausalFrontier =
    val ofSnapshot: snapshot: DiagnosticWaitSnapshot -> CausalFrontier list
