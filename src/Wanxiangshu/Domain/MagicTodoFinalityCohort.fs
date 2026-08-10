namespace Wanxiangshu.Domain

open Wanxiangshu.Domain.MagicTodo
open Wanxiangshu.Domain.MagicTodoFacts
open Wanxiangshu.Kernel.Identity

/// Dedicated process-reviewer ↔ Finality cohort integration (protocol §30).
/// Speculative / unwired — does not invent "never graduate / force re-enlist".
module MagicTodoFinalityCohort =

    /// Ordinary graduation: after first dual-PERFECT on a FinalityRequest,
    /// Dedicated is NOT force-re-enlisted by Magic Todo on later requests.
    type DedicatedFinalityPolicy =
        { /// Physical session still retained for process-review until LifeCompleted.
          RetainProcessSessionUntilLifeCompleted: bool
          /// Enlist on first terminal Finality only if not yet graduated.
          EnlistOnFirstFinalityIfUngraduated: bool }

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
        | Some _ ->
            policy.EnlistOnFirstFinalityIfUngraduated
            && not alreadyGraduated

    /// Process PERFECT must not count as terminal first PERFECT / dual-PERFECT.
    let processPerfectIsTerminalWitness = false

    /// Resource split: Finality graduation ≠ process-review session disposal.
    let mustRetainProcessSession
        (policy: DedicatedFinalityPolicy)
        (lifeCompleted: bool)
        (graduatedFromFinality: bool)
        : bool =
        let _ = graduatedFromFinality

        policy.RetainProcessSessionUntilLifeCompleted
        && not lifeCompleted
