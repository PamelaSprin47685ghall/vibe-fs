namespace Wanxiangshu.OpenCode

open System.Threading.Tasks
open Wanxiangshu.Host
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.Session

/// GLORY-018/020: legacy Activation vocabulary retained as no-op entry.
/// BlindPlan / TODO-001 — production never sends ManagerWorkActivation;
/// WorkActivated remains inert legacy decode only.
module ManagerActivation =

    /// Result of ensuring Activation for a reconciled Manager turn.
    [<RequireQualifiedAccess>]
    type EnsureAcceptedResult =
        /// Activation was sent this observation, or Activation is not yet
        /// warranted / no current Life — caller should stop here.
        | Deferred
        /// Activation already established (or Life present without a fresh
        /// activation send); caller may proceed to idle/labor.
        | Ready of LifeProjection

    let private currentLife (journal: AgentJournal option) (sessionId: SessionId) =
        journal
        |> Option.bind (fun durable ->
            AgentProjection.tryFind sessionId (AgentJournal.snapshot durable).AgentProjections)
        |> Option.bind (fun session -> session.ManagerLife)
        |> Option.bind (fun lifecycle -> lifecycle.CurrentLife)

    /// Production Activation path deleted (GLORY-018). Return Ready when a Life
    /// exists so idle/labor continue under BlindPlan without WorkActivated floor.
    let ensureAccepted
        (_sessionPort: ISessionHostPort)
        (_eventPort: IEventObservationPort)
        (journal: AgentJournal option)
        (turn: ReconciledTurn)
        : Task<EnsureAcceptedResult> =
        task {
            match currentLife journal turn.SessionId with
            | Some life -> return EnsureAcceptedResult.Ready life
            | None -> return EnsureAcceptedResult.Deferred
        }
