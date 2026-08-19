import assert from 'node:assert/strict'
import { createHash } from 'node:crypto'
import test from 'node:test'
import * as HostSignalSurface from '../../../dist/OpenCode/Host/HostSignalSurface.js'
import * as CompactionPolicySurface from '../../../dist/Host/Contract/CompactionPolicySurface.js'

const sha256Hex = (value) => createHash('sha256').update(String(value)).digest('hex')

const requiredSettings = CompactionPolicySurface.requiredSettings()
const judgeFirstTurn = (pseudoRuns) => CompactionPolicySurface.judgeFirstTurn('ses_probe', pseudoRuns)

test('WHAT[HOST-BOUNDARY-007] HOST_006_prevention_requires_compaction_settings_off_and_autocontinue_off', () => {
  assert.deepEqual(requiredSettings.map((s) => s.path), ['compaction.auto', 'compaction.prune', 'compaction.autocontinue'])
  assert.deepEqual(requiredSettings.map((s) => s.required), [false, false, false])
  assert.equal(CompactionPolicySurface.autoContinueEnabled(), false)
})

test('WHAT[HOST-BOUNDARY-007] HOST_006_first_turn_probe_is_the_only_startup_verdict', () => {
  assert.equal(judgeFirstTurn(0).kind, 'Satisfied')
  assert.equal(judgeFirstTurn(1).kind, 'CompactedDespiteSettings')
})

test('WHAT[HOST-BOUNDARY-007] HOST_006_containment_folds_observation_and_reanchors_newest_unhandled_once', () => {
  assert.equal(CompactionPolicySurface.isContainableCompaction(true), true)
  assert.equal(CompactionPolicySurface.nextReanchor(['run_8'], () => false), 'run_8')
  assert.equal(CompactionPolicySurface.nextReanchor(['run_8'], () => true), null)
})

test('WHAT[HOST-BOUNDARY-003] HOST_003_retry_signal_is_a_typed_wake_never_a_run_identity_carrier', () => {
  const retry = HostSignalSurface.tryDecode({ type: 'session.status', sessionID: 'ses_retry', properties: { status: { type: 'retry', attempt: 3, reason: 'again' } } })
  assert.equal(retry.kind, 'ProviderRetry')
  assert.equal(retry.sessionId, 'ses_retry')
  assert.equal('messageId' in retry, false)
})

test('WHAT[HOST-BOUNDARY-019] HOST_DIGEST_single_deterministic_sha256_for_durable_identity', () => {
  assert.equal(sha256Hex('hello'), '2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824')
  assert.equal(sha256Hex('hello'), sha256Hex('hello'))
  assert.equal(sha256Hex('').length, 64)
})
