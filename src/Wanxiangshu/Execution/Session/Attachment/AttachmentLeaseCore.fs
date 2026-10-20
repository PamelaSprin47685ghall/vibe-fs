namespace Wanxiangshu.Execution.Session.Attachment

open System
open System.Collections.Generic
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open FsToolkit.ErrorHandling
open Wanxiangshu.Execution.Session
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity

/// The ONE lifecycle mechanism for every Attached session (managed-session-lifecycle
/// [001]). Each AttachmentKind supplies only its parameters — agent, title, restore
/// hint — and this module performs creation, recovery, registration, single-flight
/// and release for all of them.
///
/// It deliberately depends on nothing from the OpenCode layer: each caller supplies
/// its own child operations as data. That is what lets Companion, SyncDelegate and
/// the read-only replica share a single owner instead of each growing its own.
[<RequireQualifiedAccess>]
module AttachmentLeaseCore =

    /// A child as the Host reports it. Only the fields the lease decision needs.
    type Child =
        { SessionId: string
          Agent: string option
          Title: string option }

    /// What the caller must supply for its own layer. No OpenCode types.
    type Operations =
        { ListChildren: string -> Task<Result<Child list, string>>
          CreateChild: string -> string -> string option -> Task<Result<string, string>>
          AbortChild: string -> Task<Result<unit, string>>
          Link: string -> string -> string -> Task<Result<unit, string>>
          Close: string -> Task<Result<unit, string>> }

    /// Every kind's parameters. This is the whole of what a kind may differ in.
    type Spec =
        {
            Kind: AttachmentKind
            Agent: string
            Title: string
            Directory: string option
            /// The child this kind's durable state says it already has, if any.
            RestoreHint: string option
        }

    type Origin =
        | Created
        | Reused
        | Replacement

    type Lease =
        {
            SessionId: string
            Origin: Origin
            /// The agent this child was established for. A reuse answers with the
            /// agent bound at create time; a later request with a different agent
            /// must not overwrite it, so the lease carries it rather than the caller.
            Agent: string
        }

    /// Stable registry key for a kind. Structural equality on F# unions would work,
    /// but a total string projection keeps the registry independent of how the
    /// union happens to be compiled and makes the key observable in diagnostics.
    let kindKey (kind: AttachmentKind) =
        match kind with
        | AttachmentKind.Companion -> "companion"
        | AttachmentKind.SyncInspector -> "sync-inspector"
        | AttachmentKind.SyncCoder -> "sync-coder"
        | AttachmentKind.SyncEngineer -> "sync-engineer"
        | AttachmentKind.SyncDevOps -> "sync-devops"
        | AttachmentKind.Bookkeeper transactionId -> "bookkeeper\u001f" + transactionId


    let leaseKey (owner: SessionId) (kind: AttachmentKind) =
        SessionId.value owner + "\u001f" + kindKey kind

    /// CUT-12 sub-loop 3: which child the kind's lease uses (reuse / conflict /
    /// replacement / ambiguity / create) and whether a fresh child is aborted on a
    /// failed close or link have one owner, wanxiangli/attachment-lease. This module
    /// lists the children, performs the operations and maps the verdict back.
    [<Import("AttachmentLeaseLaw", "wanxiangli/attachment-lease")>]
    let private lawClass: obj = jsNative

    [<Emit("new $0()")>]
    let private construct (cls: obj) : obj = jsNative

    let private law: obj = construct lawClass

    let private optionalText (value: string option) : obj =
        match value with
        | Some text -> box text
        | None -> null

    let private originName (origin: Origin) =
        match origin with
        | Created -> "Created"
        | Reused -> "Reused"
        | Replacement -> "Replacement"

    /// Resolve which child this kind should use. Never guesses: the library decides,
    /// this side creates the child a `create` verdict asks for.
    let private resolve
        (operations: Operations)
        (owner: SessionId)
        (spec: Spec)
        (children: Child list)
        : Task<Result<Lease * bool, string>> =
        let ownerText = SessionId.value owner

        let create origin closeReplacedBeforeLink =
            task {
                match! operations.CreateChild ownerText spec.Agent spec.Directory with
                | Ok child ->
                    return
                        Ok(
                            { SessionId = child
                              Origin = origin
                              Agent = spec.Agent },
                            closeReplacedBeforeLink
                        )
                | Error error -> return Error error
            }

        let verdict: obj =
            law?resolve (
                createObj
                    [ "restoreHint" ==> optionalText spec.RestoreHint
                      "agent" ==> spec.Agent
                      "title" ==> spec.Title
                      "children"
                      ==> (children
                           |> List.map (fun child ->
                               createObj
                                   [ "sessionId" ==> child.SessionId
                                     "agent" ==> optionalText child.Agent
                                     "title" ==> optionalText child.Title ])
                           |> List.toArray) ]
            )

        match unbox<string> verdict?verdict with
        | "reuse" ->
            Task.FromResult(
                Ok(
                    { SessionId = unbox<string> verdict?child
                      Origin = Reused
                      Agent = spec.Agent },
                    false
                )
            )
        | "create" ->
            let closeFirst = unbox<bool> verdict?closeReplacedBeforeLink

            match unbox<string> verdict?origin with
            | "Created" -> create Created closeFirst
            | "Replacement" -> create Replacement closeFirst
            | other -> invalidOp ("wanxiangli/attachment-lease returned an unknown origin: " + other)
        | "reject" ->
            let hash = unbox<string> verdict?child

            match unbox<string> verdict?rejection with
            | "Conflicting" ->
                Task.FromResult(
                    Error(
                        sprintf
                            "Conflicting %s recovery for %s: child %s has a different agent or title"
                            (kindKey spec.Kind)
                            ownerText
                            hash
                    )
                )
            | "Ambiguous" ->
                Task.FromResult(
                    Error(
                        sprintf "Ambiguous %s recovery for %s: duplicate child id %s" (kindKey spec.Kind) ownerText hash
                    )
                )
            | other -> invalidOp ("wanxiangli/attachment-lease returned an unknown rejection: " + other)
        | other -> invalidOp ("wanxiangli/attachment-lease returned an unknown verdict: " + other)

    /// A lease that was created or replaced must not survive a failed link: the
    /// caller's durable association is what makes a child real. Which origins are
    /// fresh is the library's answer.
    let private abortFresh (operations: Operations) (lease: Lease) =
        task {
            if unbox<bool> (law?abortOnFailure (originName lease.Origin)) then
                let! _ = operations.AbortChild lease.SessionId
                ()
        }

    let private keepFreshOnlyOnSuccess (operations: Operations) (lease: Lease) operation =
        task {
            match! operation with
            | Ok() -> return Ok()
            | Error error ->
                do! abortFresh operations lease
                return Error error
        }

    /// Establish (or re-establish) the kind's lease and publish its association.
    let ensure (operations: Operations) (owner: SessionId) (spec: Spec) : Task<Result<Lease, string>> =
        taskResult {
            // A replacement is a real transition, not a repoint: close the vanished
            // association before establishing the new one.
            let closeReplaced (lease: Lease) closeFirst =
                if closeFirst then
                    operations.Close(SessionId.value owner)
                    |> keepFreshOnlyOnSuccess operations lease
                else
                    Task.FromResult(Ok())

            let! children = operations.ListChildren(SessionId.value owner)
            let! lease, closeFirst = resolve operations owner spec children
            do! closeReplaced lease closeFirst

            do!
                operations.Link (SessionId.value owner) lease.SessionId spec.Agent
                |> keepFreshOnlyOnSuccess operations lease

            return lease
        }

    /// Release the kind's lease: the child ends and the association closes. Used
    /// when the owner ends, and by kinds whose lifecycle ends with the call.
    let retire (operations: Operations) (owner: SessionId) : Task<Result<unit, string>> =
        operations.Close(SessionId.value owner)

