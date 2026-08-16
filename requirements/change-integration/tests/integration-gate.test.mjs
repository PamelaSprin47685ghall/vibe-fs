// tests/unit/git/integration-gate.test.mjs — VERIFY-009 coverage: publish lock.
//
// Real proper-lockfile over per-test temp files: lockPath stability, acquire/release,
// and the cross-instance mutual exclusion the gate exists for.

import assert from 'node:assert/strict'
import { mkdtempSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'

const {
  IntegrationGateModule_acquire,
  IntegrationGateModule_lockPath,
  IntegrationGate__Release,
} = await import('../../../dist/Git/IntegrationGate.js')

const sandbox = () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-gate-'))
  return { dir, cleanup: () => rmSync(dir, { recursive: true, force: true }) }
}

test('WHAT[CHGINT-004] GATE_lock_path_is_stable_per_repo_and_branch', () => {
  const first = IntegrationGateModule_lockPath('/repo/a', 'main')
  const second = IntegrationGateModule_lockPath('/repo/a', 'main')
  const otherBranch = IntegrationGateModule_lockPath('/repo/a', 'dev')
  const otherRepo = IntegrationGateModule_lockPath('/repo/b', 'main')

  assert.equal(first, second, 'same repo+branch must map to the same lock file')
  assert.notEqual(first, otherBranch)
  assert.notEqual(first, otherRepo)
  assert.match(first, /wanxiangshu-publish-[0-9a-f]{64}$/)
})

test('WHAT[CHGINT-004] GATE_acquire_and_release_round_trips', async () => {
  const { dir, cleanup } = sandbox()
  const lockTarget = join(dir, 'target.lock')
  writeFileSync(lockTarget, '')

  const gate = await IntegrationGateModule_acquire(lockTarget)
  await IntegrationGate__Release(gate)
  await IntegrationGate__Release(gate) // idempotent

  // After release the same path can be locked again immediately.
  const second = await IntegrationGateModule_acquire(lockTarget)
  await IntegrationGate__Release(second)
  cleanup()
})

test('WHAT[CHGINT-004] GATE_dispose_releases_the_lock', async () => {
  const { dir, cleanup } = sandbox()
  const lockTarget = join(dir, 'target.lock')
  writeFileSync(lockTarget, '')

  const gate = await IntegrationGateModule_acquire(lockTarget)
  await gate['System.IAsyncDisposable.DisposeAsync']()

  const second = await IntegrationGateModule_acquire(lockTarget)
  await IntegrationGate__Release(second)
  cleanup()
})

test('WHAT[CHGINT-004] GATE_second_acquire_on_held_lock_eventually_fails', async () => {
  const { dir, cleanup } = sandbox()
  const lockTarget = join(dir, 'target.lock')
  writeFileSync(lockTarget, '')

  const gate = await IntegrationGateModule_acquire(lockTarget)

  // proper-lockfile retries (50 × ≤500ms) — a competing acquire must not succeed
  // while the first holder keeps the lock. We only assert it does NOT resolve
  // quickly with success.
  let settled = false
  const competing = IntegrationGateModule_acquire(lockTarget).then(
    () => {
      settled = 'acquired'
    },
    () => {
      settled = 'failed'
    },
  )

  await new Promise((resolve) => setTimeout(resolve, 300))
  assert.equal(settled, false, 'a held lock must not be acquired concurrently')

  await IntegrationGate__Release(gate)
  await competing.catch(() => {})
  cleanup()
})
