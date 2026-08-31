namespace Wanxiangshu.Execution.Session.Attachment

open System
open System.Collections.Generic
open System.Threading.Tasks
open FsToolkit.ErrorHandling
open Wanxiangshu.Execution.Session
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.OpenCode

/// HOST-014: one recovery and ownership mechanism for Companion leaf satellites.
/// The runtime owns only physical Session lifecycle; semantic state remains in
/// the caller's durable projection (Companion).
type SatelliteOrigin =
    | Created
    | Reused
    | Replacement

/// DSL-class: PhysicalHandle — HOST-014 session lease identity and observed allocation origin; owner SatelliteRuntime, law HOST-014, proof satellite-runtime.
type SatelliteLease =
    { SessionId: SessionId
      Origin: SatelliteOrigin }

/// DSL-class: PhysicalHandle — HOST-014 injected session lifecycle ports and physical launch coordinates; owner SatelliteRuntime, law HOST-014, proof satellite-runtime.
type SatelliteSpec =
    { Kind: SatelliteKind
      Agent: string
      Title: string
      Directory: string option
      RestoredSessionId: SessionId option
      Link: SessionId -> SessionId -> string -> Task<Result<unit, string>>
      Close: SessionId -> Task<Result<unit, string>> }

module private SatelliteLeaseFlow =
    let kindLabel =
        function
        | SatelliteKind.Companion -> "companion"

    let recoverError kind error =
        sprintf "Cannot recover %s satellite: %s" (kindLabel kind) error

    let exactCandidate (spec: SatelliteSpec) (child: OpenCodeChildInfo) =
        child.Agent = Some spec.Agent && child.Title = Some spec.Title

    let listOwnerChildren (sessions: ISessionHostPort) rootId owner =
        if rootId = owner then
            Task.FromResult(Ok [])
        else
            sessions.ListChildren owner

    let createChild (sessions: ISessionHostPort) owner (spec: SatelliteSpec) origin =
        taskResult {
            let! child =
                sessions.CreateChildSession(
                    owner,
                    { Title = Some spec.Title
                      Agent = Some spec.Agent
                      Directory = spec.Directory }
                )

            return { SessionId = child; Origin = origin }
        }

    let replacementOrConflict
        (sessions: ISessionHostPort)
        owner
        (spec: SatelliteSpec)
        (merged: OpenCodeChildInfo list)
        restored
        =
        match merged |> List.filter (fun child -> child.SessionId = restored) with
        | [] -> createChild sessions owner spec Replacement
        | _ ->
            Task.FromResult(
                Error(
                    sprintf
                        "Conflicting %s satellite recovery for %s: journal-linked Host child has different agent/title"
                        (kindLabel spec.Kind)
                        (SessionId.value owner)
                )
            )

    let resolveRestored
        (sessions: ISessionHostPort)
        owner
        (spec: SatelliteSpec)
        (merged: OpenCodeChildInfo list)
        (candidates: OpenCodeChildInfo list)
        restored
        =
        match candidates |> List.filter (fun child -> child.SessionId = restored) with
        | [ child ] ->
            Task.FromResult(
                Ok
                    { SessionId = child.SessionId
                      Origin = Reused }
            )
        | [] -> replacementOrConflict sessions owner spec merged restored
        | many ->
            Task.FromResult(
                Error(
                    sprintf
                        "Ambiguous %s satellite recovery for %s: %d journal-linked Host children"
                        (kindLabel spec.Kind)
                        (SessionId.value owner)
                        many.Length
                )
            )

    let resolveLease
        (sessions: ISessionHostPort)
        owner
        (spec: SatelliteSpec)
        (merged: OpenCodeChildInfo list)
        (candidates: OpenCodeChildInfo list)
        =
        match spec.RestoredSessionId with
        | Some restored -> resolveRestored sessions owner spec merged candidates restored
        | None -> createChild sessions owner spec Created

    let private abortFreshLease (sessions: ISessionHostPort) (lease: SatelliteLease) =
        match lease.Origin with
        | Reused -> Task.FromResult(Ok())
        | Created
        | Replacement -> sessions.AbortSession lease.SessionId

    let private closeReplacedAssociation (spec: SatelliteSpec) owner (lease: SatelliteLease) =
        match lease.Origin, spec.RestoredSessionId with
        | Replacement, Some _ -> spec.Close owner
        | _ -> Task.FromResult(Ok())

    let private keepFreshLeaseOnlyOnSuccess
        (sessions: ISessionHostPort)
        (lease: SatelliteLease)
        (operation: Task<Result<unit, string>>)
        : Task<Result<unit, string>> =
        task {
            match! operation with
            | Ok() -> return Ok()
            | Error error ->
                let! _ = abortFreshLease sessions lease
                return Error error
        }

    let linkLease (sessions: ISessionHostPort) (spec: SatelliteSpec) owner (lease: SatelliteLease) =
        taskResult {
            // A replacement is a real durable state transition, not a repoint.
            // CompanionBloggerLinked deliberately rejects old→new in one step;
            // close the vanished attachment first, then establish the new one.
            do!
                closeReplacedAssociation spec owner lease
                |> keepFreshLeaseOnlyOnSuccess sessions lease

            do!
                spec.Link owner lease.SessionId spec.Agent
                |> keepFreshLeaseOnlyOnSuccess sessions lease

            return lease
        }

    let start (sessions: ISessionHostPort) (owner: SessionId) (spec: SatelliteSpec) =
        taskResult {
            // HOST-015: physical parent is always the family root; ownership is
            // proven by the journal link (RestoredSessionId), never by Host
            // parentID. Root children are the flat location; owner children are
            // queried too so satellites created before flattening stay reusable.
            let rootId = sessions.FamilyRootOf owner

            let! rootChildren =
                sessions.ListChildren rootId
                |> TaskValue.map (Result.mapError (recoverError spec.Kind))

            let! ownerChildren =
                listOwnerChildren sessions rootId owner
                |> TaskValue.map (Result.mapError (recoverError spec.Kind))

            let merged =
                (rootChildren @ ownerChildren) |> List.distinctBy (fun child -> child.SessionId)

            let candidates = merged |> List.filter (exactCandidate spec)
            let! lease = resolveLease sessions owner spec merged candidates
            return! linkLease sessions spec owner lease
        }

    let retireInFlight
        (sessions: ISessionHostPort)
        (invalidate: unit -> unit)
        (spec: SatelliteSpec)
        owner
        (pending: Task<Result<SatelliteLease, string>>)
        =
        taskResult {
            let! outcome = pending |> TaskResultCE.ofTask

            match outcome with
            | Error error ->
                invalidate ()
                return! Error error
            | Ok lease ->
                do! sessions.AbortSession lease.SessionId
                do! spec.Close owner
                invalidate ()
                return ()
        }

