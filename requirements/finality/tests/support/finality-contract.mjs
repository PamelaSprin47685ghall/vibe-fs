// Finality family support contract: the one place finality semantic tests may
// reach the compiled dist modules they exercise. Mirrors the per-package
// support-contract pattern used across the requirements tree (e.g.
// output-distillation/tests/support/distiller-contract.mjs): the dist import
// knowledge stays inside this support file, tests only read semantic handles.
//
// The shared facade (verification-system/tests/support/domain/interop.mjs) does
// not load these modules, so this package owns the boundary here.

import { caseOf, payloadOf } from '../../../verification-system/tests/support/domain.mjs'

const {
  ReviewerOutcome,
  rosterOf,
  graduatedReviewer,
} = await import('../../../../dist/Composition/Bridges/FinalityReview/FinalityReviewCohort.js')
const { admitLabor, classifyEnding, EndingDisposition, LaborAdmission } = await import(
  '../../../../dist/Mission/Manager/Finality.js'
)
const { ManagerLifeAdmission_ending, ManagerLifeAdmission_tryHumanRootOpening } = await import(
  '../../../../dist/Mission/Manager/Life/Admission.js'
)
const { ManagerLifecycleProjection_isLifeArchived } = await import(
  '../../../../dist/Mission/Manager/Life/Projection.js'
)

export const finalityContract = {
  endingName: (ending) => caseOf(ending),
}

export const reviewerOutcomeContract = {
  revision: (workRecord) => {
    const value = new ReviewerOutcome(0, [workRecord])
    return { name: caseOf(value), workRecord: payloadOf(value) }
  },
  confirmed: (reviewer, barrier) => {
    const value = new ReviewerOutcome(1, [reviewer, barrier])
    return { name: caseOf(value) }
  },
  // Enumerate the two ReviewerOutcome cases by name (Fable `cases()` stays
  // inside the support contract; tests only read the name list).
  caseNames: () => Object.create(ReviewerOutcome.prototype).cases(),
}

/** FINALITY-* disposition algebra (Mission/Manager/Finality.fs). */
export const finalityDisposition = {
  admitLabor,
  classifyEnding,
  EndingDisposition,
  LaborAdmission,
}

/** FINALITY-022 Life admission (Mission/Manager/Life/Admission.fs). */
export const lifeAdmission = {
  ending: ManagerLifeAdmission_ending,
  tryHumanRootOpening: ManagerLifeAdmission_tryHumanRootOpening,
}

/** GLORY-070 archived-life decision (Mission/Manager/Life/Projection.fs). */
export const lifeProjection = {
  isLifeArchived: ManagerLifecycleProjection_isLifeArchived,
}

/** FINALITY-009/010 cohort roster algebra (FinalityReviewCohort.fs). */
export const finalityCohort = {
  rosterOf,
  graduatedReviewer,
}
