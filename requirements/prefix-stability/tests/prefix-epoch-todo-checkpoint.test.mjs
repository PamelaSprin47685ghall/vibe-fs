// PREFIX-STABILITY-004 / PREFIX-STABILITY-005 — TodoCheckpoint enters the SAME
// ActivePrefixEpoch contract (CTX-015 / TODO-009): one epoch SSOT, no parallel
// todo-only epoch, no NeedRebase/RebaseRequested Stage.
//
// `MagicTodoPrefixEpoch` derives the lag-1 desired cutoff purely from the
// Accepted chain and assembles the SAME `PrefixRebaseCommittedV2` shape the
// probe path uses — EvidenceKind=TodoCheckpoint(Tk, coveredBefore) is the only
// difference; provider success/failure never rolls a sealed epoch back.

import assert from 'node:assert/strict'
import test from 'node:test'
import {
  toList,
  magicTodo,
  sessionId,
  managerLifeId,
  prefixEpochId,
  blobRef,
  blobDigest,
  idValue,
} from '../../../tests/unit/support/domain.mjs'

const { coveredBefore, requiresLag1Rebase, buildTodoCheckpointCommit } = await import(
  '../../../dist/Domain/MagicTodoPrefixEpoch.js'
)

const t = (id) => magicTodo.todoWriteIdCreate(id)
const ids = (xs) => toList(xs.map(t))

test('PREFIX_STABILITY_desired_cutoff_is_derived_from_the_accepted_chain_only', () => {
  // desiredCutoff(Tk) = Before(T(k-1)) — T1 has no prior.
  assert.equal(coveredBefore(ids(['T1']), t('T1')), undefined, 'T1 → no covered-before id')
  assert.equal(magicTodo.todoWriteIdValue(coveredBefore(ids(['T1', 'T2']), t('T2'))), 'T1', 'T2 → T1')
  assert.equal(magicTodo.todoWriteIdValue(coveredBefore(ids(['T1', 'T2', 'T3']), t('T3'))), 'T2', 'T3 → T2')
  assert.equal(coveredBefore(ids([]), t('T1')), undefined, 'empty chain → nothing')
})

test('PREFIX_STABILITY_lag1_rebase_is_mandatory_only_after_the_second_accepted', () => {
  assert.equal(requiresLag1Rebase(ids([])), false)
  assert.equal(requiresLag1Rebase(ids(['T1'])), false, 'T1 has no prior → no TodoCheckpoint replacement')
  assert.equal(requiresLag1Rebase(ids(['T1', 'T2'])), true)
  assert.equal(requiresLag1Rebase(ids(['T1', 'T2', 'T3'])), true)
})

test('PREFIX_STABILITY_todo_checkpoint_commit_uses_the_existing_epoch_contract', () => {
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
    ids(['T1', 'T2']),
    t('T2'),
    blobRef('blob-y'),
    blobDigest('y-1'),
    'provider-prefix-digest',
  )

  // Same record shape as a probe commit (PrefixRebaseCommittedV2), one epoch SSOT:
  assert.equal(idValue.session(commit.SessionId), 'ses_1')
  assert.equal(idValue.prefixEpoch(commit.PreviousEpochId), 2n)
  assert.equal(idValue.prefixEpoch(commit.NextEpochId), 3n, 'epoch advances by exactly one')

  // EvidenceKind = TodoCheckpoint(Tk, coveredBefore) — the ONLY difference from
  // the probe path; no parallel todo-only epoch.
  assert.equal(commit.EvidenceKind.tag, 1, 'EvidenceKind is TodoCheckpoint')
  assert.equal(magicTodo.todoWriteIdValue(commit.EvidenceKind.fields[0]), 'T2')
  assert.equal(magicTodo.todoWriteIdValue(commit.EvidenceKind.fields[1]), 'T1')

  // CTX-015 field parity with the probe path: snapshot identity is carried
  // whole (SealRoot/SyntheticMessageId — the next request continues the same
  // prefix instead of paying a second cold boundary).
  assert.equal(commit.CutoffExclusive, 5)
  assert.equal(commit.CoveredPrefixDigest, 'prefix-5')
  assert.equal(commit.SealRoot, 'seal-5')
  assert.equal(commit.SyntheticMessageId, 'synthetic-5')

  // The Y bundle is PrefixCoverage-proven (never LWR RawGap) and the provider
  // prefix digest rides on the fact; SolvingProviderRun stays None — the epoch
  // is committed before the attempt seals, so provider success/failure cannot
  // roll it back (PREFIX-STABILITY-005).
  assert.equal(idValue.blobRef(commit.YBundleRef), 'blob-y')
  assert.equal(idValue.blobDigest(commit.YBundleDigest), 'y-1')
  assert.equal(commit.ProviderPrefixDigest, 'provider-prefix-digest')
  assert.equal(commit.SolvingProviderRun, undefined)
})
