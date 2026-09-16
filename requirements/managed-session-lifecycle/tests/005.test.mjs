import assert from 'node:assert/strict'
import test from 'node:test'
import * as attached from '../../../dist/Execution/Session/AttachedSessionRuntimeSurface.js'

test('WHAT[MANAGED-SESSION-005] attached runtime enforces exclusive session lifecycle', async () => {
  const r = await attached.testExclusiveLifecycle()
  assert.equal(r.exclusive, true)
})
