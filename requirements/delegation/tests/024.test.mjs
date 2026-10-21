import test from 'node:test'
import assert from 'node:assert/strict'
import { withForkRuntime, toolModule } from './support/fork-runtime.mjs'
import * as runtimeSurface from '../../../dist/Repository/Programming/Js/RuntimeSurface.js'
import * as forkTool from '../../../dist/Execution/Delegation/Fork/OpenCode/ToolSurface.js'
import { acceptAuthorityRoot, openIncumbency, withExecutablePlugin } from '../../verification-system/tests/support/plugin-fixture.mjs'
import * as eventsSurface from '../../../dist/OpenCode/Host/EventsSurface.js'
import * as handleJournal from '../../../dist/Execution/Delegation/Handle/JournalSurface.js'

// The provider language is a global preference; this file mixes the real
// plugin path (which boots the plugin and refreshes that preference) with the
// sync path, so the file pins one language for every member.
process.env.WANXIANGSHU_PROVIDER_LANGUAGE = 'en'

// 真实插件路径下的委派：fork / resume / join 都走生产接线，交付走
// `this.deliver`（它落唯一一条 durable 交付见证），终态走生产的
// `message.updated` + `session.idle` → 终态发布。夹具只负责把宿主消息
// 摆成真实顺序（物理 user 消息 → assistant 消息 → 交付 → 终态事件），
// 不伪造任何 durable 事实。

/** The Host's physical user message of one child turn (the last one the child received). */
const physicalUserMessageOf = (runtime, child) => {
  const physical = runtime.messages.filter((message) => message.id?.startsWith(`msg-${child}-`)).at(-1)
  assert.ok(physical, `the child ${child} must have received a physical user message`)
  return physical
}

/**
 * One real child turn: the assistant message, its delivery, and its terminal.
 * A turn without a material delivers nothing — the turn still completes, but
 * it owes the parent no delivery.
 */
const deliverChildTurn = async (hooks, runtime, child, run, prose, material, awaitTerminal) => {
  const physical = physicalUserMessageOf(runtime, child)
  const assistant = {
    info: {
      id: run,
      role: 'assistant',
      sessionID: child,
      parentID: physical.id,
      agent: 'engineer',
      providerID: 'provider',
      modelID: 'engineer-model',
      time: { created: 2 },
    },
    parts: [{ type: 'text', text: prose }],
  }
  runtime.pushHostMessage(child, assistant)
  const user = {
    info: {
      id: physical.id,
      role: 'user',
      sessionID: child,
      agent: 'engineer',
      model: { providerID: 'provider', modelID: 'engineer-model' },
      time: { created: 1 },
    },
    parts: physical.parts ?? [],
  }
  await hooks['experimental.chat.messages.transform']({ sessionID: child }, { messages: [user] })
  if (material !== null) {
    await hooks.tool['todowrite-for-engineer'].execute(
      {
        program: `class Js extends JsProgram { async run() { await this.deliver({ 0: ${JSON.stringify(material)} }); } }`,
      },
      { sessionID: child, agent: 'engineer', callID: `call-${run}`, messageID: run },
    )
  }
  assistant.info.time.completed = 3
  assistant.info.finish = 'stop'
  await hooks.event({ event: { type: 'message.updated', properties: { info: assistant.info } } })
  await hooks.event({ event: { type: 'session.idle', properties: { sessionID: child } } })
  await awaitTerminal(child, run)
}

/**
 * The shared terminal port is where the production terminal publication
 * lands. The test observes it (it publishes nothing itself) and waits for the
 * exact run's terminal — the child turn is not finished until the parent can
 * hear about it.
 */
const terminalObserver = async (runtime) => {
  const port = await runtime.terminalPort
  const seen = []
  const subscription = eventsSurface.subscribe(port, (sessionId, outcome) => {
    seen.push({ sessionId, outcome })
  })
  return {
    seen,
    dispose: () => eventsSurface.dispose(subscription),
    awaitRun: async (child, run) => {
      const deadline = Date.now() + 15000
      while (Date.now() < deadline) {
        const published = seen.some(
          (entry) => entry.sessionId === child && entry.outcome?.kind === 'Completed' && entry.outcome?.providerRun === run,
        )
        if (published) return
        await new Promise((resolve) => setTimeout(resolve, 25))
      }
      assert.fail(`the child ${child} never published its terminal for ${run}`)
    },
  }
}

