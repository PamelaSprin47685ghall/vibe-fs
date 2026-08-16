// PREFIX-STABILITY-004 / PREFIX-STABILITY-005 — TodoCheckpoint enters the SAME
// ActivePrefixEpoch contract. obligation-ledger supplies one O(1)
// PreviousCommittedCheckpoint locator; prefix-stability never scans the accepted
// history and never invents a todo-only rebase stage.

import assert from 'node:assert/strict'
import test from 'node:test'
import {
  magicTodo,
  sessionId,
  managerLifeId,
  prefixEpochId,
  blobRef,
  blobDigest,
  idValue,
} from '../../verification-system/tests/support/domain.mjs'

const { requiresLag1Rebase, buildTodoCheckpointCommit } = await import(
  '../../../dist/Mission/Obligation/Todo/PrefixEpoch.js'
)

const t = (id) => magicTodo.todoWriteIdCreate(id)

test('WHAT[PREFIX-STABILITY-004] PREFIX_STABILITY_lag1_rebase_consumes_one_previous_committed_locator', () => {
  assert.equal(requiresLag1Rebase(undefined), false, 'T1 has no committed predecessor')
  assert.equal(requiresLag1Rebase(t('T1')), true, 'later committed checkpoints have one lag-1 predecessor')
})

test('WHAT[PREFIX-STABILITY-004] PREFIX_STABILITY_todo_checkpoint_commit_uses_the_existing_epoch_contract', () => {
  const snapshot = {
    FrozenRecordPrefixRef: blobRef('blob-frozen'),
    FrozenRecordPrefixDigest: blobDigest('frozen-1'),
    CutoffExclusive: 5,
    CoveredPrefixDigest: 'prefix-5',
    SealRoot: 'seal-5',
    SyntheticMessageId: 'synthetic-5',
  }

  const commit = buildTodoCheckpointCommit(
    sessionId('ses_1'),
    managerLifeId('life-1'),
    prefixEpochId(2),
    snapshot,
    t('T1'),
    t('T2'),
    blobRef('blob-y'),
    blobDigest('y-1'),
    'provider-prefix-digest',
  )

  assert.equal(idValue.session(commit.SessionId), 'ses_1')
  assert.equal(idValue.prefixEpoch(commit.PreviousEpochId), 2n)
  assert.equal(idValue.prefixEpoch(commit.NextEpochId), 3n)
  assert.equal(commit.EvidenceKind.tag, 1, 'EvidenceKind is TodoCheckpoint')
  assert.equal(magicTodo.todoWriteIdValue(commit.EvidenceKind.fields[0]), 'T2')
  assert.equal(magicTodo.todoWriteIdValue(commit.EvidenceKind.fields[1]), 'T1')
  assert.equal(commit.CutoffExclusive, 5)
  assert.equal(commit.CoveredPrefixDigest, 'prefix-5')
  assert.equal(commit.SealRoot, 'seal-5')
  assert.equal(commit.SyntheticMessageId, 'synthetic-5')
  assert.equal(idValue.blobRef(commit.YBundleRef), 'blob-y')
  assert.equal(idValue.blobDigest(commit.YBundleDigest), 'y-1')
  assert.equal(commit.ProviderPrefixDigest, 'provider-prefix-digest')
  assert.equal(commit.SolvingProviderRun, undefined)
})
