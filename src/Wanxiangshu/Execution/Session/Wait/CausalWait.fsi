namespace Wanxiangshu.Execution.Session.Wait

open System

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
