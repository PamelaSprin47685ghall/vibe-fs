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
open FsToolkit.ErrorHandling
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
    { OnLanesCreated: SessionId -> SessionId option -> string -> FissionStartedLane list -> Task<Result<unit, string>>
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

    // One process-wide single-flight resource for live admissions only.
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

    let private reserve (_runtime: FissionAdmissionRuntime) (ownerSessionId: SessionId) =
        lock gate (fun () -> activeOwners.Add(SessionId.value ownerSessionId))

    let private dependencyError message =
        Error(FissionRejectReason.RuntimeUnavailable message)

    let private abortLaneQuietly (runtime: FissionAdmissionRuntime) (lane: FissionStartedLane) =
        task {
            try
                do! runtime.Dependencies.AbortLane lane.SessionId
            with _ ->
                ()
        }

    let private rollback (runtime: FissionAdmissionRuntime) ownerSessionId (created: FissionStartedLane list) =
        task {
            for lane in created do
                do! abortLaneQuietly runtime lane

            release runtime ownerSessionId
        }

    let private dependencyOrRelease
        (runtime: FissionAdmissionRuntime)
        ownerSessionId
        prefix
        (operation: Task<Result<'a, string>>)
        : Task<Result<'a, FissionRejectReason>> =
        task {
            match! operation with
            | Ok value -> return Ok value
            | Error error ->
                release runtime ownerSessionId
                return dependencyError (prefix + error)
        }

    let private mapCreateLaneError createdRev (operation: Task<Result<SessionId, string>>) =
        task {
            match! operation with
            | Ok laneSessionId -> return Ok laneSessionId
            | Error error -> return Error(error, List.rev createdRev)
        }

    let rec private createLanes
        (runtime: FissionAdmissionRuntime)
        ownerSessionId
        parentSessionId
        (remaining: FissionLanePrompt list)
        (createdRev: FissionStartedLane list)
        =
        taskResult {
            match remaining with
            | [] -> return List.rev createdRev
            | lane :: rest ->
                let! laneSessionId =
                    mapCreateLaneError
                        createdRev
                        (runtime.Dependencies.CreateLane ownerSessionId parentSessionId lane)

                let started =
                    { Index = lane.Index
                      SessionId = laneSessionId
                      Prompt = lane.Prompt }

                return! createLanes runtime ownerSessionId parentSessionId rest (started :: createdRev)
        }

    let private createAllLanes
        (runtime: FissionAdmissionRuntime)
        ownerSessionId
        parentSessionId
        (lanes: FissionLanePrompt list)
        =
        task {
            match! createLanes runtime ownerSessionId parentSessionId lanes [] with
            | Ok created -> return Ok created
            | Error(error, created) ->
                do! rollback runtime ownerSessionId created
                return dependencyError ("lane create failed: " + error)
        }

    let private commitLanesCreated
        (runtime: FissionAdmissionRuntime)
        ownerSessionId
        parentSessionId
        ownerWorkRecord
        (created: FissionStartedLane list)
        =
        task {
            match! runtime.Hooks.OnLanesCreated ownerSessionId parentSessionId ownerWorkRecord created with
            | Ok() -> return Ok()
            | Error error ->
                do! rollback runtime ownerSessionId created
                return dependencyError ("fission admission commit failed: " + error)
        }

    let private startAllLanes
        (runtime: FissionAdmissionRuntime)
        ownerSessionId
        (parsed: ParsedFissionPrompts)
        ownerWorkRecord
        (created: FissionStartedLane list)
        =
        task {
            let! outcome =
                created
                |> TaskResultList.traverseM (fun started ->
                    let lane =
                        parsed.Lanes
                        |> List.find (fun candidate -> candidate.Index = started.Index)

                    let startup = FissionStartup.render parsed.Count lane ownerWorkRecord
                    runtime.Dependencies.StartLane started.SessionId startup)

            match outcome with
            | Ok _ -> return Ok()
            | Error error ->
                do! runtime.Hooks.OnFailed ownerSessionId ("lane start failed: " + error)
                do! rollback runtime ownerSessionId created
                return dependencyError ("lane start failed: " + error)
        }

    let private silentInterruptOwner
        (runtime: FissionAdmissionRuntime)
        ownerSessionId
        (created: FissionStartedLane list)
        =
        task {
            match! runtime.Dependencies.SilentInterruptOwner ownerSessionId with
            | Ok() -> return Ok()
            | Error error ->
                do! runtime.Hooks.OnFailed ownerSessionId ("silent owner interrupt failed: " + error)
                do! rollback runtime ownerSessionId created
                return dependencyError ("silent owner interrupt failed: " + error)
        }

    let private admitReserved
        (runtime: FissionAdmissionRuntime)
        (ownerSessionId: SessionId)
        (parsed: ParsedFissionPrompts)
        =
        taskResult {
            let! parentSessionId =
                dependencyOrRelease
                    runtime
                    ownerSessionId
                    "physical parent lookup failed: "
                    (runtime.Dependencies.ParentOf ownerSessionId)

            let! ownerWorkRecord =
                dependencyOrRelease
                    runtime
                    ownerSessionId
                    "owner LWR materialization failed: "
                    (runtime.Dependencies.OwnerWorkRecord ownerSessionId)

            if String.IsNullOrWhiteSpace ownerWorkRecord then
                release runtime ownerSessionId
                return! dependencyError "owner LWR materialization returned empty"
            else
                let! created = createAllLanes runtime ownerSessionId parentSessionId parsed.Lanes
                do! commitLanesCreated runtime ownerSessionId parentSessionId ownerWorkRecord created
                do! startAllLanes runtime ownerSessionId parsed ownerWorkRecord created
                do! silentInterruptOwner runtime ownerSessionId created

                return
                    { OwnerSessionId = ownerSessionId
                      ParentSessionId = parentSessionId
                      OwnerWorkRecord = ownerWorkRecord
                      Lanes = created }
        }

    let admit
        (runtime: FissionAdmissionRuntime)
        (ownerSessionId: SessionId)
        (parsed: ParsedFissionPrompts)
        : Task<Result<FissionAdmission, FissionRejectReason>> =
        taskResult {
            if parsed.Count < 2 then
                return! Error FissionRejectReason.TooFewLanes
            elif not (reserve runtime ownerSessionId) then
                return! Error FissionRejectReason.AlreadyFissioned
            else
                return! admitReserved runtime ownerSessionId parsed
        }