/** The join records block — the byName index below it is a lookup mirror of the same records. */
const joinRecordsOf = (result) => {
  const start = result.indexOf('[[data.records]]')
  assert.notEqual(start, -1, 'the join result must carry its records block')
  const end = result.indexOf('[data.byName.', start)
  return end === -1 ? result.slice(start) : result.slice(start, end)
}

test('WHAT[delegation-024] pre-join resume preserves both public join delivered views', async () => {
  await withExecutablePlugin(async (hooks, directory, createdIds, runtime) => {
    const owner = 'prejoin-retained-delivery'
    await acceptAuthorityRoot(runtime, owner, 'manager')
    await openIncumbency(runtime, owner)

    const first = await hooks.tool['todowrite-for-manager'].execute(
      {
        program: 'class Js extends JsProgram { async run() { return await this.fork({ name: "Ada", charge: "FIRST" }, { 1: null }); } }',
      },
      { sessionID: owner, agent: 'manager', callID: 'call-fork', messageID: 'msg-fork' },
    )
    assert.match(first, /Ada/)
    assert.equal(createdIds.length, 1)
    const child = createdIds[0]

    const terminals = await terminalObserver(runtime)
    try {
      await deliverChildTurn(hooks, runtime, child, `run-${child}-1`, 'WORK REPORT A', 'DELIVERED-A', terminals.awaitRun)

      const second = await hooks.tool['todowrite-for-manager'].execute(
        {
          program: 'class Js extends JsProgram { async run() { return await this.resume({ name: "Ada", charge: "SECOND" }, { 1: null }); } }',
        },
        { sessionID: owner, agent: 'manager', callID: 'call-resume', messageID: 'msg-resume' },
      )
      assert.match(second, /Ada/)
      assert.equal(createdIds.length, 1, 'the same Byname must reuse the same physical child')

      await deliverChildTurn(hooks, runtime, child, `run-${child}-2`, 'WORK REPORT B', 'DELIVERED-B', terminals.awaitRun)

      const result = await hooks.tool['todowrite-for-manager'].execute(
        { program: 'class Js extends JsProgram { async run() { return await this.join(); } }' },
        { sessionID: owner, agent: 'manager', callID: 'call-join', messageID: 'msg-join' },
      )
      assert.match(result, /DELIVERED-A/)
      assert.match(result, /DELIVERED-B/)
      const records = joinRecordsOf(result)
      // The delivered view of each turn reaches the join records once, as the
      // returned body of that turn (the view names the material twice: as the
      // anchor id and as its text).
      assert.equal((records.match(/text = \\"DELIVERED-A\\"/g) ?? []).length, 1)
      assert.equal((records.match(/text = \\"DELIVERED-B\\"/g) ?? []).length, 1)
    } finally {
      terminals.dispose()
    }
  })
})

test('WHAT[delegation-024] an undelivered Completed terminal does not close the child and join stays not-ready', async () => {
  await withExecutablePlugin(async (hooks, directory, createdIds, runtime) => {
    const owner = 'delivered-and-undelivered-join'
    await acceptAuthorityRoot(runtime, owner, 'manager')
    await openIncumbency(runtime, owner)

    const first = await hooks.tool['todowrite-for-manager'].execute(
      {
        program: 'class Js extends JsProgram { async run() { return await this.fork({ name: "Ada", charge: "DELIVER-CHARGE" }, { 1: null }); } }',
      },
      { sessionID: owner, agent: 'manager', callID: 'call-fork', messageID: 'msg-fork' },
    )
    assert.match(first, /Ada/)
    const agentId = /agentId = "([^"]+)"/.exec(first)?.[1]
    assert.ok(agentId, 'the fork receipt must name the delegated agent')
    const child = createdIds[0]
    const deliveredRoot = physicalUserMessageOf(runtime, child).id

    const terminals = await terminalObserver(runtime)
    try {
      await deliverChildTurn(
        hooks,
        runtime,
        child,
        `run-${child}-1`,
        'WORK REPORT DELIVERED',
        'DELIVERED-RECORD with files = 2',
        terminals.awaitRun,
      )

      const second = await hooks.tool['todowrite-for-manager'].execute(
        {
          program: 'class Js extends JsProgram { async run() { return await this.resume({ name: "Ada", charge: "UNDELIVERED-CHARGE" }, { 1: null }); } }',
        },
        { sessionID: owner, agent: 'manager', callID: 'call-resume', messageID: 'msg-resume' },
      )
      assert.match(second, /Ada/)
      const undeliveredRoot = physicalUserMessageOf(runtime, child).id
      assert.notEqual(undeliveredRoot, deliveredRoot, 'the resumed turn roots a new physical input')

      // A Completed turn without a delivery does not write a completion: the
      // work unit stays Active and no terminal reaches the parent.
      await deliverChildTurn(hooks, runtime, child, `run-${child}-2`, 'PLAIN-RETURN-NO-DELIVER', null, () => {})

      const lifecycle = handleJournal.snapshot(runtime.journal, owner, agentId, child, undeliveredRoot)
      assert.equal(lifecycle.record?.lifecycle, 'Active')

      // The join still reports the delivered turn, and nothing of the
      // undelivered one: the undelivered turn left the child open.
      const joinReceipt = await hooks.tool['todowrite-for-manager'].execute(
        { program: 'class Js extends JsProgram { async run() { return await this.join(); } }' },
        { sessionID: owner, agent: 'manager', callID: 'call-join', messageID: 'msg-join' },
      )
      assert.match(joinReceipt, /DELIVERED-RECORD with files = 2/)
      assert.doesNotMatch(joinReceipt, /PLAIN-RETURN-NO-DELIVER/)
    } finally {
      terminals.dispose()
    }
  })
})

