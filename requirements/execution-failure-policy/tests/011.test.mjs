import assert from 'node:assert/strict'
import test from 'node:test'
import * as hooks from '../../../dist/OpenCode/Host/PluginHooksSurface.js'

const args = {
  sessionID: 'ses-hook-011',
  messages: [{ id: 'msg-hook-011', role: 'user' }],
}

test('WHAT[execution-failure-policy-011] unknown failures retain incomplete settlement with or without an owned identity', () => {
  for (const [input, hasExecutionKey] of [
    [args, true], [{}, false], [{ sessionID: args.sessionID }, false],
    [{ sessionID: args.sessionID, messageID: 'unproven-message' }, false],
  ]) {
    const observed = hooks.normalizeHookFailureOutcome(input, {}, new Error('unexpected hook crash'))
    assert.equal(observed.hasExecutionKey, hasExecutionKey)
    assert.equal(observed.settlement, 'SettlementIncomplete')
    assert.equal(observed.lifecycle, 'AcceptedBeforeProvider')
  }
})

test('WHAT[execution-failure-policy-011] real hook membrane rethrows identical synchronous and asynchronous failures', async () => {
  for (const input of [args, {}]) {
    for (const error of [new Error('unexpected crash'), { reason: 'opaque Host rejection' }]) {
      const sync = hooks.policyAwareHook('execfail-011-sync', () => { throw error })
      assert.throws(() => sync(input, {}), (caught) => caught === error)
      const asyncHook = hooks.policyAwareHook('execfail-011-async', () => Promise.reject(error))
      await assert.rejects(asyncHook(input, {}), (caught) => caught === error)
    }
  }
})
