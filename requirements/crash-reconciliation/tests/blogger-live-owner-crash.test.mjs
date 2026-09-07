// requirements/crash-reconciliation/tests/blogger-live-owner-crash.test.mjs — WHAT[CRASH-016]
//
// Blogger 修复只属于当前进程的 live owner：nudge/AABB 修复 episode、等待者与
// flight lease 均为 process-local 物理所有权。进程死亡（owner scope 释放）后
// 这些能力消失；新进程的 scope 从空开始，只能重新准入，不能继承旧 episode。
import test from 'node:test'
import assert from 'node:assert/strict'
import * as runtime from '../../../dist/Context/Companion/RuntimeSurface.js'

test('WHAT[CRASH-016] CRASH_016_blogger_flight_lease_dies_with_the_process_scope', async () => {
  const key = 'ses-blogger-crash-016'
  const dying = runtime.scope()
  assert.equal(runtime.claimCurrentRequest(dying, key, runtime.main({ toml: 'live-episode' })), 'Claimed')
  assert.notEqual(runtime.tryGetFlight(dying, key), null)
  const waiter = runtime.park(dying, key)

  // 进程死亡：释放 owner scope，等待者被取消，flight 不再存在。
  runtime.dispose(dying)
  assert.equal((await waiter).kind, 'Cancelled')

  // 新进程：同一 session 在新 scope 下没有任何继承来的 episode、等待者或 lease，
  // 只能以全新材料重新准入。
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
