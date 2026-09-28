import assert from 'node:assert/strict'
import test from 'node:test'
import * as sync from '../../../dist/Execution/Delegation/SyncDelegate/Surface.js'
import { withSyncRuntime } from './support/sync-runtime.mjs'

test('WHAT[delegation-010] successive sync work reuses its child and requires a fresh managed assignment rather than an old completion', async () => {
  const owner = 'owner-sync-binding'
  await withSyncRuntime(owner, async runtime => {
    const first = sync.invoke(runtime, owner, 'Engineer', 'first charge')
    await sync.awaitPromptCount(runtime, owner, 'Engineer', 1)
    assert.equal(sync.promptOrigin(runtime, owner, 'Engineer', 0), 'AgentOwnerRoot')
    assert.equal(sync.acceptPrompt(runtime, owner, 'Engineer', 0), true)
    const child = sync.child(runtime, owner, 'Engineer')
    assert.equal(await sync.settle(runtime, owner, 'Engineer', 'first answer', 'run-first'), true)
    assert.equal((await first).ok, true)
    const second = sync.invoke(runtime, owner, 'Engineer', 'second charge')
    const admission = await Promise.race([
      second.then(value => ({ kind: 'result', value })),
      sync.awaitPromptCount(runtime, owner, 'Engineer', 2).then(() => ({ kind: 'prompt' })),
    ])
    assert.deepEqual(admission, { kind: 'prompt' })
    assert.equal(sync.promptOrigin(runtime, owner, 'Engineer', 1), 'ManagedDelegationAssignment')
    assert.equal(sync.acceptPrompt(runtime, owner, 'Engineer', 1), true)
    assert.equal(sync.child(runtime, owner, 'Engineer'), child)
    assert.equal(await sync.settle(runtime, owner, 'Engineer', 'second answer', 'run-second'), true)
    const result = await second
    assert.equal(result.ok, true)
    assert.match(result.value, /second answer/)
    assert.doesNotMatch(result.value, /first answer/)
    assert.equal(sync.childCount(runtime), 1)
  })
})

test('WHAT[delegation-010] vocabulary keeps compatibility tiers inert and distinguishes active from historical role labels', () => {
  for (const tier of ['Fast', 'Deep', 'Bigger']) {
    assert.deepEqual(sync.vocabulary('Engineer', tier, 'scope'), { role: 'engineer', agent: 'engineer', scope: 'scope' })
  }
  assert.equal(sync.vocabulary('Coder', 'Fast', 'scope').agent, 'coder')
  assert.equal(sync.vocabulary('Inspector', 'Deep', 'scope').agent, 'inspector')
  assert.equal(sync.vocabulary('Engineer', 'Fast', 'other-scope').scope, 'other-scope')
})

test.todo('WHAT[delegation-010] actual routing uses effective system configuration and retained binding without accepting caller-selected model targets (GAP-153)')
