namespace Wanxiangshu.Execution.Session.Wait

open Wanxiangshu.Foundation

open System

/// DSL-012: process-local, non-authoritative causal wait observations.
///
/// Wait observation may describe a suspended CE; it must never decide a branch,
/// mint a permit, write Journal, recover, dedupe, or influence PromptAuthority /
/// Finality / Reviewer / Manager decisions.
///
/// WaitKind / Subject strings exist only for diagnostics render — they are not
/// Domain vocabulary.
///
/// Exit case names are prefixed (`Wait*`) so they do not collide with
/// TerminalOutcome.Failed / Cancelled in Session modules that open Kernel.

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

/// Lease returned by Enter. MarkExit records the diagnostic leave reason;
/// Dispose always clears the active wait (defaults to WaitDisposed if unmarked).
type IWaitLease =
    inherit IDisposable
    abstract MarkExit: DiagnosticWaitExit -> unit

/// Application workflows may enter waits; they must never read the snapshot.
type IWaitObserver =
    abstract Enter: DiagnosticWait -> IWaitLease

/// Diagnostics surfaces may read; Application must not hold this interface.
type IWaitSnapshotReader =
    abstract Snapshot: unit -> DiagnosticWaitSnapshot

module CausalOwner =

    let key (owner: CausalOwnerRef) : string =
        let ids =
            owner.Identity
            |> List.sortBy fst
            |> List.map (fun (k, v) -> k + "=" + v)
            |> String.concat ","

        owner.Kind + ":" + ids

    let create (kind: string) (identity: (string * string) list) : CausalOwnerRef = { Kind = kind; Identity = identity }

module CausalProducer =

    let key (producer: CausalProducerRef) : string =
        match producer with
        | WorkflowProducer owner -> "workflow:" + CausalOwner.key owner
        | ExternalProducer(kind, identity) -> "external:" + CausalOwner.key { Kind = kind; Identity = identity }

    let asOwner (producer: CausalProducerRef) : CausalOwnerRef option =
        match producer with
        | WorkflowProducer owner -> Some owner
        | ExternalProducer _ -> None

module DiagnosticWait =

    let create
        (waitKind: string)
        (owner: CausalOwnerRef)
        (subject: (string * string) list)
        (producer: CausalProducerRef)
        (escapes: WaitEscape list)
        (source: string)
        : DiagnosticWait =
        { WaitKind = waitKind
          Owner = owner
          Subject = subject
          Producer = producer
          Escapes = escapes
          Source = source }

/// Frontier outcomes are diagnostic-only explanations of the minimal unsatisfied
/// causal edge. They must never drive workflow control flow.
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

    let private ownerKey = CausalOwner.key

    let private waitsByOwner (active: DiagnosticWait list) =
        active |> List.groupBy (fun wait -> ownerKey wait.Owner) |> Map.ofList

    let private producerOwners (active: DiagnosticWait list) =
        active
        |> List.choose (fun wait -> CausalProducer.asOwner wait.Producer)
        |> List.map ownerKey
        |> Set.ofList

    let private rootOwners (active: DiagnosticWait list) =
        let produced = producerOwners active

        active
        |> List.map (fun wait -> wait.Owner)
        |> List.distinctBy ownerKey
        |> List.filter (fun owner -> not (Set.contains (ownerKey owner) produced))

    let private walk (byOwner: Map<string, DiagnosticWait list>) (start: CausalOwnerRef) : CausalFrontier =
        let rec go (owner: CausalOwnerRef) (chain: CausalFrontierNode list) (seen: Set<string>) =
            let key = ownerKey owner

            if Set.contains key seen then
                let cycle =
                    chain
                    |> List.map (fun node -> node.Owner)
                    |> List.skipWhile (fun o -> ownerKey o <> key)
                    |> fun prefix -> prefix @ [ owner ]

                { Kind = CausalWaitCycle
                  Chain = List.rev chain
                  FrontierProducer = None
                  Cycle = cycle
                  Detail = "CAUSAL WAIT CYCLE" }
            else
                resolveOwner byOwner go owner chain seen

        and resolveOwner byOwner continueWalk owner chain seen =
            let key = ownerKey owner

            match Map.tryFind key byOwner with
            | None ->
                { Kind = BrokenCausalEdge
                  Chain = List.rev ({ Owner = owner; Wait = None } :: chain)
                  FrontierProducer = None
                  Cycle = []
                  Detail =
                    "BROKEN CAUSAL EDGE: consumer waits for "
                    + key
                    + " but no active wait declares that owner" }
            | Some [] ->
                { Kind = ProducerRunningWithoutWait
                  Chain = List.rev ({ Owner = owner; Wait = None } :: chain)
                  FrontierProducer = None
                  Cycle = []
                  Detail = "PRODUCER RUNNING WITHOUT DECLARED WAIT: " + key }
            | Some(wait :: _) ->
                let node = { Owner = owner; Wait = Some wait }
                let nextChain = node :: chain
                let nextSeen = Set.add key seen
                resolveProducer byOwner continueWalk key wait nextChain nextSeen

        and resolveProducer byOwner continueWalk key wait nextChain nextSeen =
            match wait.Producer with
            | ExternalProducer _ as producer ->
                { Kind = ExternalProducerFrontier
                  Chain = List.rev nextChain
                  FrontierProducer = Some producer
                  Cycle = []
                  Detail = "FRONTIER: waiting for external producer " + CausalProducer.key producer }
            | WorkflowProducer next -> resolveWorkflow byOwner continueWalk key wait next nextChain nextSeen

        and resolveWorkflow byOwner continueWalk key wait next nextChain nextSeen =
            match Map.tryFind (ownerKey next) byOwner with
            | None ->
                { Kind = BrokenCausalEdge
                  Chain = List.rev ({ Owner = next; Wait = None } :: nextChain)
                  FrontierProducer = Some wait.Producer
                  Cycle = []
                  Detail =
                    "BROKEN CAUSAL EDGE: "
                    + key
                    + " waits for "
                    + ownerKey next
                    + " but no active wait exists for that producer" }
            | Some _ -> continueWalk next nextChain nextSeen

        go start [] Set.empty

    let private startsForSnapshot (active: DiagnosticWait list) (roots: CausalOwnerRef list) : CausalOwnerRef list =
        if List.isEmpty roots then
            active |> List.map (fun wait -> wait.Owner) |> List.distinctBy ownerKey
        else
            roots

    /// Pure diagnostic algorithm: from living root owners, follow consumer→producer
    /// edges until the first unexplained frontier.
    let ofSnapshot (snapshot: DiagnosticWaitSnapshot) : CausalFrontier list =
        match snapshot.Active with
        | [] ->
            [ { Kind = Empty
                Chain = []
                FrontierProducer = None
                Cycle = []
                Detail = "no active waits" } ]
        | active ->
            let byOwner = waitsByOwner active
            let starts = startsForSnapshot active (rootOwners active)
            starts |> List.map (fun root -> walk byOwner root)
