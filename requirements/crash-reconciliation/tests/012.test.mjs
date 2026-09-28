import assert from 'node:assert/strict'
import test from 'node:test'
import * as handles from '../../../dist/Execution/Delegation/Handle/Surface.js'

test('WHAT[crash-reconciliation-012] handle projection keeps retired completion unavailable after repeated retire and late completion', () => {
  for (const scenario of ['retired', 'replayed-retired']) {
    const state = handles.crashScenario(scenario)
    assert.equal(state.lifecycle, 'Retired')
    assert.equal(state.joinable, 0)
    assert.equal(state.retired, true)
  }
})

test.todo('WHAT[crash-reconciliation-012] actual completion owner commits blob before fact rejects duplicate claims and preserves single delivery through crash cuts (GAP-149)')
