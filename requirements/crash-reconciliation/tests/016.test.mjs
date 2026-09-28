import test from 'node:test'
import assert from 'node:assert/strict'
import * as runtime from '../../../dist/Context/Companion/RuntimeSurface.js'



test('WHAT[crash-reconciliation-016] explicit scope disposal cancels its waiter and a fresh scope has no inherited flight', async () => {
  const key = 'ses-blogger-crash-016'
  const dying = runtime.scope()
  assert.equal(runtime.claimCurrentRequest(dying, key, runtime.main({ toml: 'live-episode' })), 'Claimed')
  assert.notEqual(runtime.tryGetFlight(dying, key), null)
  const waiter = runtime.park(dying, key)

  // 当前进程主动释放该 scope，不冒充 OS crash。
  runtime.dispose(dying)
  assert.equal((await waiter).kind, 'Cancelled')

  // 同一进程中的新 scope 使用新材料重新准入。
  const reborn = runtime.scope()
  try {
    assert.equal(runtime.tryGetFlight(reborn, key), null)
    assert.equal(runtime.peekCurrentRequest(reborn, key), null)
    assert.equal(runtime.claimCurrentRequest(reborn, key, runtime.main({ toml: 'fresh-admission' })), 'Claimed')
    assert.equal(runtime.tryGetFlight(reborn, key)?.toml, 'fresh-admission')
  } finally {
    runtime.dispose(reborn)
  }
})

test.todo('WHAT[crash-reconciliation-016] actual process restart with durable Blogger dispatch never rebuilds an old repair episode or sends it automatically (GAP-149)')
