import assert from 'node:assert/strict'
import test from 'node:test'

import * as change from '../../../dist/Change/Surface.js'

const job = 'job-reentry'
const identity = 'manager/job-reentry'
const path = '/repo/.worktrees/job-reentry'

const decide = (evidence) =>
  change.worktreeReconciliationDecision(job, identity, path, evidence)

const requested = (entries) => ({
  kind: 'RequestedEntries',
  jobId: job,
  path,
  entries,
})

test('WHAT[CHGINT-006] PERSIST_009_worktree_requested_created_reentry_is_finite_and_fail_closed', () => {
  // Fresh entry records intent before the ordinary fork CE creates anything.
  assert.deepEqual(decide({ kind: 'NoDurableEffect' }), { kind: 'RequestThenCreate' })

  // Crash after Requested but before the physical effect: a complete empty
  // observation proves that creation is safe on retry.
  assert.deepEqual(decide(requested([])), { kind: 'CreateAfterProvenMissing' })

  // Crash after physical create but before Created: exact identity + path is
  // adopted and the missing receipt is recorded, never recreated.
  assert.deepEqual(
    decide(requested([{ path, identity }])),
    { kind: 'AdoptThenRecordCreated' },
  )

  // Either half of the physical identity/path key conflicting fails closed.
  assert.deepEqual(
    decide(requested([{ path: '/repo/.worktrees/elsewhere', identity }])),
    { kind: 'Reject', reason: 'PhysicalIdentityPathConflict' },
  )
  assert.deepEqual(
    decide(requested([{ path, identity: 'manager/another-job' }])),
    { kind: 'Reject', reason: 'PhysicalIdentityPathConflict' },
  )

  // No guessed success or unsafe retry is available when the physical query fails.
  assert.deepEqual(
    decide({ kind: 'RequestedQueryFailure', jobId: job, path, error: 'git unavailable' }),
    { kind: 'Reject', reason: 'WorktreeQueryFailed' },
  )

  // Created is already the durable receipt: reentry adopts without another query.
  assert.deepEqual(
    decide({ kind: 'CreatedReceipt', jobId: job, path }),
    { kind: 'AdoptCreated' },
  )

  // Durable intent cannot be stolen by another job or redirected to another path;
  // this state is rejected before any physical query is admitted.
  assert.deepEqual(
    decide({ kind: 'RequestedConflict', jobId: 'job-other', path }),
    { kind: 'Reject', reason: 'DurableOwnershipConflict' },
  )
  assert.deepEqual(
    decide({ kind: 'CreatedReceipt', jobId: 'job-other', path }),
    { kind: 'Reject', reason: 'DurableOwnershipConflict' },
  )
})
