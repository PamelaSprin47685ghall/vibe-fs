namespace Wanxiangshu.Session

open System
open System.Collections.Generic
open System.Threading.Tasks
open Wanxiangshu.Journal
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.OpenCode

/// HOST-014: one recovery and ownership mechanism for every leaf satellite.
/// The runtime owns only physical Session lifecycle; semantic state remains in
/// the caller's durable projection (Companion) or QA.md (Teacher).
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
      Link: SessionId -> SessionId -> string -> Result<unit, string>
      Close: SessionId -> Result<unit, string> }

type SatelliteRuntime(sessions: ISessionHostPort) =
    let gate = obj ()
    let flights = Dictionary<string, Task<Result<SatelliteLease, string>>>()

    let kindLabel =
        function
        | SatelliteKind.Companion -> "companion"
        | SatelliteKind.Teacher -> "teacher"

    let key owner kind =
        SessionId.value owner + "\u001f" + kindLabel kind

    let exactCandidate (owner: SessionId) (spec: SatelliteSpec) (child: OpenCodeChildInfo) =
        child.ParentSessionId = Some owner
        && child.Agent = Some spec.Agent
        && child.Title = Some spec.Title

    let start (owner: SessionId) (spec: SatelliteSpec) =
        task {
            match! sessions.ListChildren owner with
            | Error error -> return Error(sprintf "Cannot recover %s satellite: %s" (kindLabel spec.Kind) error)
            | Ok children ->
                let candidates = children |> List.filter (exactCandidate owner spec)

                let! resolved =
                    task {
                        match candidates with
                        | [ child ] ->
                            let origin =
                                match spec.RestoredSessionId with
                                | Some restored when restored <> child.SessionId -> SatelliteOrigin.Replacement
                                | _ -> SatelliteOrigin.Reused

                            return
                                Ok
                                    { SessionId = child.SessionId
                                      Origin = origin }
                        | [] ->
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
                                let origin =
                                    if spec.RestoredSessionId.IsSome then
                                        SatelliteOrigin.Replacement
                                    else
                                        SatelliteOrigin.Created

                                return Ok { SessionId = child; Origin = origin }
                        | many ->
                            return
                                Error(
                                    sprintf
                                        "Ambiguous %s satellite recovery for %s: %d exact Host children"
                                        (kindLabel spec.Kind)
                                        (SessionId.value owner)
                                        many.Length
                                )
                    }

                match resolved with
                | Error error -> return Error error
                | Ok lease ->
                    // The reverse association must exist before the first prompt so
                    // transforms can prove that this child is a leaf satellite.
                    match spec.Link owner lease.SessionId spec.Agent with
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
                match spec.Close owner with
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
                        match spec.Close owner with
                        | Error error -> return Error error
                        | Ok() ->
                            this.Invalidate(owner, spec.Kind)
                            return Ok()
        }