type SatelliteRuntime(sessions: ISessionHostPort) =
    let gate = obj ()
    // DSL-MUTABLE: single-flight — memoized satellite create task by owner+kind
    let flights = Dictionary<string, Task<Result<SatelliteLease, string>>>()

    let key owner kind =
        SessionId.value owner + "\u001f" + SatelliteLeaseFlow.kindLabel kind

    member _.Ensure(owner: SessionId, spec: SatelliteSpec) : Task<Result<SatelliteLease, string>> =
        lock gate (fun () ->
            let cacheKey = key owner spec.Kind

            match flights.TryGetValue cacheKey with
            | true, flight -> flight
            | false, _ ->
                let flight = SatelliteLeaseFlow.start sessions owner spec
                flights.[cacheKey] <- flight
                flight)

    member _.Invalidate(owner: SessionId, kind: SatelliteKind) =
        lock gate (fun () -> flights.Remove(key owner kind) |> ignore)

    member this.Retire(owner: SessionId, spec: SatelliteSpec) : Task<Result<unit, string>> =
        let cacheKey = key owner spec.Kind

        let flight =
            lock gate (fun () ->
                if flights.ContainsKey cacheKey then
                    Some flights.[cacheKey]
                else
                    None)

        let invalidate () = this.Invalidate(owner, spec.Kind)

        match flight with
        | None -> spec.Close owner
        | Some pending -> SatelliteLeaseFlow.retireInFlight sessions invalidate spec owner pending