{
const { default: assert } = await import("node:assert/strict");
const { mkdtempSync } = await import("node:fs");
const { tmpdir } = await import("node:os");
const { join } = await import("node:path");
const { default: test } = await import("node:test");
const fork = await import("../../../dist/Execution/Delegation/Fork/Surface.js");
const forkTool = await import("../../../dist/Execution/Delegation/Fork/OpenCode/ToolSurface.js");
const runtimeSurface = await import("../../../dist/Repository/Programming/Js/RuntimeSurface.js");

const schemaNode = (kind, extra = {}) => ({
  kind,
  ...extra,
  describe: () => schemaNode(`${kind}-described`, extra),
  optional: () => schemaNode(`${kind}-optional`, extra),
  int: () => schemaNode(`${kind}-int`, extra),
  nonnegative: () => schemaNode(`${kind}-nonnegative`, extra),
})
const toolModule = {
  tool: {
    schema: {
      string: () => schemaNode('string'),
      number: () => schemaNode('number'),
      enum: (values) => schemaNode('enum', { values }),
      array: (inner) => schemaNode('array', { inner }),
    },
  },
}
const waitForPromptCount = (runtime, count) => forkTool.awaitPromptCount(runtime, count)
const ownerDescriptor = (sessionId) => [{ sessionId, agent: 'manager' }]

test('WHAT[delegation-024] FORK_TOOL_same_byname_reuse_dispatches_immediately_and_leaves_completion_to_join', async () => {
  await withExecutablePlugin(async (hooks, directory, createdIds, runtime) => {
    const owner = 'manager-reuse'
    await acceptAuthorityRoot(runtime, owner, 'manager')
    await openIncumbency(runtime, owner)

    const terminals = await terminalObserver(runtime)
    try {
      const first = await hooks.tool['todowrite-for-manager'].execute(
        {
          program: 'class Js extends JsProgram { async run() { return await this.fork({ name: "Ada", charge: "FIRST-FORK-CHARGE" }, { 1: null }); } }',
        },
        { sessionID: owner, agent: 'manager', callID: 'call-fork', messageID: 'msg-fork' },
      )
      assert.equal(createdIds.length, 1)
      const child = createdIds[0]
      assert.notEqual(child, null)
      assert.match(first, /Ada/)
      const agentId = /agentId = "([^"]+)"/.exec(first)?.[1]
      assert.ok(agentId, 'the fork receipt must name the delegated agent')

      await deliverChildTurn(hooks, runtime, child, `run-${child}-1`, 'REPORT ONE', 'FIRST-FORK-ANSWER', terminals.awaitRun)
      const firstRoot = physicalUserMessageOf(runtime, child).id

      const second = await hooks.tool['todowrite-for-manager'].execute(
        {
          program: 'class Js extends JsProgram { async run() { return await this.resume({ name: "Ada", charge: "SECOND-FORK-CHARGE" }, { 1: null }); } }',
        },
        { sessionID: owner, agent: 'manager', callID: 'call-resume', messageID: 'msg-resume' },
      )
      assert.match(second, /Ada/)
      assert.equal(createdIds.length, 1, 'same Byname must reuse the same physical child')
      assert.doesNotMatch(second, /SECOND-FORK-ANSWER/)
      assert.doesNotMatch(second, /FIRST-FORK-ANSWER/)

      const secondRoot = physicalUserMessageOf(runtime, child).id
      assert.notEqual(secondRoot, firstRoot, 'the reused child received a new physical input')
      assert.equal(
        handleJournal.snapshot(runtime.journal, owner, agentId, child, secondRoot).record?.lifecycle,
        'Active',
        'fork return must not imply that the reused child already completed',
      )

      await deliverChildTurn(hooks, runtime, child, `run-${child}-2`, 'REPORT TWO', 'SECOND-FORK-ANSWER', terminals.awaitRun)
      const thirdRoot = physicalUserMessageOf(runtime, child).id
      assert.equal(
        handleJournal.snapshot(runtime.journal, owner, agentId, child, thirdRoot).record?.lifecycle,
        'CompletedAwaitingJoin',
        'completion remains a separately consumable join fact',
      )
    } finally {
      terminals.dispose()
    }
  })
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { mkdtemp } = await import("node:fs/promises");
const { tmpdir } = await import("node:os");
const { join } = await import("node:path");
const sync = await import("../../../dist/Execution/Delegation/SyncDelegate/Surface.js");
const runtimeSurface = await import("../../../dist/Repository/Programming/Js/RuntimeSurface.js");

const live = async (owner) => sync.create(
  await mkdtemp(join(tmpdir(), 'wxs-sync-delegate-')),
  [{ sessionId: owner, agent: 'manager' }],
)
const waitForChild = async (h, owner, role) => {
  await sync.awaitPromptCount(h, owner, role, 1)
  return sync.child(h, owner, role)
}
const waitForPromptCount = async (h, owner, role, count) => {
  await sync.awaitPromptCount(h, owner, role, count)
  assert.equal(sync.acceptPrompt(h, owner, role, count - 1), true)
}
const settle = async (h, owner, role, answer, run = 'run-1') => {
  runtimeSurface.recordFencedDeliveredView(sync.child(h, owner, role), run, `call-${run}`, {
    prefixUntil: 0,
    thenMap: {},
  })
  return sync.settle(h, owner, role, answer, run)
}
const remainsPending = async (promise) =>
  Promise.race([
    promise.then((value) => ({ kind: 'resolved', value })),
    new Promise((resolve) => setImmediate(() => resolve({ kind: 'pending' }))),
  ])
const verifyReusableHandoff = async (role) => {
  const owner = `owner-handoff-${role.toLowerCase()}`
  const h = await live(owner)
  try {
    const first = sync.invoke(h, owner, role, 'FIRST-CHARGE')
    await waitForPromptCount(h, owner, role, 1)
    assert.match(sync.prompt(h, owner, role, 0), /FIRST-CHARGE/)
    assert.match(
      sync.prompt(h, owner, role, 0),
      /closing report/,
      'the charge prompt must carry the shared fork-child Base guidance (delegation-019)',
    )

    assert.equal(await settle(h, owner, role, 'FIRST-ANSWER', 'run-first'), true)
    const firstResult = await first
    assert.equal(firstResult.ok, true)
    assert.match(firstResult.value, /FIRST-ANSWER/)

    const second = sync.invoke(h, owner, role, 'SECOND-CHARGE')
    await waitForPromptCount(h, owner, role, 2)
    const secondPrompt = sync.prompt(h, owner, role, 1)
    assert.match(secondPrompt, /SECOND-CHARGE/)

    assert.equal(
      await sync.settleWithAuthorityRoot(h, owner, role, 'STALE-ANSWER', 'run-stale', 'old-authority-root'),
      false,
    )
    assert.deepEqual(await remainsPending(second), { kind: 'pending' })

    assert.equal(await settle(h, owner, role, 'SECOND-ANSWER', 'run-second'), true)
    const secondResult = await second
    assert.equal(secondResult.ok, true)
    assert.match(secondResult.value, /SECOND-ANSWER/)
    assert.doesNotMatch(secondResult.value, /FIRST-ANSWER/)
    assert.equal(sync.childCount(h), 1)
  } finally { sync.dispose(h) }
}

test('WHAT[delegation-024] Engineer reuse sends parent delta waits for its own root and returns its own child delta', async () => {
  await verifyReusableHandoff('Engineer')
})

test('WHAT[delegation-024] DevOps completion is claimed by HandleTurn and its delivered view returns to the caller', async () => {
  await verifyReusableHandoff('DevOps')
})
}
