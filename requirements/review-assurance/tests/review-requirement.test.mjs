// REVIEW-ASSURANCE-013: review requirements are task identities, not wire-message identities.
// The production projection accepts AuthorityRootUserMessageId directly and keeps
// confirmation replay idempotent without erasing requirements that arrived later.

import assert from 'node:assert/strict'
import test from 'node:test'
import * as review from '../../../dist/Mission/Review/Assurance/Surface.js'

const session = 'ses-review-requirement'

test('WHAT[REVIEW-ASSURANCE-013] requirement identity is the Authority Root and duplicate roots collapse', () => {
  const rootA = 'root-task-a'
  const rootB = 'root-task-b'

  const first = review.addRequirement(session, rootA, review.requirementsEmpty())
  const duplicate = review.addRequirement(session, rootA, first)
  const distinctTask = review.addRequirement(session, rootB, first)

  assert.deepEqual(review.requirementsView(duplicate), review.requirementsView(first), 'the same Authority Root must not mint a second requirement')
  assert.notDeepEqual(review.requirementsView(distinctTask), review.requirementsView(first), 'a distinct Authority Root is a distinct review requirement')
})

test('WHAT[REVIEW-ASSURANCE-013] confirmation clears its covered batch but replay cannot clear a later requirement', () => {
  const root = 'root-task-reused-after-confirmation'
  const confirmingRun = 'run-confirmation'

  const covered = review.addRequirement(session, root, review.requirementsEmpty())
  const confirmed = review.clearRequirements(confirmingRun, covered)

  // Clearing the covered batch removes its dedupe key: the same task identity may
  // become a new requirement after that confirmation boundary.
  const later = review.addRequirement(session, root, confirmed)
  assert.notDeepEqual(review.requirementsView(later), review.requirementsView(confirmed), 'confirmation must clear the requirement it covered')

  // Replaying the same confirmation is a no-op. In particular it must not erase
  // the requirement that arrived after the original confirmation.
  const replayed = review.clearRequirements(confirmingRun, later)
  assert.deepEqual(review.requirementsView(replayed), review.requirementsView(later), 'confirmation replay must preserve later requirements')
})
