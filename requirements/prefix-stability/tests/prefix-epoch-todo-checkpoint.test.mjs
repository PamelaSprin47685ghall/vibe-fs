// PREFIX-STABILITY-004 / PREFIX-STABILITY-005 — TodoCheckpoint enters the SAME
// ActivePrefixEpoch contract. obligation-ledger supplies one O(1)
// PreviousCommittedCheckpoint locator; prefix-stability never scans the accepted
// history and never invents a todo-only rebase stage.

import assert from 'node:assert/strict'
import test from 'node:test'

import * as magicTodo from '../../../dist/Mission/Obligation/Todo/MagicTodoSemanticSurface.js'

const t = (id) => id

test('WHAT[PREFIX-STABILITY-004] PREFIX_STABILITY_lag1_rebase_consumes_one_previous_committed_locator', () => {
  assert.equal(magicTodo.requiresLag1Rebase(undefined), false, 'T1 has no committed predecessor')
  assert.equal(magicTodo.requiresLag1Rebase(t('T1')), true, 'later committed checkpoints have one lag-1 predecessor')
})

test('WHAT[PREFIX-STABILITY-004] PREFIX_STABILITY_todo_checkpoint_commit_uses_the_existing_epoch_contract', () => {
  const commit = magicTodo.buildTodoCheckpointCommit({
    sessionId: 'ses_1',
    managerLifeId: 'life-1',
    previousEpoch: 2,
    snapshot: {
      ref: 'blob-frozen',
      frozenDigest: 'frozen-1',
      cutoff: 5,
      prefixDigest: 'prefix-5',
      sealRoot: 'seal-5',
      syntheticId: 'synthetic-5',
    },
    previousCommitted: t('T1'),
    trigger: t('T2'),
    yBundleRef: 'blob-y',
    yBundleDigest: 'y-1',
    providerPrefixDigest: 'provider-prefix-digest',
  })

  assert.equal(commit.sessionId, 'ses_1')
  assert.equal(commit.managerLifeId, 'life-1')
  assert.equal(commit.previousEpoch, 2n)
  assert.equal(commit.nextEpoch, 3n)
  assert.deepEqual(commit.evidenceKind, {
    kind: 'TodoCheckpoint',
    triggerTodoWriteId: 'T2',
    coveredBeforeTodoWriteId: 'T1',
  })
  assert.equal(commit.cutoffExclusive, 5)
  assert.equal(commit.coveredPrefixDigest, 'prefix-5')
  assert.equal(commit.sealRoot, 'seal-5')
  assert.equal(commit.syntheticMessageId, 'synthetic-5')
  assert.equal(commit.frozenRecordPrefixRef, 'blob-frozen')
  assert.equal(commit.frozenRecordPrefixDigest, 'frozen-1')
  assert.equal(commit.yBundleRef, 'blob-y')
  assert.equal(commit.yBundleDigest, 'y-1')
  assert.equal(commit.providerPrefixDigest, 'provider-prefix-digest')
  assert.equal(commit.solvingProviderRun, null)
})
