module Wanxiangshu.Shell.EffectSupervisor

open Fable.Core
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.Reactive
open Wanxiangshu.Kernel.Subsession.Types
open Wanxiangshu.Shell.CommandProcessor

// ── EffectSupervisor ──

/// Manages the lifecycle of host-side effects for a single subsession.
///
/// Host effects are dispatched as fire-and-forget Promises. Their
/// completion re-enters the CommandProcessor via the `postCommand`
/// callback (Law 6: No Reentrancy — re-entry is always via enqueue,
/// never synchronous).
type EffectSupervisor
    (sessionId: SessionId, host: ISubsessionHost, postCommand: Command -> unit, ?reactivePort: IReactivePort) =
    let react = defaultArg reactivePort (NullReactivePort() :> IReactivePort)

    /// True for effects that require host-side async dispatch.
    let isHostEffect (effect: Effect) : bool =
        match effect with
        | DispatchPrompt _
        | QueryDispatchStatus _
        | QuerySessionQuiescence _
        | ClosePhysicalSession _
        | AbortHostSession _ -> true
        | _ -> false

    let infrastructureError (ex: exn) : ErrorInput =
        { ErrorName = "InfrastructureFailure"
          DomainError = None
          Message = ex.Message
          StatusCode = None
          IsRetryable = Some false }

    /// Emit ephemeral telemetry (fire-and-forget, never blocks).
    let emit (t: EphemeralTelemetry) = react.OnTelemetry [ t ]

    /// Launch a single host effect as fire-and-forget.
    /// On completion, posts the result command back to the processor.
    member _.Launch(effect: Effect) : unit =
        match effect with
        | DispatchPrompt plan ->
            emit (TelemetryHostDispatchStart plan.TurnId)

            host.Dispatch(sessionId, plan)
            |> Promise.map (function
                | Ok receipt ->
                    emit (TelemetryHostDispatchOk(plan.TurnId, receipt))
                    postCommand (DispatchAccepted(plan.TurnId, receipt))
                | Error failure ->
                    emit (TelemetryHostDispatchError(plan.TurnId, string failure))
                    postCommand (DispatchRejected(plan.TurnId, failure)))
            |> Promise.catch (fun ex ->
                emit (TelemetryHostDispatchError(plan.TurnId, ex.Message))

                postCommand (
                    DispatchRejected(plan.TurnId, DispatchFailure.HostAcceptanceUnknown(infrastructureError ex))
                ))
            |> ignore

        | QueryDispatchStatus(sid, tid) ->
            emit (TelemetryHostQuery("QueryDispatchStatus", TurnId.value tid))

            host.QueryDispatchStatus(sid, tid)
            |> Promise.map (fun status -> postCommand (DispatchStatusResolved status))
            |> Promise.catch (fun _ -> postCommand (DispatchStatusResolved Unknown))
            |> ignore

        | QuerySessionQuiescence(sid, tid) ->
            emit (TelemetryHostQuery("QuerySessionQuiescence", TurnId.value tid))

            host.QuerySessionQuiescence(sid, tid)
            |> Promise.map (fun status -> postCommand (SessionQuiescenceResolved status))
            |> Promise.catch (fun _ -> postCommand (SessionQuiescenceResolved StopUnknown))
            |> ignore

        | ClosePhysicalSession sid ->
            emit (TelemetryHostQuery("ClosePhysicalSession", SessionId.value sid))

            host.ClosePhysicalSession sid
            |> Promise.map (fun status -> postCommand (PhysicalCloseResolved status))
            |> Promise.catch (fun _ -> postCommand (PhysicalCloseResolved StopUnknown))
            |> ignore

        | AbortHostSession(sid, tid) ->
            emit (TelemetryHostAbortStart tid)

            host.Abort(sid, tid)
            |> Promise.map (function
                | ConfirmedStopped ->
                    emit (TelemetryHostAbortResult(tid, "ConfirmedStopped"))
                    postCommand (AbortConfirmed tid)
                | RequestAcceptedAwaitIdle ->
                    emit (TelemetryHostAbortResult(tid, "RequestAcceptedAwaitIdle"))
                    postCommand (AbortHostAccepted tid)
                | AbortUnavailable ->
                    emit (TelemetryHostAbortResult(tid, "AbortUnavailable"))

                    postCommand (
                        AbortRequestFailed(tid, infrastructureError (System.Exception "host abort API unavailable"))
                    ))
            |> Promise.catch (fun ex ->
                emit (TelemetryHostAbortResult(tid, "Error:" + ex.Message))
                postCommand (AbortRequestFailed(tid, infrastructureError ex)))
            |> ignore

        | _ -> () // non-host effects are handled by CommandProcessor

    /// Launch multiple host effects. Each is fire-and-forget.
    member this.LaunchAll(effects: Effect list) : unit =
        for e in effects do
            if isHostEffect e then
                this.Launch e

    /// Filter effects to host-only.
    member _.FilterHostEffects(effects: Effect list) : Effect list = effects |> List.filter isHostEffect
