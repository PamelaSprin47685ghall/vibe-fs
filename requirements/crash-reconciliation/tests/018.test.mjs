import { readdirSync } from 'node:fs'
import { resolve } from 'node:path'
import test from 'node:test'
import assert from 'node:assert/strict'
import * as authority from '../../../dist/Interaction/Authority/RuntimeSurface.js'
import * as dispatch from '../../../dist/Interaction/Dispatch/DispatchSurface.js'
import * as journal from '../../../dist/Persistence/Journal/Surface.js'
import * as recovery from '../../../dist/OpenCode/Host/LoadRecoverySurface.js'
import * as forkTool from '../../../dist/Execution/Delegation/Fork/OpenCode/ToolSurface.js'
import { withJournal, acceptOwner, hostPort } from '../../interaction-authority/tests/support/authority.mjs'

test('WHAT[crash-reconciliation-018] canonical load settlement voids the exact admitted child work and preserves its stable road for explicit new work', async () => {
  await withJournal('scoped-child-load', async (initial, reopen, directory) => {
    const parent = 'load-parent'
    const child = 'load-child'
    const owner = await acceptOwner(initial, parent)
    const linked = await journal.JournalSurface_appendAgent(initial, { kind: 'Session', session: parent }, null, {
      family: 'Execution', case: 'HandleLinked', payload: {
        ParentSessionId: parent, ChildSessionId: child, Handle: 'agent:load-engineer',
        TargetAgent: 'engineer', Byname: 'Ada', CanonicalRole: 'engineer', Ownership: 'DurableParentHandle',
      },
    })
    assert.equal(linked.ok, true, JSON.stringify(linked.error))
    const seed = authority.issueInheritedIdentitySeed('engineer', owner)
    assert.equal(seed.ok, true, seed.error)
    let sends = 0
    const port = hostPort(async () => {
      sends += 1
      return dispatch.admittedWithReceipt(`load-receipt-${sends}`)
    })
    const first = await dispatch.sendAgentOwnerRootAwait(port, initial, child, 'WORK-A', seed.value)
    assert.equal(first.ok, true, first.error)
    const acceptedA = await dispatch.acceptAgentOwnerRoot(initial, child, first.key, 'physical-work-A')
    assert.equal(acceptedA.ok, true, acceptedA.error)
    const [workA] = await forkTool.coldWorkSnapshot(directory, parent)
    assert.equal(workA.lifecycle, 'Active')
    const current = await reopen()
    await recovery.settleChildRuns(current)
    assert.equal(dispatch.projectionObservation(current, child).activeLogicalRun, null)
    const after = await forkTool.coldWorkSnapshot(directory, parent)
    assert.equal(after.length, 1)
    assert.deepEqual(after[0], { ...workA, lifecycle: 'Retired' })
    assert.equal(after[0].completionRef, undefined)
    assert.equal(after[0].completionDigest, undefined)
    assert.equal(sends, 1, 'load settlement never replays the interrupted assignment')
    await recovery.settleChildRuns(current)
    assert.deepEqual(await forkTool.coldWorkSnapshot(directory, parent), after)
    const second = await dispatch.sendAgentOwnerRootAwait(port, current, child, 'WORK-B', seed.value)
    assert.equal(second.ok, true, second.error)
    const acceptedB = await dispatch.acceptAgentOwnerRoot(current, child, second.key, 'physical-work-B')
    assert.equal(acceptedB.ok, true, acceptedB.error)
    const works = await forkTool.coldWorkSnapshot(directory, parent)
    assert.equal(works.length, 2)
    assert.equal(works.find(work => work.root === workA.root).lifecycle, 'Retired')
    const workB = works.find(work => work.root !== workA.root)
    assert.equal(workB.lifecycle, 'Active')
    assert.equal(workB.handle, workA.handle)
    assert.equal(workB.child, workA.child)
    assert.equal(workB.targetAgent, workA.targetAgent)
    assert.equal(workB.byname, workA.byname)
    assert.equal(sends, 2, 'only the explicit new assignment sends after load settlement')
  })
})

{
const { default: assert } = await import('node:assert/strict')
const { default: test } = await import('node:test')
const { withExecutablePlugin } = await import('../../verification-system/tests/support/plugin-fixture.mjs')

const root = resolve(import.meta.dirname, '../../..')

// WHAT[crash-reconciliation-018]: a restart normalizes itself — the interrupted
// child run is settled, process-local execution bindings are rebuilt from the
// durable handles, and durable children are re-enlisted — so there is no explicit
// resume command any more. Nothing may be replayed on the user's behalf, and the
// interrupted tool stays failed in visible history.
test('WHAT[crash-reconciliation-018] CRASH_018_no_explicit_resume_command_is_registered', async () => {
  await withExecutablePlugin(async (hooks) => {
    const config = {}

    await hooks.config(config)

    assert.equal(config.command, undefined, 'plugin load must not register any command, including /continue')
  })
})

test('WHAT[crash-reconciliation-018] structural audit finds no retired explicit-resume source files', () => {
  const walk = (directory) =>
    readdirSync(directory, { withFileTypes: true }).flatMap((entry) => {
      const path = resolve(directory, entry.name)
      if (entry.isDirectory()) {
        if (entry.name === '.fable-build' || entry.name === 'node_modules') return []
        return walk(path)
      }
      return [path]
    })

  const offenders = walk(resolve(root, 'src/Wanxiangshu'))
    .filter((path) => /(ExplicitResume|ExplicitSessionResume|SessionResumePort)/.test(path))
    .map((path) => path.slice(root.length + 1))

  assert.deepEqual(offenders, [], 'no explicit-resume module may remain in the compiled source tree')
})
}

test.todo('WHAT[crash-reconciliation-018] a restarted plugin normalizes durable child runs before reuse without replaying tools or hiding their interrupted history (GAP-149)')
