namespace Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Context.Trace
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Foundation
open Wanxiangshu.Host
open Wanxiangshu.Mission.WorkRecord
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Participant.Provider.Projection

open Wanxiangshu.Mission.Obligation.Todo.MagicTodo
open Wanxiangshu.Mission.Obligation.Todo.MagicTodoFacts
open Wanxiangshu.Foundation.Identity

/// Dedicated process-reviewer ↔ Finality cohort integration (protocol §30).
/// Speculative / unwired — does not invent "never graduate / force re-enlist".
module MagicTodoFinalityCohort =

    /// Ordinary graduation: after first dual-PERFECT on a FinalityRequest,
    /// Dedicated is NOT force-re-enlisted by Magic Todo on later requests.
    type DedicatedFinalityPolicy =
        {
            /// Physical session still retained for process-review until LifeCompleted.
            RetainProcessSessionUntilLifeCompleted: bool
            /// Enlist on first terminal Finality only if not yet graduated.
            EnlistOnFirstFinalityIfUngraduated: bool
        }

    let defaultPolicy: DedicatedFinalityPolicy =
        { RetainProcessSessionUntilLifeCompleted = true
          EnlistOnFirstFinalityIfUngraduated = true }

    /// Whether Dedicated should be added to the current Finality roster.
    let shouldEnlistDedicated
        (policy: DedicatedFinalityPolicy)
        (dedicated: DedicatedTodoReviewerEnlisted option)
        (alreadyGraduated: bool)
        : bool =
        match dedicated with
        | None -> false
        | Some _ -> policy.EnlistOnFirstFinalityIfUngraduated && not alreadyGraduated

    /// Process PERFECT must not count as terminal first PERFECT / dual-PERFECT.
    let processPerfectIsTerminalWitness = false

    /// Resource split: Finality graduation ≠ process-review session disposal.
    let mustRetainProcessSession
        (policy: DedicatedFinalityPolicy)
        (lifeCompleted: bool)
        (graduatedFromFinality: bool)
        : bool =
        let _ = graduatedFromFinality

        policy.RetainProcessSessionUntilLifeCompleted && not lifeCompleted
