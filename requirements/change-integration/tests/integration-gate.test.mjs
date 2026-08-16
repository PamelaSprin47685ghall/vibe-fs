// CHGINT-004 — the integration gate serializes only the ref mutation window.

import assert from 'node:assert/strict'
import { mkdtempSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'

const change = await import('../../../dist/Change/Surface.js')

const sandbox = () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-gate-'))
  return { dir, cleanup: () => rmSync(dir, { recursive: true, force: true }) }
}

test('WHAT[CHGINT-004] GATE_lock_path_is_stable_per_repo_and_branch', () => {
  const first = change.lockPath('/repo/a', 'main')
  const second = change.lockPath('/repo/a', 'main')
  const otherBranch = change.lockPath('/repo/a', 'dev')
  const otherRepo = change.lockPath('/repo/b', 'main')

  assert.equal(first, second, 'same repo+branch must map to the same lock file')
  assert.notEqual(first, otherBranch)
  assert.notEqual(first, otherRepo)
  assert.match(first, /wanxiangshu-publish-[0-9a-f]{64}$/)
})

test('WHAT[CHGINT-004] GATE_acquire_and_release_round_trips', async () => {
  const { dir, cleanup } = sandbox()
  const lockTarget = join(dir, 'target.lock')
  writeFileSync(lockTarget, '')

  const gate = await change.acquireGate(lockTarget)
  await change.releaseGate(gate)
  await change.releaseGate(gate)

  const second = await change.acquireGate(lockTarget)
  await change.releaseGate(second)
  cleanup()
})

test('WHAT[CHGINT-004] GATE_dispose_releases_the_lock', async () => {
  const { dir, cleanup } = sandbox()
  const lockTarget = join(dir, 'target.lock')
  writeFileSync(lockTarget, '')

  const gate = await change.acquireGate(lockTarget)
  await change.disposeGate(gate)

  const second = await change.acquireGate(lockTarget)
  await change.releaseGate(second)
  cleanup()
})

test('WHAT[CHGINT-004] GATE_second_acquire_on_held_lock_eventually_fails', async () => {
  const { dir, cleanup } = sandbox()
  const lockTarget = join(dir, 'target.lock')
  writeFileSync(lockTarget, '')

  const gate = await change.acquireGate(lockTarget)
  let settled = false
  const competing = change.acquireGate(lockTarget).then(
    () => {
      settled = 'acquired'
    },
    () => {
      settled = 'failed'
    },
  )

  await new Promise((resolve) => setTimeout(resolve, 300))
  assert.equal(settled, false, 'a held lock must not be acquired concurrently')

  await change.releaseGate(gate)
  await competing.catch(() => {})
  cleanup()
})
