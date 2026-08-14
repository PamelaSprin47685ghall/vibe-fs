// host-boundary: the minimal, verifiable Host capability contract.
//
// HOST-006: the compaction observation gate — prevention (config keys that must
// be off, verified by the first-turn probe) + containment (folded
// is-containable-compaction predicate, newest-unhandled reanchor, no source
// discrimination). HOST-002/003: typed HostSignal is a wake, not a fact carrier
// (no message id on any case). HostDigest is the single deterministic digest
// function durable facts depend on.

import assert from 'node:assert/strict'
import test from 'node:test'

import { hostCompaction, sessionId } from '../../../tests/unit/support/domain.mjs'

const { sha256Hex } = await import('../../../dist/Host/HostDigest.js')
const { HostSignal, RetrySignal } = await import('../../../dist/Infrastructure/OpenCode/Signals/HostSignal.js')

test('HOST_006_prevention_requires_compaction_settings_off_and_autocontinue_off', () => {
  // Three keys close the four behaviours: compaction.auto closes both threshold
  // overflow and provider-error compaction; prune deletes persisted rows
  // (COMPANION-009); autocontinue injects an unclaimed synthetic turn.
  assert.deepEqual(hostCompaction.settingPaths, ['compaction.auto', 'compaction.prune', 'compaction.autocontinue'])
  for (const setting of hostCompaction.settings) {
    assert.equal(setting.required, false, `${setting.path} must be forced off`)
    assert.ok(setting.clause === 'HOST-006' || setting.clause === 'COMPANION-009')
  }
  assert.equal(hostCompaction.autoContinueEnabled, false)
})

test('HOST_006_first_turn_probe_is_the_only_startup_verdict', () => {
  const session = sessionId('ses_probe')
  const satisfied = hostCompaction.judgeFirstTurn({ session, pseudoRuns: 0 })
  assert.equal(satisfied.name, 'Satisfied')

  // A first turn is far below any threshold: a compaction there is a contract
  // violation, not a legitimate user /compact.
  const compacted = hostCompaction.judgeFirstTurn({ session, pseudoRuns: 1 })
  assert.equal(compacted.name, 'CompactedDespiteSettings')
  assert.match(compacted.message, /HostContractUnsupported/)

  // A setting the Host cannot reach takes priority over the probe result.
  const unavailable = hostCompaction.judgeFirstTurn({ unavailable: 'compaction.auto', session, pseudoRuns: 0 })
  assert.equal(unavailable.name, 'SettingUnavailable')
  assert.match(unavailable.message, /HostContractUnsupported: compaction\.auto/)
})

test('HOST_006_containment_folds_observation_and_reanchors_newest_unhandled_once', () => {
  // The three raw fields fold into one predicate at the snapshot boundary; a
  // caller re-deriving it from raw fields would be a second definition.
  assert.equal(hostCompaction.isContainableCompaction(true), true)
  assert.equal(hostCompaction.isContainableCompaction(false), false)

  const observed = ['run_1', 'run_2', 'run_3']
  assert.equal(hostCompaction.nextReanchor(observed), 'run_3', 'newest unhandled wins')
  assert.equal(hostCompaction.nextReanchor(observed, ['run_1', 'run_2']), 'run_3')
  assert.equal(hostCompaction.nextReanchor(observed, ['run_1', 'run_2', 'run_3']), undefined, 'nothing left to reanchor')
})

test('HOST_003_host_signal_is_a_typed_wake_never_a_fact_carrier', () => {
  // RetrySignal carries the Host's own retry counter for diagnostics and wake
  // routing only — no message id, no attempt outcome. HostSignal cases are typed
  // wakes; business facts come from the reconciled snapshot.
  const retry = new RetrySignal(sessionId('ses_r'), '3', 'rate limited')
  const signal = new HostSignal(1, [retry]) // ProviderRetry
  assert.equal(signal.tag, 1)
  assert.equal(signal.fields[0].SessionId.fields[0], 'ses_r')
  assert.equal(signal.fields[0].Attempt, '3', 'Attempt is the Host counter string, not a domain count')
  assert.equal(signal.fields[0].Reason, 'rate limited')
})

test('HOST_DIGEST_single_deterministic_sha256_for_durable_identity', () => {
  assert.equal(sha256Hex('hello'), '2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824')
  assert.equal(sha256Hex('hello'), sha256Hex('hello'), 'deterministic')
  assert.equal(sha256Hex('').length, 64, 'lowercase hex')
  assert.notEqual(sha256Hex('hello'), sha256Hex('world'))
})
