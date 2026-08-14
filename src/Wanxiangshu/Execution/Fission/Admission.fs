namespace Wanxiangshu.Execution.Fission
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Enforcer.Guidance
open Wanxiangshu.Execution.Delegation.Handle
open Wanxiangshu.Execution.Session
open Wanxiangshu.Execution.Session.Attachment
open Wanxiangshu.Execution.Session.Wait
open Wanxiangshu.Participant.Provider.Attempt.Fallback

open System
open System.Collections.Generic
open System.Threading.Tasks
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Context.Trace
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Foundation
open Wanxiangshu.Host
open Wanxiangshu.Host.Contract
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Mission.Finality
open Wanxiangshu.Mission.Manager
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Mission.Review
open Wanxiangshu.Mission.Review.Judgement
open Wanxiangshu.Mission.WorkRecord
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Repository.Investigation.WarmStart
open Wanxiangshu.Repository.Knowledge.Casebook
open Wanxiangshu.Repository.Programming.Js
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Prediction
open Wanxiangshu.Strength.Projection
open Wanxiangshu.Strength.Replica
open Wanxiangshu.Foundation.Identity

[<CLIMutable>]
type FissionStartedLane =
    { Index: int
      SessionId: SessionId
      Prompt: string }

[<CLIMutable>]
type FissionAdmission =
    { OwnerSessionId: SessionId
      ParentSessionId: SessionId option
      OwnerWorkRecord: string
      Lanes: FissionStartedLane list }

[<CLIMutable>]
type FissionAdmissionDependencies =
    { ParentOf: SessionId -> Task<Result<SessionId option, string>>
      OwnerWorkRecord: SessionId -> Task<Result<string, string>>
      CreateLane: SessionId -> SessionId option -> FissionLanePrompt -> Task<Result<SessionId, string>>
      StartLane: SessionId -> string -> Task<Result<unit, string>>
      AbortLane: SessionId -> Task
      SilentInterruptOwner: SessionId -> Task<Result<unit, string>> }

[<CLIMutable>]
type FissionAdmissionHooks =
    { OnLanesCreated:
        SessionId -> SessionId option -> string -> FissionStartedLane list -> Task<Result<unit, string>>
      OnFailed: SessionId -> string -> Task }

type FissionAdmissionRuntime internal (deps: FissionAdmissionDependencies, hooks: FissionAdmissionHooks) =
    member internal _.Dependencies = deps
    member internal _.Hooks = hooks

module FissionStartup =

    /// Fresh lane startup deliberately uses a bounded LWR rather than transcript
    /// cloning. The lane input is copied exactly after the canonical parser.
    let render laneCount (lane: FissionLanePrompt) (ownerWorkRecord: string) =
        String.concat
            "\n"
            [ "You are one coequal present of the same logical participant after Fission."
              sprintf "lane_index = %d" lane.Index
              sprintf "lane_count = %d" laneCount
              ""
              "[fission_input]"
              lane.Prompt
              ""
              "[owner_lifecycle_work_record]"
              ownerWorkRecord
              ""
              "Continue the same participant's responsibility. Do not treat sibling lanes as delegated agents." ]

