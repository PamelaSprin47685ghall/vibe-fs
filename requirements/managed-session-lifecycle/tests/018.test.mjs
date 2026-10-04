import assert from 'node:assert/strict'
import test from 'node:test'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { setImmediate } from 'node:timers/promises'
import * as forkTool from '../../../dist/Execution/Delegation/Fork/OpenCode/ToolSurface.js'
import * as status from '../../../dist/Execution/Session/ChatExecution/StatusSurface.js'
import * as routing from '../../../dist/OpenCode/Host/ModelRoutingSurface.js'
import { withExecutablePlugin } from '../../verification-system/tests/support/plugin-fixture.mjs'

const schemaNode = (kind, extra = {}) => ({
  kind, ...extra,
  describe: () => schemaNode(`${kind}-described`, extra),
  optional: () => schemaNode(`${kind}-optional`, extra),
  int: () => schemaNode(`${kind}-int`, extra),
  nonnegative: () => schemaNode(`${kind}-nonnegative`, extra),
})
const toolModule = {
  tool: { schema: {
    string: () => schemaNode('string'), number: () => schemaNode('number'),
    enum: (values) => schemaNode('enum', { values }),
    array: (inner) => schemaNode('array', { inner }),
  } },
}

test('WHAT[managed-session-lifecycle-018] actual tool runtime detach preserves durable Active child without physical abort', async () => {
  const directory = mkdtempSync(join(tmpdir(), 'wxs-fork-process-detach-'))
  const owner = 'manager-process-detach'
  const runtime = await forkTool.createRuntime(directory, [{ sessionId: owner, agent: 'manager' }])
  try {
    const placed = forkTool.executeManagerFork(runtime, toolModule, owner, 'engineer', 'Ada', 'SURVIVE-PLUGIN-RELOAD')
    await forkTool.awaitPromptCount(runtime, 1)
    assert.equal(forkTool.acceptPrompt(runtime, 0), true)
    assert.match(await placed, /Ada/)
    assert.equal(forkTool.durableLifecycleByname(runtime, owner, 'Ada'), 'Active')
    await forkTool.detachToolRuntime(runtime)
    assert.equal(forkTool.durableLifecycleByname(runtime, owner, 'Ada'), 'Active')
    assert.equal(forkTool.abortCount(runtime), 0)
    assert.equal(forkTool.childCount(runtime), 1)
  } finally {
    forkTool.disposeRuntime(runtime)
    rmSync(directory, { recursive: true, force: true })
  }
})

