import assert from 'node:assert/strict'
import test from 'node:test'
import { permissions, isAllowed } from '../../../dist/Participant/Persona/OfficeCapabilitySurface.js'
import * as sync from '../../../dist/Execution/Delegation/SyncDelegate/Surface.js'
import * as forkTool from '../../../dist/Execution/Delegation/Fork/OpenCode/ToolSurface.js'
import { toolModule, withForkRuntime } from './support/fork-runtime.mjs'

test('WHAT[delegation-032] office capability projection denies cross-role dispatch while retaining each role’s own work', () => {
  for (const [role, denied, allowed] of [
    ['engineer', ['Fork', 'Resume', 'Exec', 'Pty', 'Join', 'Horizon'], ['Read', 'Write', 'Edit', 'Glob', 'Grep', 'Move', 'Remove', 'BashHoneypot', 'Fission']],
    ['devops', ['Fork', 'Resume', 'Fission'], ['Read', 'Write', 'Edit', 'Glob', 'Grep', 'Move', 'Remove', 'Exec', 'Pty', 'Join', 'Horizon']],
  ]) {
    const actual = permissions(role)
    for (const capability of denied) {
      assert.equal(actual.includes(capability), false)
      assert.equal(isAllowed(role, capability), false)
    }
    for (const capability of allowed) {
      assert.equal(actual.includes(capability), true)
      assert.equal(isAllowed(role, capability), true)
    }
  }
})

test('WHAT[delegation-032] SyncDelegate selects standard Engineer capabilities without DevOps authority', () => {
  const vocabulary = sync.vocabulary('Engineer', 'Fast', 'sphinx-scope')
  assert.equal(vocabulary.role, 'engineer')
  assert.equal(vocabulary.agent, 'engineer')
  assert.equal(vocabulary.scope, 'sphinx-scope')
  assert.equal(isAllowed(vocabulary.agent, 'Write'), true)
  assert.equal(isAllowed(vocabulary.agent, 'Fission'), true)
  assert.equal(isAllowed(vocabulary.agent, 'Exec'), false)
})

test('WHAT[delegation-032] Manager fork refuses a non-engineer calling and dispatches nothing while engineer calling still dispatches', async () => {
  const owner = 'owner-cross-role-dispatch'
  await withForkRuntime(owner, async runtime => {
    // The same runtime proves the counters are live: a legal engineer fork does dispatch.
    const engineerFork = forkTool.executeManagerFork(runtime, toolModule, owner, 'engineer', 'Ada', 'ENGINEER-CHARGE')
    await forkTool.awaitPromptCount(runtime, 1)
    assert.equal(forkTool.acceptPrompt(runtime, 0), true)
    assert.match(await engineerFork, /carries this charge now|现已接下这项托付/)
    assert.equal(forkTool.childCount(runtime), 1)

    // Manager may not fork DevOps. The refusal must leave no child, no prompt and no handle.
    const devopsFork = await forkTool.executeManagerFork(runtime, toolModule, owner, 'devops', 'Grace', 'DEVOPS-CHARGE')
    assert.doesNotMatch(devopsFork, /carries this charge now|现已接下这项托付/)
    assert.equal(forkTool.childCount(runtime), 1, 'a refused cross-role fork must not create a child')
    assert.equal(forkTool.promptCount(runtime), 1, 'a refused cross-role fork must not dispatch a prompt')
    assert.equal(forkTool.durableLifecycleByname(runtime, owner, 'Grace'), null, 'a refused cross-role fork must leave no durable handle')

    assert.equal(await forkTool.settle(runtime, owner, 'ENGINEER-DONE', 'engineer-run'), true)
  })
})
