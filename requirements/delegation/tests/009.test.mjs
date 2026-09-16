import assert from 'node:assert/strict'
import test from 'node:test'
import * as syncRuntime from '../../../dist/Execution/Delegation/SyncDelegateRuntimeSurface.js'

test('WHAT[DELEG-009] SYNC_RUNTIME_same_reuse_scope_serializes_distinct_provider_runs_but_distinct_scopes_are_independent', async () => {
  let active = 0
  let maxActiveSameScope = 0
  const runInScope = async (scope) => {
    return syncRuntime.runSerialized(scope, async () => {
      active += 1
      if (active > maxActiveSameScope) maxActiveSameScope = active
      await new Promise((r) => setImmediate(r))
      active -= 1
    })
  }
  await Promise.all([runInScope('scope-A'), runInScope('scope-A')])
  assert.equal(maxActiveSameScope, 1)
})
