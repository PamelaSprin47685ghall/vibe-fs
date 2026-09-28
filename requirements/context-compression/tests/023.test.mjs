import assert from 'node:assert/strict'
import test from 'node:test'
import * as runtime from '../../../dist/Context/Companion/RuntimeSurface.js'

test('WHAT[context-compression-023] park wakes with a typed material event', async (t) => {
  const scope = runtime.scope()
  t.after(() => runtime.dispose(scope))
  const parked = runtime.park(scope, 'ses-blog')
  assert.equal(runtime.offerParked(scope, 'ses-blog', runtime.main({ toml: 'fresh' })), 'Delivered')
  const wake = await parked
  assert.equal(wake.kind, 'MaterialAvailable')
  assert.equal(wake.context.toml, 'fresh')
})

test.todo('WHAT[context-compression-023] actual retry waits on committed open-request changes and ends only on new coverage or exact settlement, with no clock dependency; GAP-104')
