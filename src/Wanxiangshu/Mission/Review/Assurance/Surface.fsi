namespace Wanxiangshu.Mission.Review.Assurance

open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Mission.Review
open Wanxiangshu.Mission.Review.Barrier
open Wanxiangshu.Mission.Review.Judgement
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Resources

/// Review-assurance owner boundary for semantic tests and host adapters.
///
/// The production review state remains typed and durable. This boundary exposes
/// only strings, arrays, records, and opaque projection handles so callers never
/// need Fable DU/list/result mechanics or internal module paths.
[<RequireQualifiedAccess>]
module ReviewAssuranceSurface =

    type GuardHandle =
        new: current: ReviewGuardProjection -> GuardHandle
        member Current: ReviewGuardProjection
        static member Create: current: ReviewGuardProjection -> GuardHandle

    type RequirementsHandle =
        new: current: ReviewRequirementProjection -> RequirementsHandle
        member Current: ReviewRequirementProjection
        static member Create: current: ReviewRequirementProjection -> RequirementsHandle

    type ConfirmedReviewWitnessHandle =
        new: witness: ConfirmedReviewWitness -> ConfirmedReviewWitnessHandle
        member Witness: ConfirmedReviewWitness
        static member Create: witness: ConfirmedReviewWitness -> ConfirmedReviewWitnessHandle

    val challengePath: string

    val challengeText: language: string -> string

    val challengePrompt: text: string -> string

    val challengeObject: language: string -> obj

    val verdictWitness: value: obj -> obj

    val attemptIdentity: barrier: string -> witness: obj -> obj

    val dedupeKey: attempt: obj -> string

    val isDistinctAttempt: barrier: string -> first: obj -> second: obj -> bool

    val confirmWitness:
        barrier: string -> firstPhysical: string -> secondPhysical: string -> first: obj -> second: obj -> obj

    val isConfirmed: witness: obj -> bool

    val isRevision: witness: obj -> bool

    val readWitness: witness: obj -> obj

    val confirmedWitnessRecord: witness: obj -> obj

    val noReview: obj

    val gitTreeHash: witness: obj -> string

    val confirmedReviewer: witness: obj -> string

    val isValidForTree: tree: string -> witness: obj -> bool

    val projectConfirmedReview: lifeId: string -> requestId: string -> tree: string -> memberWitnesses: obj array -> obj

    val confirmedReviewWitnessTree: witness: ConfirmedReviewWitnessHandle -> string

    val isConfirmedReviewValidForTree: tree: string -> witness: ConfirmedReviewWitnessHandle -> bool

    val verifyCandidate: candidateTree: string -> witness: ConfirmedReviewWitnessHandle -> obj

    val emptyGuard: unit -> GuardHandle

    val startBarrier: manager: string -> barrier: string -> tree: string -> current: GuardHandle -> GuardHandle

    val applyVerdict: attempt: obj -> verdict: string -> current: GuardHandle -> obj

    val applyConfirmedWitness:
        barrier: string ->
        firstPhysical: string ->
        secondPhysical: string ->
        first: obj ->
        second: obj ->
        current: GuardHandle ->
            obj

    val guardView: current: GuardHandle -> obj

    val guardWitness: current: GuardHandle -> obj

    val hasObservedAttempt: attempt: obj -> current: GuardHandle -> bool

    val satisfiesGuard: tree: string -> current: GuardHandle -> bool

    val requirementsEmpty: unit -> RequirementsHandle

    val addRequirement: session: string -> authorityRoot: string -> current: RequirementsHandle -> RequirementsHandle

    val clearRequirements: providerRun: string -> current: RequirementsHandle -> RequirementsHandle

    val requirementsView: current: RequirementsHandle -> obj

    /// Provider run binding result. The raw Host message is adapted once here;
    /// SessionMessage never crosses the owner boundary.
    val bindableRun: physicalUser: string -> messages: obj array -> obj
