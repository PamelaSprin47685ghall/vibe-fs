import { caseOf, payloadOf } from '../../../verification-system/tests/support/domain.mjs'

const { ReviewerOutcome } = await import('../../../../dist/Composition/Bridges/FinalityReview/FinalityReviewCohort.js')

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
  cases: () => Object.create(ReviewerOutcome.prototype).cases(),
}
