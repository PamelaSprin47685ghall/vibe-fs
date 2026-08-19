namespace Wanxiangshu.OpenCode.Host

open System
open System.Collections.Generic
open System.Threading.Tasks
open Fable.Core.JsInterop
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Fission.OpenCode
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Foundation.Outcome
open Wanxiangshu.OpenCode

/// JS-native Host boundary surface for Fission turn absorption.
///
/// This module composes host session/event ports with ordinary-turn observation
/// and publishes only a JSON-shaped observation for the logical-owner law. It
/// keeps Host capabilities private; callers cannot obtain emitted turn values.
module FissionHostSurface =

    /// INTRA-PARTICIPANT-PARALLELISM-013: expose the exact request-local
    /// provider tool projection without exposing Host session registries.
    let projectFissionToolVisibility (hasPhysicalParent: bool) (tools: obj) : obj =
        if FissionRequestProjection.apply hasPhysicalParent then
            tools?fission <- box false

        tools

    type private CallFlags() =
        // DSL-MUTABLE: single-flight — one-shot continuation sent latch.
        member val ContinuationSent = false with get, set
        // DSL-MUTABLE: single-flight — one-shot terminal notified latch.
        member val TerminalNotified = false with get, set

    type private DummyDeadline() =
        interface IDeadlineHandle with
            member _.Delay = Task.FromResult(())
            member _.Cancel() = ()

    type private DummyTimer() =
        interface ITimerPort with
            member _.Delay _ = DummyDeadline() :> IDeadlineHandle
            member _.Dispose() = ()

    type private DummySessionPort(flags: CallFlags) =
        interface ISessionHostPort with
            member _.SubscribeTerminal(_, _) =
                { new IDisposable with
                    member _.Dispose() = () }

            member _.SendPrompt(_, _, _) =
                flags.ContinuationSent <- true
                Task.FromResult(SendOutcome.AdmittedWithReceipt(TransportReceipt.create "receipt"))

            member _.AbortSession _ = Task.FromResult(Ok())
            member _.InterruptAttempt _ = Task.FromResult(Ok())

            member _.TerminateAttempt(_sessionId: SessionId, _reason: string) : Task<Result<unit, string>> =
                Task.FromResult(Ok())

            member _.TryTakeAttemptTermination(_sessionId: SessionId) : string option = None
            member _.AbortChildren _ = AsyncSupport.completedTask ()
            member _.CreateSiblingSession(_, _, _) = Task.FromResult(Error "unused")
            member _.TryGetParentSession _ = Task.FromResult(Ok None)
            member _.CreateChildSession(_, _) = Task.FromResult(Error "unused")
            member _.ListChildren _ = Task.FromResult(Ok [])
            member _.FamilyRootOf sessionId = sessionId

    type private DummyEventPort(flags: CallFlags) =
        interface IEventObservationPort with
            member _.SubscribeTerminalListener _ =
                { new IDisposable with
                    member _.Dispose() = () }

            member _.NotifyTerminal _ _ =
                flags.TerminalNotified <- true
                true

    let private dummyTurn (owner: SessionId) : ReconciledTurn =
        { SessionId = owner
          PhysicalUserMessageId = PhysicalUserMessageId.create "msg-1"
          AuthorityRootUserMessageId = AuthorityRootUserMessageId.create "msg-0"
          ProviderRun = ProviderRunIdentity.create "run-1"
          Role = None
          Directory = None
          Parts = [||]
          Finish = None
          ErrorName = None
          Model = None
          Outcome = ReconcileProgram.TurnInProgress
          Observation = None }

    /// Absorb a Fission-replaced owner turn through Host + ordinary-turn observe.
    /// Caller must have already `markSilentInterrupt`'d the owner.
    let observeReplacedOwner (ownerSessionId: string) : Task<obj> =
        task {
            let flags = CallFlags()
            let sessionPort = DummySessionPort flags :> ISessionHostPort
            let eventPort = DummyEventPort flags :> IEventObservationPort
            let owner = SessionId.create ownerSessionId
            let turn = dummyTurn owner
            let! handled = FissionHost.observeLaneTurn sessionPort eventPort None (HashSet<string>()) turn

            let context =
                { Turn = turn
                  Quiescence = None
                  Delivery = ReconciledTurnDelivery.Observation }

            do!
                OrdinaryTurnWorkflow.observe
                    (DummyTimer() :> ITimerPort)
                    (fun _ -> ())
                    (fun _ -> Task.FromResult(()) :> Task)
                    sessionPort
                    eventPort
                    None
                    (HashSet<string>())
                    (fun _ -> false)
                    (HashSet<string>())
                    AbortCause.External
                    (SessionQuiescenceGate())
                    context

            let idleContext =
                { context with
                    Delivery = ReconciledTurnDelivery.IdleRevisit }

            do! OrdinaryTurnWorkflow.observeIdle (SessionQuiescenceGate()) sessionPort eventPort None idleContext

            return
                box
                    {| handled = handled
                       continuationSent = flags.ContinuationSent
                       terminalNotified = flags.TerminalNotified |}
        }
