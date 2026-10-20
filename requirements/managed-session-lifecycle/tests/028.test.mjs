import assert from 'node:assert/strict'
import test from 'node:test'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { dirname } from 'node:path'
import fs from 'node:fs'
import { syncBuiltinESMExports } from 'node:module'
import * as forkTool from '../../../dist/Execution/Delegation/Fork/OpenCode/ToolSurface.js'
import { toolModule } from '../../delegation/tests/support/fork-runtime.mjs'

// managed-session-lifecycle: a proven child terminal must always produce an
// observable settlement. The incident class these tests pin: the child run
// reached Terminal(Completed) in the journal, but no HandleWorkCompleted was
// ever appended, so the parent join waited forever and only shutdown
// cancellation ended it.

test('WHAT[managed-session-lifecycle-028] child completion reads its delivery through the current view boundary', async () => {
  const owner = 'manager-settlement-workrecord-fault'
  const directory = mkdtempSync(join(tmpdir(), 'wxs-settle-workrecord-'))

  let readFaults = 0
  const runtime = await forkTool.createRuntimeWithViewRead(
    directory,
    [{ sessionId: owner, agent: 'manager' }],
    async () => {
      readFaults += 1
    }
  )

  try {
    const placed = forkTool.executeManagerFork(runtime, toolModule, owner, 'engineer', 'Ada', 'WORK-RECORD-FAULT-CHARGE')
    await forkTool.awaitPromptCount(runtime, 1)
    assert.equal(forkTool.acceptPrompt(runtime, 0), true)
    assert.match(await placed, /Ada/)

    assert.equal(await forkTool.settle(runtime, owner, 'WORK-RECORD-FAULT-DONE', 'fault-provider-run'), true)

    assert.equal(readFaults, 1, 'the current view boundary is observed once')

    const works = forkTool.workSnapshot(runtime, owner)
    assert.ok(works.length > 0, 'the owner keeps a work record')
    assert.equal(works[0].lifecycle, 'CompletedAwaitingJoin', 'the completion cell is joinable')

    const joined = await forkTool.executeJoin(runtime, owner)
    assert.ok(
      /WORK-RECORD-FAULT-DONE|returned|fault|PERSIST|ERROR|CANCELLED/i.test(joined),
      `the join resolved with an explicit outcome: ${joined}`
    )
  } finally {
    await forkTool.disposeRuntime(runtime)
    rmSync(directory, { recursive: true, force: true })
  }
})

test('WHAT[managed-session-lifecycle-028] a proven terminal that faults during settlement resolves the run with an explicit failure instead of stranding the join', async () => {
  const owner = 'manager-settlement-delivery-fault'
  const directory = mkdtempSync(join(tmpdir(), 'wxs-settle-delivery-'))

  const runtime = await forkTool.createRuntime(directory, [{ sessionId: owner, agent: 'manager' }])
  const append = fs.appendFileSync
  let injected = 0

  try {
    const placed = forkTool.executeManagerFork(runtime, toolModule, owner, 'engineer', 'Ada', 'DELIVERY-FAULT-CHARGE')
    await forkTool.awaitPromptCount(runtime, 1)
    assert.equal(forkTool.acceptPrompt(runtime, 0), true)
    assert.match(await placed, /Ada/)

    const delivered = await forkTool.prepareTerminalDelivery(runtime, owner, 'DELIVERY-FAULT-DONE', 'fault-provider-run')
    const root = fs.realpathSync(directory)
    fs.appendFileSync = function (path, content, ...options) {
      if (String(content).includes('HandleWorkCompleted') && fs.realpathSync(dirname(path)).startsWith(`${root}/`)) {
        injected += 1
        throw Object.assign(new Error('actual HandleWorkCompleted append failed'), { code: 'EIO' })
      }
      return append.call(this, path, content, ...options)
    }
    syncBuiltinESMExports()
    try {
      await delivered()
    } finally {
      fs.appendFileSync = append
      syncBuiltinESMExports()
    }
    assert.equal(injected, 1, 'the real production completion append must reach the injected boundary')

    const works = forkTool.workSnapshot(runtime, owner)
    assert.ok(works.length > 0, 'the owner keeps a work record after delivery')
    assert.equal(works[0].lifecycle, 'Active', 'unknown persistence must retain the durable work')

    // The join must terminate: either the completion or an explicit failure
    // is consumable, never an endless wait.
    const joined = await forkTool.executeJoin(runtime, owner)
    assert.ok(
      /could not be gathered|未能被收集|materializ/i.test(joined),
      `the join resolved with an explicit outcome: ${joined}`
    )
  } finally {
    fs.appendFileSync = append
    syncBuiltinESMExports()
    await forkTool.disposeRuntime(runtime)
    rmSync(directory, { recursive: true, force: true })
  }
})

test('WHAT[managed-session-lifecycle-028] a PERSIST-settled Source with durable work still Active wakes the join instead of hanging on the journal revision', async () => {
  const owner = 'manager-settlement-persist-source'
  const directory = mkdtempSync(join(tmpdir(), 'wxs-settle-persist-'))

  const runtime = await forkTool.createRuntime(directory, [{ sessionId: owner, agent: 'manager' }])

  try {
    const placed = forkTool.executeManagerFork(runtime, toolModule, owner, 'engineer', 'Ada', 'PERSIST-SOURCE-CHARGE')
    await forkTool.awaitPromptCount(runtime, 1)
    assert.equal(forkTool.acceptPrompt(runtime, 0), true)
    assert.match(await placed, /Ada/)

    // The pending run exists and its durable work is still Active.
    assert.equal(forkTool.workSnapshot(runtime, owner)[0].lifecycle, 'Active')

    // Settlement faulted: the Source is settled to a PERSIST failure while the
    // durable work stays Active and the run stays pending (unknown durable
    // commit). The journal revision never advances, so a join waiting only on
    // journal change would hang forever — it must instead surface the
    // settlement failure through the pending Source.
    const injected = await forkTool.injectPendingPersistFailure(runtime, owner)
    assert.equal(injected.ok, true)

    let timer
    let joined
    try {
      joined = await Promise.race([
        forkTool.executeJoin(runtime, owner),
        new Promise((_, reject) => {
          timer = setTimeout(() => reject(new Error('join hung on PERSIST-settled Source with durable work Active')), 5000)
        }),
      ])
    } finally {
      clearTimeout(timer)
    }
    assert.match(
      joined,
      /could not be gathered|未能被收集|materializ/i,
      `join surfaced the settlement failure instead of hanging: ${joined}`,
    )

    // The durable work is NOT released: it stays Active. An unknown durable
    // commit must not permit new work or masquerade as a success.
    assert.equal(forkTool.workSnapshot(runtime, owner)[0].lifecycle, 'Active')
  } finally {
    await forkTool.disposeRuntime(runtime)
    rmSync(directory, { recursive: true, force: true })
  }
})
