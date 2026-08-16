import assert from 'node:assert/strict'
import test from 'node:test'
import { hostCompaction, sha256Hex, hostSignals } from './support/host-surface.mjs'

test('WHAT[HOST-BOUNDARY-007] HOST_006_prevention_requires_compaction_settings_off_and_autocontinue_off', () => {
  assert.deepEqual(hostCompaction.settingPaths, ['compaction.auto', 'compaction.prune', 'compaction.autocontinue'])
  assert.deepEqual(hostCompaction.settings.map((setting) => setting.value), [false, false, false])
})

test('WHAT[HOST-BOUNDARY-007] HOST_006_first_turn_probe_is_the_only_startup_verdict', () => {
  assert.deepEqual(hostCompaction.judgeFirstTurn({ pseudoRuns: 0 }), { name: 'Satisfied' })
  assert.deepEqual(hostCompaction.judgeFirstTurn({ pseudoRuns: 1 }), { name: 'Unsupported' })
})

test('WHAT[HOST-BOUNDARY-007] HOST_006_containment_folds_observation_and_reanchors_newest_unhandled_once', () => {
  assert.equal(hostCompaction.isContainableCompaction(true), true)
  assert.deepEqual(hostCompaction.nextReanchor({ handled: false, newest: 8 }), { kind: 'ContextReanchored', newest: 8 })
  assert.deepEqual(hostCompaction.nextReanchor({ handled: true, newest: 8 }), { kind: 'AlreadyHandled' })
})

test('WHAT[HOST-BOUNDARY-003] HOST_003_host_signal_is_a_typed_wake_never_a_fact_carrier', () => {
  const retry = hostSignals.tryDecode({ type: 'session.status', sessionID: 'ses_retry', properties: { status: { type: 'retry', attempt: 3, reason: 'again' } } })
  assert.equal(retry.kind, 'ProviderRetry')
  assert.equal(retry.sessionId, 'ses_retry')
  assert.equal('messageId' in retry, false)
})

test('WHAT[HOST-BOUNDARY-019] HOST_DIGEST_single_deterministic_sha256_for_durable_identity', () => {
  assert.equal(sha256Hex('hello'), '2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824')
  assert.equal(sha256Hex('hello'), sha256Hex('hello'))
  assert.equal(sha256Hex('').length, 64)
})