module FissionAdmission =

    // One process-wide single-flight resource. Durable FissionProjection is the
    // restart authority; this lock prevents two live plugin instances from both
    // admitting the same logical owner before either durable append is visible.
    let private gate = obj ()
    let private activeOwners = HashSet<string>()

    let private noHooks =
        { OnLanesCreated = fun _ _ _ _ -> Task.FromResult(Ok())
          OnFailed = fun _ _ -> Task.FromResult(()) }

    let create deps = FissionAdmissionRuntime(deps, noHooks)

    let createWithHooks deps hooks = FissionAdmissionRuntime(deps, hooks)

    let isActive (_runtime: FissionAdmissionRuntime) (ownerSessionId: SessionId) =
        lock gate (fun () -> activeOwners.Contains(SessionId.value ownerSessionId))

    let release (_runtime: FissionAdmissionRuntime) (ownerSessionId: SessionId) =
        lock gate (fun () -> activeOwners.Remove(SessionId.value ownerSessionId) |> ignore)

    let releaseOwner (ownerSessionId: SessionId) =
        lock gate (fun () -> activeOwners.Remove(SessionId.value ownerSessionId) |> ignore)

    let restoreActiveOwner (ownerSessionId: SessionId) =
        lock gate (fun () -> activeOwners.Add(SessionId.value ownerSessionId) |> ignore)

    let private reserve (_runtime: FissionAdmissionRuntime) (ownerSessionId: SessionId) =
        lock gate (fun () -> activeOwners.Add(SessionId.value ownerSessionId))

    let private dependencyError message =
        Error(FissionRejectReason.RuntimeUnavailable message)

    let private rollback (runtime: FissionAdmissionRuntime) ownerSessionId (created: FissionStartedLane list) =
        task {
            for lane in created do
                try
                    do! runtime.Dependencies.AbortLane lane.SessionId
                with _ ->
                    ()

            release runtime ownerSessionId
        }

    let admit
        (runtime: FissionAdmissionRuntime)
        (ownerSessionId: SessionId)
        (parsed: ParsedFissionPrompts)
        : Task<Result<FissionAdmission, FissionRejectReason>> =
        task {
            if parsed.Count < 2 then
                return Error FissionRejectReason.TooFewLanes
            elif not (reserve runtime ownerSessionId) then
                return Error FissionRejectReason.AlreadyFissioned
            else
                match! runtime.Dependencies.ParentOf ownerSessionId with
                | Error error ->
                    release runtime ownerSessionId
                    return dependencyError ("physical parent lookup failed: " + error)
                | Ok parentSessionId ->
                    match! runtime.Dependencies.OwnerWorkRecord ownerSessionId with
                    | Error error ->
                        release runtime ownerSessionId
                        return dependencyError ("owner LWR materialization failed: " + error)
                    | Ok ownerWorkRecord when String.IsNullOrWhiteSpace ownerWorkRecord ->
                        release runtime ownerSessionId
                        return dependencyError "owner LWR materialization returned empty"
                    | Ok ownerWorkRecord ->
                        let rec createLanes remaining createdRev =
                            task {
                                match remaining with
                                | [] -> return Ok(List.rev createdRev)
                                | lane :: rest ->
                                    match! runtime.Dependencies.CreateLane ownerSessionId parentSessionId lane with
                                    | Error error -> return Error(error, List.rev createdRev)
                                    | Ok laneSessionId ->
                                        let started =
                                            { Index = lane.Index
                                              SessionId = laneSessionId
                                              Prompt = lane.Prompt }

                                        return! createLanes rest (started :: createdRev)
                            }

                        match! createLanes parsed.Lanes [] with
                        | Error(error, created) ->
                            do! rollback runtime ownerSessionId created
                            return dependencyError ("lane create failed: " + error)
                        | Ok created ->
                            match! runtime.Hooks.OnLanesCreated ownerSessionId parentSessionId ownerWorkRecord created with
                            | Error error ->
                                do! rollback runtime ownerSessionId created
                                return dependencyError ("fission admission commit failed: " + error)
                            | Ok() ->
                                let rec startLanes remaining =
                                    task {
                                        match remaining with
                                        | [] -> return Ok()
                                        | started :: rest ->
                                            let lane =
                                                parsed.Lanes
                                                |> List.find (fun candidate -> candidate.Index = started.Index)

                                            let startup = FissionStartup.render parsed.Count lane ownerWorkRecord

                                            match! runtime.Dependencies.StartLane started.SessionId startup with
                                            | Error error -> return Error error
                                            | Ok() -> return! startLanes rest
                                    }

                                match! startLanes created with
                                | Error error ->
                                    do! runtime.Hooks.OnFailed ownerSessionId ("lane start failed: " + error)
                                    do! rollback runtime ownerSessionId created
                                    return dependencyError ("lane start failed: " + error)
                                | Ok() ->
                                    match! runtime.Dependencies.SilentInterruptOwner ownerSessionId with
                                    | Error error ->
                                        do! runtime.Hooks.OnFailed ownerSessionId ("silent owner interrupt failed: " + error)
                                        do! rollback runtime ownerSessionId created
                                        return dependencyError ("silent owner interrupt failed: " + error)
                                    | Ok() ->
                                        return
                                            Ok
                                                { OwnerSessionId = ownerSessionId
                                                  ParentSessionId = parentSessionId
                                                  OwnerWorkRecord = ownerWorkRecord
                                                  Lanes = created }
        }