/// The single lifecycle owner's registry: which child each `(owner, AttachmentKind)`
/// currently holds, and the in-flight ensure so concurrent first requests share one
/// creation instead of racing into two children.
type AttachmentLeaseRegistry() =
    let gate = obj ()
    // DSL-MUTABLE: resource — attached child identity by owner+kind
    let bindings = Dictionary<string, AttachmentLeaseCore.Lease>()
    // DSL-MUTABLE: resource — in-flight ensure by owner+kind
    let flights = Dictionary<string, Task<Result<AttachmentLeaseCore.Lease, string>>>()

    /// The kinds whose key is fully described by its rendered fragment. Bookkeeper
    /// carries a transaction id in a further fragment, so it is parsed separately.
    let fragmentKinds =
        [ AttachmentKind.Companion
          AttachmentKind.SyncInspector
          AttachmentKind.SyncCoder
          AttachmentKind.SyncEngineer
          AttachmentKind.SyncDevOps ]


    /// Re-parse one registry key back into `(owner, kind)`. The registry keeps only
    /// the key, so a reverse lookup recovers the kind from the key's own rendering
    /// instead of holding a second copy of every binding.
    let parseKey (key: string) =
        let parts = key.Split('\u001f')

        if parts.Length < 2 then
            None
        elif parts.Length >= 3 && parts.[1] = "bookkeeper" then
            Some(SessionId.create parts.[0], AttachmentKind.Bookkeeper parts.[2])
        else
            let rendered = parts.[1]

            fragmentKinds
            |> List.tryFind (fun candidate -> AttachmentLeaseCore.kindKey candidate = rendered)
            |> Option.map (fun kind -> SessionId.create parts.[0], kind)

    member _.Bind(owner: SessionId, kind: AttachmentKind, lease: AttachmentLeaseCore.Lease) =
        lock gate (fun () -> bindings.[AttachmentLeaseCore.leaseKey owner kind] <- lease)

    member _.TryFind(owner: SessionId, kind: AttachmentKind) : AttachmentLeaseCore.Lease option =
        lock gate (fun () ->
            match bindings.TryGetValue(AttachmentLeaseCore.leaseKey owner kind) with
            | true, lease -> Some lease
            | false, _ -> None)

    member _.Remove(owner: SessionId, kind: AttachmentKind) : bool =
        lock gate (fun () -> bindings.Remove(AttachmentLeaseCore.leaseKey owner kind))

    /// One kind's lease for one owner. This is the axis the Sync family resolves
    /// its `ReuseScopeId` lookup on: `ReuseScope.ofSession` is the identity wrap of
    /// a SessionId value, so an owner IS its own scope and the scope axis is the
    /// owner axis (see `AttachmentPort`).
    member this.TryFindScoped(owner: SessionId, kind: AttachmentKind) : AttachmentLeaseCore.Lease option =
        this.TryFind(owner, kind)

    /// Reverse lookup: which `(owner, kind)` currently holds this child. Used by
    /// callers that are handed a child id and must find its owner (Sync's
    /// scope-close and deleted-delegate paths).
    member _.TryFindByChild(child: SessionId) : (SessionId * AttachmentKind) list =
        lock gate (fun () ->
            bindings
            |> Seq.choose (fun entry ->
                if entry.Value.SessionId <> SessionId.value child then
                    None
                else
                    parseKey entry.Key)
            |> Seq.toList)

    /// Every binding, for bulk reconciliation on owner/cascade teardown.
    member _.Snapshot() : (SessionId * AttachmentKind) list =
        lock gate (fun () -> bindings |> Seq.choose (fun entry -> parseKey entry.Key) |> Seq.toList)

    /// The child itself is gone: drop whichever binding held it, so no owner keeps a
    /// dead lease.
    member _.RemoveByChild(child: SessionId) : (SessionId * AttachmentKind) list =
        lock gate (fun () ->
            let doomed =
                bindings
                |> Seq.filter (fun entry -> entry.Value.SessionId = SessionId.value child)
                |> Seq.toList

            doomed |> List.iter (fun entry -> bindings.Remove entry.Key |> ignore)

            doomed |> List.choose (fun entry -> parseKey entry.Key))

    /// Share one in-flight ensure per `(owner, kind)`.
    member _.Ensure
        (owner: SessionId, kind: AttachmentKind, start: unit -> Task<Result<AttachmentLeaseCore.Lease, string>>)
        : Task<Result<AttachmentLeaseCore.Lease, string>> =
        lock gate (fun () ->
            let key = AttachmentLeaseCore.leaseKey owner kind

            match flights.TryGetValue key with
            | true, inFlight -> inFlight
            | false, _ ->
                let inFlight = start ()
                flights.[key] <- inFlight
                inFlight)

    member _.FinishEnsure(owner: SessionId, kind: AttachmentKind) =
        lock gate (fun () -> flights.Remove(AttachmentLeaseCore.leaseKey owner kind) |> ignore)

    member _.Clear() =
        lock gate (fun () ->
            bindings.Clear()
            flights.Clear())
