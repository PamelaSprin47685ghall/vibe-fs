namespace Wanxiangshu.Execution.Session.Attachment

open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Enforcer.Guidance
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.Handle
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Execution.Session.Wait
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt.Fallback
open Wanxiangshu.Strength

open System
open System.Collections.Generic
open System.Threading.Tasks
open Wanxiangshu.Execution.Session
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.OpenCode

/// HOST-014: one recovery and ownership mechanism for Companion leaf satellites.
/// The runtime owns only physical Session lifecycle; semantic state remains in
/// the caller's durable projection (Companion).
type SatelliteOrigin =
    | Created
    | Reused
    | Replacement

type SatelliteLease =
    { SessionId: SessionId
      Origin: SatelliteOrigin }

type SatelliteSpec =
    { Kind: SatelliteKind
      Agent: string
      Title: string
      Directory: string option
      RestoredSessionId: SessionId option
      Link: SessionId -> SessionId -> string -> Task<Result<unit, string>>
      Close: SessionId -> Task<Result<unit, string>> }

type SatelliteRuntime(sessions: ISessionHostPort) =
    let gate = obj ()
    let flights = Dictionary<string, Task<Result<SatelliteLease, string>>>()

    let kindLabel =
        function
        | SatelliteKind.Companion -> "companion"

    let key owner kind =
        SessionId.value owner + "\u001f" + kindLabel kind

    let exactCandidate (spec: SatelliteSpec) (child: OpenCodeChildInfo) =
        child.Agent = Some spec.Agent && child.Title = Some spec.Title

    let start (owner: SessionId) (spec: SatelliteSpec) =
        task {
            // HOST-015: physical parent is always the family root; ownership is
            // proven by the journal link (RestoredSessionId), never by Host
            // parentID. Root children are the flat location; owner children are
            // queried too so satellites created before flattening stay reusable.
            let rootId = sessions.FamilyRootOf owner

            match! sessions.ListChildren rootId with
            | Error error -> return Error(sprintf "Cannot recover %s satellite: %s" (kindLabel spec.Kind) error)
            | Ok rootChildren ->
                let! ownerChildren =
                    if rootId = owner then
                        Task.FromResult(Ok [])
                    else
                        sessions.ListChildren owner

                match ownerChildren with
                | Error error -> return Error(sprintf "Cannot recover %s satellite: %s" (kindLabel spec.Kind) error)
                | Ok ownerChildren ->
                    let merged =
                        (rootChildren @ ownerChildren) |> List.distinctBy (fun child -> child.SessionId)

                    let candidates = merged |> List.filter (exactCandidate spec)

                    let! resolved =
                        task {
                            match spec.RestoredSessionId with
                            | Some restored ->
                                match candidates |> List.filter (fun child -> child.SessionId = restored) with
                                | [ child ] ->
                                    return
                                        Ok
                                            { SessionId = child.SessionId
                                              Origin = Reused }
                                | [] ->
                                    match merged |> List.filter (fun child -> child.SessionId = restored) with
                                    | [] ->
                                        // The journal-linked child is permanently
                                        // gone from the Host → Replacement.
                                        match!
                                            sessions.CreateChildSession(
                                                owner,
                                                { Title = Some spec.Title
                                                  Agent = Some spec.Agent
                                                  Directory = spec.Directory }
                                            )
                                        with
                                        | Error error -> return Error error
                                        | Ok child ->
                                            return
                                                Ok
                                                    { SessionId = child
                                                      Origin = Replacement }
                                    | _ ->
                                        return
                                            Error(
                                                sprintf
                                                    "Conflicting %s satellite recovery for %s: journal-linked Host child has different agent/title"
                                                    (kindLabel spec.Kind)
                                                    (SessionId.value owner)
                                            )
                                | many ->
                                    return
                                        Error(
                                            sprintf
                                                "Ambiguous %s satellite recovery for %s: %d journal-linked Host children"
                                                (kindLabel spec.Kind)
                                                (SessionId.value owner)
                                                many.Length
                                        )
                            | None ->
                                // No journal link proves ownership of any existing
                                // child: never adopt a same-agent/title sibling
                                // under the shared flat root — always create.
                                match!
                                    sessions.CreateChildSession(
                                        owner,
                                        { Title = Some spec.Title
                                          Agent = Some spec.Agent
                                          Directory = spec.Directory }
                                    )
                                with
                                | Error error -> return Error error
                                | Ok child -> return Ok { SessionId = child; Origin = Created }
                        }

                    match resolved with
                    | Error error -> return Error error
                    | Ok lease ->
                        // The reverse association must exist before the first prompt so
                        // transforms can prove that this child is a leaf satellite.
                        match! spec.Link owner lease.SessionId spec.Agent with
                        | Ok() -> return Ok lease
                        | Error error ->
                            let! _ = sessions.AbortSession lease.SessionId
                            return Error error
        }

    member _.Ensure(owner: SessionId, spec: SatelliteSpec) : Task<Result<SatelliteLease, string>> =
        lock gate (fun () ->
            let cacheKey = key owner spec.Kind

            match flights.TryGetValue cacheKey with
            | true, flight -> flight
            | false, _ ->
                let flight = start owner spec
                flights.[cacheKey] <- flight
                flight)

    member _.Invalidate(owner: SessionId, kind: SatelliteKind) =
        lock gate (fun () -> flights.Remove(key owner kind) |> ignore)

    member this.Retire(owner: SessionId, spec: SatelliteSpec) : Task<Result<unit, string>> =
        task {
            let cacheKey = key owner spec.Kind

            let flight =
                lock gate (fun () ->
                    if flights.ContainsKey cacheKey then
                        Some flights.[cacheKey]
                    else
                        None)

            match flight with
            | None ->
                match! spec.Close owner with
                | Ok() -> return Ok()
                | Error error -> return Error error
            | Some pending ->
                match! pending with
                | Error error ->
                    this.Invalidate(owner, spec.Kind)
                    return Error error
                | Ok lease ->
                    match! sessions.AbortSession lease.SessionId with
                    | Error error -> return Error error
                    | Ok() ->
                        match! spec.Close owner with
                        | Error error -> return Error error
                        | Ok() ->
                            this.Invalidate(owner, spec.Kind)
                            return Ok()
        }
