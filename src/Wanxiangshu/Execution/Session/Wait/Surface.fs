namespace Wanxiangshu.Execution.Session.Wait

open System
open System.Collections.Generic
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Foundation

/// JS-native boundary for causal-wait diagnostics. Descriptors and snapshots are
/// plain objects; registry and lease values are opaque process-local capabilities.
module CausalWaitSurface =

    type private RegistryHandle(registry: CausalWaitRegistry) =
        member _.Registry = registry

    type private LeaseHandle(lease: IWaitLease) =
        member _.Lease = lease

    [<Emit("$0($1)")>]
    let private call1 (fn: obj) (value: obj) : obj = jsNative

    [<Emit("$0()")>]
    let private call0 (fn: obj) : obj = jsNative

    [<Emit("Promise.resolve($0)")>]
    let private asPromise (value: obj) : JS.Promise<obj> = jsNative

    let private property (value: obj) (name: string) : obj = emitJsExpr (value, name) "$0[$1]"

    let private optionObj (value: 'T option) : obj =
        match value with
        | Some value -> box value
        | None -> null

    [<Emit("$0==null")>]
    let private isNullish (value: obj) : bool = jsNative

    let private stringOf (value: obj) =
        if isNullish value then "" else string value

    let private pairsOf (value: obj) : (string * string) list =
        if isNullish value then
            []
        else
            let keys: string array = emitJsExpr value "Object.keys($0)"
            keys |> Array.toList |> List.map (fun key -> key, stringOf (property value key))

    let private objectOfPairs (pairs: (string * string) list) : obj =
        pairs |> List.map (fun (key, value) -> key, box value) |> createObj

    let private ownerOf (value: obj) : CausalOwnerRef =
        { Kind = stringOf (property value "kind")
          Identity = pairsOf (property value "identity") }

    let private ownerObject (owner: CausalOwnerRef) : obj =
        box {| kind = owner.Kind; identity = objectOfPairs owner.Identity |}

    let private producerOf (value: obj) : CausalProducerRef =
        let kind = stringOf (property value "kind")

        if kind = "workflow" then
            WorkflowProducer(ownerOf (property value "owner"))
        else
            ExternalProducer(stringOf (property value "producerKind"), pairsOf (property value "identity"))

    let private producerObject (producer: CausalProducerRef) : obj =
        match producer with
        | WorkflowProducer owner -> box {| kind = "workflow"; owner = ownerObject owner |}
        | ExternalProducer(kind, identity) ->
            box {| kind = "external"; producerKind = kind; identity = objectOfPairs identity |}

    let private escapeOf (value: obj) : WaitEscape =
        match stringOf (property value "kind") with
        | "deadlineAt" -> DeadlineAt(DateTimeOffset.Parse(stringOf (property value "at")))
        | "cancelledBy" -> CancelledBy(ownerOf (property value "owner"))
        | "processLifetime" -> ProcessLifetime
        | "sessionLifetime" -> SessionLifetime
        | "openEndedExternal" -> OpenEndedExternal
        | unknown -> invalidArg "escape" ("unknown wait escape: " + unknown)

    let private escapeObject (escape: WaitEscape) : obj =
        match escape with
        | DeadlineAt at -> box {| tag = "deadlineAt"; at = at.ToString("o") |}
        | CancelledBy owner -> box {| tag = "cancelledBy"; owner = ownerObject owner |}
        | ProcessLifetime -> box {| tag = "processLifetime" |}
        | SessionLifetime -> box {| tag = "sessionLifetime" |}
        | OpenEndedExternal -> box {| tag = "openEndedExternal" |}

    let private waitOf (value: obj) : DiagnosticWait =
        let subject = pairsOf (property value "subject")
        let escapes =
            let rawEscapes = property value "escapes"
            if isNullish rawEscapes then
                []
            else
                unbox<obj array> rawEscapes |> Array.toList |> List.map escapeOf

        DiagnosticWait.create
            (stringOf (property value "waitKind"))
            (ownerOf (property value "owner"))
            subject
            (producerOf (property value "producer"))
            escapes
            (stringOf (property value "source"))

    let private waitObject (wait: DiagnosticWait) : obj =
        box
            {| waitKind = wait.WaitKind
               owner = ownerObject wait.Owner
               subject = objectOfPairs wait.Subject
               producer = producerObject wait.Producer
               escapes = wait.Escapes |> List.map escapeObject |> List.toArray
               source = wait.Source |}

    let private exitOf (name: string) =
        match name with
        | "WaitResolved"
        | "resolved" -> DiagnosticWaitExit.WaitResolved
        | "WaitFailed"
        | "failed" -> DiagnosticWaitExit.WaitFailed
        | "WaitCancelled"
        | "cancelled" -> DiagnosticWaitExit.WaitCancelled
        | "WaitTimedOut"
        | "timedOut" -> DiagnosticWaitExit.WaitTimedOut
        | "WaitDisposed"
        | "disposed" -> DiagnosticWaitExit.WaitDisposed
        | unknown -> invalidArg "exit" ("unknown wait exit: " + unknown)

    let private exitName =
        function
        | DiagnosticWaitExit.WaitResolved -> "WaitResolved"
        | DiagnosticWaitExit.WaitFailed -> "WaitFailed"
        | DiagnosticWaitExit.WaitCancelled -> "WaitCancelled"
        | DiagnosticWaitExit.WaitTimedOut -> "WaitTimedOut"
        | DiagnosticWaitExit.WaitDisposed -> "WaitDisposed"

    let private transitionObject (transition: WaitTransition) : obj =
        box
            {| sequence = transition.Sequence
               kind =
                   match transition.Kind with
                   | WaitTransitionKind.Entered -> "Entered"
                   | WaitTransitionKind.Left -> "Left"
               wait = waitObject transition.Wait
               exit = optionObj (transition.Exit |> Option.map exitName) |}

    let private snapshotObject (snapshot: DiagnosticWaitSnapshot) : obj =
        box
            {| active = snapshot.Active |> List.map waitObject |> List.toArray
               history = snapshot.History |> List.map transitionObject |> List.toArray
               sequence = int snapshot.Sequence |}

    let private frontierKindName =
        function
        | ExternalProducerFrontier -> "ExternalProducerFrontier"
        | BrokenCausalEdge -> "BrokenCausalEdge"
        | ProducerRunningWithoutWait -> "ProducerRunningWithoutWait"
        | CausalWaitCycle -> "CausalWaitCycle"
        | Empty -> "Empty"

    let private frontierObject (frontier: CausalFrontier) : obj =
        let chain =
            frontier.Chain
            |> List.map (fun node ->
                box
                    {| owner = ownerObject node.Owner
                       waitKind = optionObj (node.Wait |> Option.map (fun wait -> wait.WaitKind)) |})
            |> List.toArray

        box
            {| kind = frontierKindName frontier.Kind
               detail = frontier.Detail
               chain = chain
               cycle = frontier.Cycle |> List.map ownerObject |> List.toArray
               producer = optionObj (frontier.FrontierProducer |> Option.map producerObject) |}

    let createRegistry (historyCapacity: obj) : obj =
        let capacity =
            if isNullish historyCapacity then None else Some(int (string historyCapacity))

        RegistryHandle(CausalWaitRegistry(?historyCapacity = capacity)) :> obj

    let createWait (descriptor: obj) : obj = descriptor

    let owner (kind: string) (identity: obj) : obj =
        box {| kind = kind; identity = objectOfPairs (pairsOf identity) |}

    let externalProducer (kind: string) (identity: obj) : obj =
        box {| kind = "external"; producerKind = kind; identity = objectOfPairs (pairsOf identity) |}

    let workflowProducer (owner: obj) : obj = box {| kind = "workflow"; owner = owner |}

    let escape (kind: string) (value: obj) : obj =
        match kind with
        | "deadlineAt" -> box {| kind = kind; at = stringOf value |}
        | "cancelledBy" -> box {| kind = kind; owner = value |}
        | _ -> box {| kind = kind |}

    let enter (registry: obj) (descriptor: obj) : obj =
        let handle = registry :?> RegistryHandle
        LeaseHandle((handle.Registry :> IWaitObserver).Enter(waitOf descriptor)) :> obj

    let markExit (lease: obj) (exit: string) : unit =
        (lease :?> LeaseHandle).Lease.MarkExit(exitOf exit)

    let dispose (lease: obj) : unit =
        (lease :?> LeaseHandle).Lease.Dispose()

    let snapshot (registry: obj) : obj =
        let handle = registry :?> RegistryHandle
        snapshotObject ((handle.Registry :> IWaitSnapshotReader).Snapshot())

    let historyCapacity (registry: obj) : int =
        (registry :?> RegistryHandle).Registry.HistoryCapacity

    let ownerKey (value: obj) : string = CausalOwner.key (ownerOf value)

    let producerKey (value: obj) : string = CausalProducer.key (producerOf value)

    let frontiers (active: obj array) : obj array =
        let waits = active |> Array.toList |> List.map waitOf

        let snapshot =
            { Active = waits
              History = []
              Sequence = 1L }

        CausalFrontier.ofSnapshot snapshot |> List.map frontierObject |> List.toArray

    let frontiersOfSnapshot (snapshot: obj) : obj array =
        let waits =
            unbox<obj array> (property snapshot "active")
            |> Array.toList
            |> List.map waitOf

        let typed =
            { Active = waits
              History = []
              Sequence = 1L }

        CausalFrontier.ofSnapshot typed |> List.map frontierObject |> List.toArray

    let awaitTask (registry: obj) (descriptor: obj) (pending: obj) : Task<obj> =
        let handle = registry :?> RegistryHandle
        let task: Task<obj> = unbox (asPromise pending)
        CausalAwait.awaitTask (handle.Registry :> IWaitObserver) (waitOf descriptor) task

    let untilSignalOrDeadline
        (registry: obj)
        (descriptor: obj)
        (deadline: obj)
        (tryRead: obj)
        (awaitSignal: obj)
        : Task<obj> =
        let handle = registry :?> RegistryHandle
        let deadlineHandle = unbox<IDeadlineHandle> deadline
        let read () =
            let value = call0 tryRead
            if isNullish value then None else Some value
        let signal () : Task<unit> = unbox (asPromise (call0 awaitSignal))

        task {
            let! result =
                CausalAwait.untilSignalOrDeadline
                    (handle.Registry :> IWaitObserver)
                    (waitOf descriptor)
                    deadlineHandle
                    read
                    signal

            match result with
            | Ok value -> return box {| ok = true; value = value |}
            | Error exit -> return box {| ok = false; reason = exitName exit |}
        }

    let writeSnapshot (workspace: string) (registry: obj) : unit =
        let handle = registry :?> RegistryHandle
        CausalWaitBridge.writeSnapshot workspace (handle.Registry :> IWaitSnapshotReader)

    let hubSetWorkspace (workspace: obj) : unit =
        let value = if isNullish workspace then None else Some(string workspace)
        CausalWaitHub.setWorkspace value

    let hubEnter (descriptor: obj) : obj =
        LeaseHandle(CausalWaitHub.observer.Enter(waitOf descriptor)) :> obj

    let hubSnapshot () : obj = snapshotObject (CausalWaitHub.snapshot ())

    let hubWriteToWorkspace () : unit = CausalWaitHub.writeToWorkspace ()

    let observerHasSnapshot () = false

    let readerHasSnapshot () = true
