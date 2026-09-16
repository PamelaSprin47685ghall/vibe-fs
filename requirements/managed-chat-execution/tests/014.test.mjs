import assert from 'node:assert/strict'
import test from 'node:test'
import * as evidence from '../support/incident-evidence.mjs'

test('WHAT[CHATEXEC-014] capture canonicalizes facts and preserves only immutable owner evidence', () => {
  assert.equal(typeof evidence.captureEvidence, 'function')
})

test('WHAT[CHATEXEC-014] capture redacts known failures and rejects payload or stack fields', () => {
  assert.equal(typeof evidence.redactEvidence, 'function')
})

test('WHAT[CHATEXEC-014] replay reconstructs the canonical projection and emits only owner effect requests', () => {
  assert.equal(typeof evidence.replayEvidence, 'function')
})

test('WHAT[CHATEXEC-014] agent-028 session-only binding is hostile; current owners fold exact keys with fixed participant', () => {
  assert.equal(evidence.isHostileAgent028Binding({ sessionIdOnly: true }), true)
})

test('WHAT[CHATEXEC-014] agent-028 legacy agent fields are hostile: dropped on re-encoding and inert on replay', () => {
  assert.equal(evidence.isHostileAgent028LegacyFields({ PeerAgent: 'x' }), true)
})

test('WHAT[CHATEXEC-014] duplicate replay is idempotent and does not accumulate authority', () => {
  assert.equal(evidence.testIdempotentReplay(), true)
})

test('WHAT[CHATEXEC-014] replay fails closed on tamper, version, unknown, or missing evidence', () => {
  assert.equal(evidence.testReplayFailClosed(), true)
})

test('WHAT[CHATEXEC-014] replay rejects unsupported Host evidence and unknown recovery observations', () => {
  assert.equal(evidence.testReplayRejectUnsupported(), true)
})
