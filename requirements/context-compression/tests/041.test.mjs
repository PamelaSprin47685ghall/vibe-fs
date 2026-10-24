import test from 'node:test'
import assert from 'node:assert/strict'
import { resetScopeState, clearSessionState, turnBudget, isScopeInitialized } from '../../../dist/Context/Scope/Surface.js'
import { withExecutablePlugin } from '../../verification-system/tests/support/plugin-fixture.mjs'

test('WHAT[context-compression-041] OpenCode session.deleted releases only its own scope actor', async () => {
  await withExecutablePlugin(async (hooks) => {
    const closed = 'ses_deleted_scope_041'
    const active = 'ses_active_scope_041'
    clearSessionState(closed)
    clearSessionState(active)
    resetScopeState(closed, 5)
    resetScopeState(active, 7)
    assert.equal(turnBudget(closed), 5)
    await hooks.event({event: {type: 'session.deleted', properties: {sessionID: closed}}})
    assert.equal(turnBudget(closed), 0)
    assert.equal(isScopeInitialized(closed), false)
    assert.equal(turnBudget(active), 7, 'other session actor must not be touched')
    clearSessionState(active)
  })
})
