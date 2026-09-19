import assert from 'node:assert/strict'
import test from 'node:test'
import * as hooks from '../../../dist/OpenCode/Host/PluginHooksSurface.js'

test('WHAT[execution-failure-policy-011] unclassified hook failure preserves own evidence and issues settlement incomplete for proven execution', () => {
  // 1. When hook arguments prove execution identity (sessionID + messageID),
  // boundary issues SettlementIncomplete and preserves execution key
  const argsWithIdentity = {
    sessionID: 'ses-hook-011',
    messageID: 'msg-hook-011',
    messages: [{ id: 'msg-hook-011', role: 'user' }],
  }
  const context = {}
  const unknownError = new Error('unexpected hook crash')

  const outcomeWithIdentity = hooks.normalizeHookFailureOutcome(argsWithIdentity, context, unknownError)
  assert.equal(outcomeWithIdentity.hasExecutionKey, true)
  assert.equal(outcomeWithIdentity.settlement, 'SettlementIncomplete')

  // 2. When hook arguments cannot prove execution identity, execution key is None
  const argsWithoutIdentity = {}
  const outcomeWithoutIdentity = hooks.normalizeHookFailureOutcome(argsWithoutIdentity, context, unknownError)
  assert.equal(outcomeWithoutIdentity.hasExecutionKey, false)

  // 3. Unknown failure does not forge a clean execution outcome or claim no owned execution
  assert.notEqual(outcomeWithIdentity.settlement, 'ExactSettlementComplete')
  assert.notEqual(outcomeWithIdentity.settlement, 'NoOwnedExecution')
})
