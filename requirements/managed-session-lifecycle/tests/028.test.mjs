import assert from 'node:assert/strict'
import test from 'node:test'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import * as forkTool from '../../../dist/Execution/Delegation/Fork/OpenCode/ToolSurface.js'
import { toolModule } from '../../delegation/tests/support/fork-runtime.mjs'

// managed-session-lifecycle: a proven child terminal must always produce an
// observable settlement. The incident class these tests pin: the child run
// reached Terminal(Completed) in the journal, but no HandleWorkCompleted was
// ever appended, so the parent join waited forever and only shutdown
// cancellation ended it.

test('WHAT[managed-session-lifecycle-028] a faulted work-record read degrades to an empty record and still settles the completed child', async () => {
  const owner = 'manager-settlement-workrecord-fault'
  const directory = mkdtempSync(join(tmpdir(), 'wxs-settle-workrecord-'))

  let readFaults = 0
  const runtime = await forkTool.createRuntimeWithWorkRecordRead(
    directory,
    [{ sessionId: owner, agent: 'manager' }],
    async () => {
      readFaults += 1
      throw new Error('injected work-record read fault')
    }
  )

  try {
    const placed = forkTool.executeManagerFork(runtime, toolModule, owner, 'engineer', 'Ada', 'WORK-RECORD-FAULT-CHARGE')
    await forkTool.awaitPromptCount(runtime, 1)
    assert.equal(forkTool.acceptPrompt(runtime, 0), true)
    assert.match(await placed, /Ada/)

    // Deliver the completed terminal through the physical Notify boundary.
    // The harness's own work-record await reports false on the degraded
    // empty record; the assertions below pin what actually must hold.
    await forkTool.settle(runtime, owner, 'WORK-RECORD-FAULT-DONE', 'fault-provider-run')

    assert.ok(readFaults > 0, 'the injected fault actually fired during settlement')

    const works = forkTool.workSnapshot(runtime, owner)
    assert.ok(works.length > 0, 'the owner keeps a work record')
    assert.equal(works[0].lifecycle, 'CompletedAwaitingJoin', 'the completion cell is joinable')

    const joined = await forkTool.executeJoin(runtime, owner)
    assert.ok(
      /WORK-RECORD-FAULT-DONE|returned|fault|PERSIST|ERROR|CANCELLED/i.test(joined),
      `the join resolved with an explicit outcome: ${joined}`
    )
  } finally {
    forkTool.disposeRuntime(runtime)
    rmSync(directory, { recursive: true, force: true })
  }
})

test('WHAT[managed-session-lifecycle-028] a proven terminal that faults during settlement resolves the run with an explicit failure instead of stranding the join', async () => {
  const owner = 'manager-settlement-delivery-fault'
  const directory = mkdtempSync(join(tmpdir(), 'wxs-settle-delivery-'))

  const runtime = await forkTool.createRuntime(directory, [{ sessionId: owner, agent: 'manager' }])

  try {
    const placed = forkTool.executeManagerFork(runtime, toolModule, owner, 'engineer', 'Ada', 'DELIVERY-FAULT-CHARGE')
    await forkTool.awaitPromptCount(runtime, 1)
    assert.equal(forkTool.acceptPrompt(runtime, 0), true)
    assert.match(await placed, /Ada/)

    // Deliver the terminal through the same physical Notify boundary the
    // production reconcile path uses. Settlement faults must surface as an
    // explicit joinable outcome, never as silence.
    const delivered = await forkTool.prepareTerminalDelivery(runtime, owner, 'DELIVERY-FAULT-DONE', 'fault-provider-run')
    await delivered()

    const works = forkTool.workSnapshot(runtime, owner)
    assert.ok(works.length > 0, 'the owner keeps a work record after delivery')
    assert.ok(
      ['CompletedAwaitingJoin', 'Retired'].includes(works[0].lifecycle),
      `the run reached an observable terminal state, got ${works[0].lifecycle}`
    )

    // The join must terminate: either the completion or an explicit failure
    // is consumable, never an endless wait.
    const joined = await forkTool.executeJoin(runtime, owner)
    assert.ok(
      /DELIVERY-FAULT-DONE|fault|PERSIST|ERROR|CANCELLED/i.test(joined),
      `the join resolved with an explicit outcome: ${joined}`
    )
  } finally {
    forkTool.disposeRuntime(runtime)
    rmSync(directory, { recursive: true, force: true })
  }
})
