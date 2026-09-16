import assert from 'node:assert/strict'
import test from 'node:test'
import * as proj from '../../../dist/OpenCode/Host/MagicTodoProjectionSurface.js'
import * as magic from '../../../dist/OpenCode/Host/MagicTodoSurface.js'

test('WHAT[OBLIGATION-LEDGER-008] rejects Accepted when it names another Prepared envelope', () => {
  assert.equal(proj.testRejectAcceptedDifferentPrepared(), true)
})

test('WHAT[OBLIGATION-LEDGER-008] rejects a replay whose frozen prepared identity differs', () => {
  assert.equal(proj.testRejectReplayDifferingIdentity(), true)
})

test('WHAT[OBLIGATION-LEDGER-008] pure replay identity checker detects corruption for the Host fatal boundary', () => {
  assert.equal(magic.testPureReplayCorruptionDetection(), true)
})
