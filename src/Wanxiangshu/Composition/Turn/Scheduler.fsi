namespace Wanxiangshu.Composition.Turn

open System.Collections.Generic
open System.Threading.Tasks
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Host
open Wanxiangshu.Host.Contract
open Wanxiangshu.OpenCode

/// Direct-CE reconcile scheduler (FLOW-001 / PR4).
/// Owns queue / generation / single-flight / clear-session runtime state.
/// Pass body lives in ReconcilePass — Scheduler only drains and dispatches.
module Reconciler =

    type Scheduler =
        new:
            snapshot: ISessionSnapshotPort *
            binding: TurnBinding.Store *
            onTurn: (ReconciledTurnContext -> Task) *
            ?onDeleted: (SessionId -> unit) *
            ?projection: (SessionId -> AgentProjectionSet option) *
            ?onSnapshot: (SessionId -> SessionMessage list -> Task) *
            ?durableUnavailable: (unit -> bool) *
            ?maxCausalRereads: int *
            ?maxConsecutiveErrors: int ->
                Scheduler

        member Kick: sessionId: SessionId * wake: ReconcileProgram.ReconcileWake -> unit

        member StopAndDrain: unit -> Task

        member SignalIdle: sessionId: SessionId * permit: QuiescencePermit -> unit

        /// HOST-BOUNDARY-001/005: terminal assistant message.updated is an
        /// infrastructure-only projection edge. It can only wake an already
        /// parked coarse-signal occasion for the exact current physical user;
        /// it never creates or changes business terminal semantics.
        member NotifyProjectionChanged: sessionId: SessionId * physical: PhysicalUserMessageId -> unit

        member Signal: signal: HostSignal -> unit

        member BindUserMessage: sessionId: SessionId * physical: PhysicalUserMessageId * ?agentRole: Role -> unit

        member BindContinuationUserMessage: sessionId: SessionId * physical: PhysicalUserMessageId -> unit

        member BindPhysicalUserMaterial: sessionId: SessionId * physical: PhysicalUserMessageId -> unit

        member BindActiveRun: value: ActiveRunBinding -> unit

        member TryPhysicalUserMessage: sessionId: SessionId -> PhysicalUserMessageId option

        member RootBindings: Dictionary<string, PhysicalUserMessageId>

        member ClearSession: sessionId: SessionId -> unit