test('WHAT[managed-session-lifecycle-018] real TurnAborted, retry, Fission and unknown stops preserve durable handles and permit re-enlist after process restart (GAP-133)', async () => {
  const scenarios = [
    { label: 'turn-aborted', expected: 'Active',
      emit: (runtime, owner, root) => forkTool.emitStopForRoot(runtime, owner, root, 'Aborted') },
    { label: 'provider-retry', expected: 'Active',
      emit: (runtime, owner, root) => forkTool.emitStopWithReason(runtime, owner, root, 'Failed', 'MISSING_FINAL_REPORT: bounded retry continues') },
    { label: 'fission-external-abort', expected: 'CompletedAwaitingJoin',
      emit: (runtime, owner) => forkTool.emitStopWithReason(runtime, owner, '', 'Failed', 'fission external abort') },
    { label: 'unknown-stop', expected: 'Active',
      emit: (runtime, owner, root) => forkTool.emitStopForRoot(runtime, owner, 'a-root-this-run-never-accepted', 'Failed') },
  ]

  for (const scenario of scenarios) {
    const owner = `manager-stop-${scenario.label}`
    const directory = mkdtempSync(join(tmpdir(), `wxs-stop-preserve-${scenario.label}-`))
    let first
    let durableAtExit

    const a = await forkTool.createRuntime(directory, [{ sessionId: owner, agent: 'manager' }])
    try {
      const invocation = forkTool.executeManagerFork(a, toolModule, owner, 'engineer', 'Ada', `CHARGE-${scenario.label}`)
      await forkTool.awaitPromptCount(a, 1)
      assert.equal(forkTool.acceptPrompt(a, 0), true)
      assert.match(await invocation, /Ada/)
      first = forkTool.workSnapshot(a, owner).find(work => work.lifecycle === 'Active')
      assert.equal(forkTool.durableLifecycleByname(a, owner, 'Ada'), 'Active')

      await scenario.emit(a, owner, first.root)

      const afterStop = forkTool.workSnapshot(a, owner)
      const stopped = afterStop.find(work => work.root === first.root)
      assert.equal(stopped.lifecycle, scenario.expected,
        `${scenario.label}: the stop settles per its real semantics, never as an unauthorized abandon`)
      assert.notEqual(stopped.lifecycle, 'Abandoned')
      assert.notEqual(stopped.lifecycle, 'Retired')
      assert.equal(forkTool.abortCount(a), 0,
        `${scenario.label}: a non-authorized stop must not physically abort the child session`)
      durableAtExit = afterStop
    } finally {
      forkTool.disposeRuntime(a)
    }

    try {
      assert.deepEqual(await forkTool.coldWorkSnapshot(directory, owner), durableAtExit,
        `${scenario.label}: a cold replay of the journal folds the exact same durable handle facts`)

      const b = await forkTool.createRuntime(directory, [{ sessionId: owner, agent: 'manager' }])
      try {
        const reEnlist = forkTool.executeManagerResume(b, toolModule, owner, '', 'Ada', `RE-ENLIST-${scenario.label}`)
        await forkTool.awaitPromptCount(b, 1)
        assert.equal(forkTool.acceptPrompt(b, 0), true)
        assert.match(await reEnlist, /Ada/)

        assert.equal(forkTool.childCount(b), 0,
          `${scenario.label}: re-enlist adopts the durable binding instead of forking a new child`)
        assert.equal(forkTool.child(b), first.child)
        const works = forkTool.workSnapshot(b, owner)
        const next = works.find(work => work.lifecycle === 'Active')
        assert.equal(next.handle, first.handle)
        assert.equal(next.child, first.child)
        // A settled stop (fission) frees the byname for a new work root; an
        // unsettled stop (Active) means the same work unit is resumed, so the
        // root is reused — both preserve the durable handle binding.
        if (scenario.expected === 'CompletedAwaitingJoin') {
          assert.notEqual(next.root, first.root)
        } else {
          assert.equal(next.root, first.root)
        }
        assert.equal(works.find(work => work.root === first.root).lifecycle, scenario.expected,
          `${scenario.label}: re-enlisting new work must not rewrite the stopped root's settled fact`)
      } finally {
        forkTool.disposeRuntime(b)
      }
    } finally {
      rmSync(directory, { recursive: true, force: true })
    }
  }
})
test('WHAT[managed-session-lifecycle-018] authorized owner cancellation durably abandons only its child and awaits the held Host abort', async () => {
  const directory = mkdtempSync(join(tmpdir(), 'wxs-fork-logical-cancel-'))
  const entered = Promise.withResolvers()
  const release = Promise.withResolvers()
  const aborts = []
  const owners = ['manager-cancel', 'manager-preserved']
  let runtime
  let cancellation
  try {
    runtime = await forkTool.createRuntimeWithAbort(directory,
      owners.map(sessionId => ({ sessionId, agent: 'manager' })),
      async sessionId => {
        aborts.push(sessionId)
        entered.resolve()
        await release.promise
        return { ok: true }
      })
    const children = []
    for (const owner of owners) {
      forkTool.acceptNextPrompt(runtime)
      assert.match(await forkTool.executeManagerFork(runtime, toolModule, owner, 'engineer', 'Ada', 'REVIEW-ONE-CHANGE'), /Ada/)
      children.push(forkTool.child(runtime))
      assert.equal(forkTool.durableLifecycleByname(runtime, owner, 'Ada'), 'Active')
    }
    let completed = false
    cancellation = forkTool.cancelOwnerChildren(runtime, owners[0])
    cancellation.then(() => { completed = true }, () => { completed = true })
    await entered.promise
    await setImmediate()
    assert.equal(completed, false, 'logical cancellation must retain the pending Host cleanup')
    assert.deepEqual(aborts, [children[0]])
    assert.equal(forkTool.durableLifecycleByname(runtime, owners[0], 'Ada'), 'Abandoned')
    assert.equal(forkTool.durableLifecycleByname(runtime, owners[1], 'Ada'), 'Active')

    release.resolve()
    await cancellation
    assert.equal(completed, true)
    assert.deepEqual(aborts, [children[0]])
    assert.equal(forkTool.durableLifecycleByname(runtime, owners[0], 'Ada'), 'Abandoned')
    assert.equal(forkTool.durableLifecycleByname(runtime, owners[1], 'Ada'), 'Active')
  } finally {
    release.resolve()
    try {
      if (cancellation) await cancellation
    } finally {
      try {
        if (runtime) await forkTool.detachToolRuntime(runtime)
      } finally {
        try {
          if (runtime) forkTool.disposeRuntime(runtime)
        } finally {
          rmSync(directory, { recursive: true, force: true })
        }
      }
    }
  }
})

test.todo('WHAT[managed-session-lifecycle-018] plugin shutdown waits for already admitted provider transforms and terminal callbacks without logical cancellation (GAP-133)')

test('WHAT[managed-session-lifecycle-018] actual plugin dispose detaches capacity without cancelling durable Accepted executions', async () => {
  await withExecutablePlugin(async (hooks, directory, createdIds, runtime) => {
    const sessionId = 'ses-dispose-accepted'
    const physicalUserMessageId = 'msg-dispose-accepted'
    await hooks['chat.message']({ sessionID: sessionId, messageID: physicalUserMessageId, agent: 'engineer' }, {
      message: { id: physicalUserMessageId, sessionID: sessionId, role: 'user', agent: 'engineer' },
      parts: [],
    })
    const before = status.query(runtime.journal, sessionId, physicalUserMessageId)
    assert.deepEqual(before, { accepted: true, providerStarted: false, terminal: false, disposition: null })
    assert.equal(routing.sharedCapacitySnapshot().executions.some((execution) => execution.sessionId === sessionId), true)

    await hooks.dispose()

    assert.deepEqual(status.query(runtime.journal, sessionId, physicalUserMessageId), before)
    const capacity = routing.sharedCapacitySnapshot()
    assert.equal(capacity.executions.some((execution) => execution.sessionId === sessionId), false)
    assert.equal(capacity.tokens.some((token) => token.owner.sessionId === sessionId), false)
    assert.equal(capacity.waiters.some((waiter) => waiter.sessionId === sessionId), false)
  })
})
