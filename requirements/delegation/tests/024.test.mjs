import test from 'node:test'
import assert from 'node:assert/strict'
import { readFileSync, readdirSync, writeFileSync } from 'node:fs'
import { join } from 'node:path'
import * as runtimeSurface from '../../../dist/Repository/Programming/Js/RuntimeSurface.js'
import * as sync from '../../../dist/Execution/Delegation/SyncDelegate/Surface.js'
import * as dispatch from '../../../dist/Interaction/Dispatch/DispatchSurface.js'
import { withSyncRuntime } from './support/sync-runtime.mjs'
import { acceptAuthorityRoot, awaitPrompted, openIncumbency, withExecutablePlugin } from '../../verification-system/tests/support/plugin-fixture.mjs'
import {
  childTurn,
  deliverChildTurn,
  executeHostTool,
  forkCall,
  joinCall,
  joinRecordsOf,
  observeTerminals,
  physicalUserMessageOf,
  resumeCall,
  workLifecycleOf,
  withRealFork,
} from './support/real-fork.mjs'

// The provider language is a global preference; this file mixes the real
// plugin path (which boots the plugin and refreshes that preference) with the
// sync path, so the file pins one language for every member.
process.env.WANXIANGSHU_PROVIDER_LANGUAGE = 'en'

test('WHAT[delegation-024] a real child delivery waits for its own terminal and late observations cannot reopen the completed authority', async () => {
  const owner = 'delivery-before-physical-stop'
  await withRealFork(owner, async ({ hooks, createdIds, runtime }) => {
    const receipt = await forkCall(hooks, owner, 'Ada', 'Report the exact verified material')
    const agentId = /agentId = "([^"]+)"/.exec(receipt)?.[1]
    assert.ok(agentId)
    const child = createdIds[0]
    const physical = physicalUserMessageOf(runtime, child)
    const run = `run-${child}-delivery`
    const terminals = await observeTerminals(runtime)
    try {
      const { result, assistant } = await deliverChildTurn(
        hooks, runtime, child, run, 'The actual child report.', 'SEALED-CHILD-DELIVERY',
      )
      assert.match(String(result), /# ok/)
      assert.equal(assistant.info.finish, undefined, 'the Host has not reported a physical terminal')
      assert.deepEqual(terminals.seen, [], 'delivery material alone does not prove completion')
      assert.equal(workLifecycleOf(runtime, owner, agentId, child, physical.id), 'Active')
      assert.deepEqual(runtimeSurface.deliveredReturned(child), [], 'delivery and peek have not consumed captured identities')
      assert.match(String(runtimeSurface.deliveredValue(child)), /SEALED-CHILD-DELIVERY/)
      assert.deepEqual(runtimeSurface.deliveredReturned(child), [])

      const snapshot = await runtime.client.session.messages({ path: { id: child } })
      const unfinishedRequest = {
        messages: snapshot.data.map(message => {
          if (message.info) return message
          const { parts, ...info } = message
          return { info, parts }
        }),
      }
      await hooks['experimental.chat.messages.transform']({ sessionID: child }, unfinishedRequest)
      assert.ok(unfinishedRequest.messages.length > 0, 'an unfinished child may continue after delivering material')
      assert.equal(runtime.abortedIds.includes(child), false, 'the delivery cannot abort its own unfinished execution')
      assert.deepEqual(terminals.seen, [])
      assert.equal(workLifecycleOf(runtime, owner, agentId, child, physical.id), 'Active')

      assistant.info.time.completed = 3
      assistant.info.finish = 'tool-calls'
      assistant.parts.at(-1).state = { ...assistant.parts.at(-1).state, status: 'completed', output: String(result) }
      await hooks.event({ event: { type: 'message.updated', properties: { info: assistant.info } } })
      assert.deepEqual(terminals.seen, [], 'a tool-call step is not a completed assistant turn')
      const stopped = {
        info: {
          id: `${run}-stop`, role: 'assistant', sessionID: child, parentID: physical.id,
          agent: 'engineer', providerID: 'provider', modelID: 'engineer-model', time: { created: 4 },
        },
        parts: [{ type: 'text', text: 'The closing report follows the delivered material.' }],
      }
      runtime.pushHostMessage(child, stopped)
      await hooks.event({ event: { type: 'message.updated', properties: { info: stopped.info } } })
      stopped.info.time.completed = 5
      stopped.info.finish = 'stop'
      await hooks.event({ event: { type: 'message.updated', properties: { info: stopped.info } } })
      await hooks.event({ event: { type: 'session.idle', properties: { sessionID: child } } })
      await terminals.awaitRun(child, stopped.info.id)
      const completed = terminals.seen.filter(entry => entry.sessionId === child && entry.outcome.kind === 'Completed')
      assert.equal(completed.length, 1, 'the exact completed turn and committed delivery notify the parent once')
      assert.equal(completed[0].outcome.providerRun, stopped.info.id)
      assert.match(completed[0].outcome.text, /SEALED-CHILD-DELIVERY/)
      const joined = await joinCall(hooks, owner)
      assert.match(joined, /SEALED-CHILD-DELIVERY/)
      assert.match(joined, /status = "completed"/)
      assert.ok(runtimeSurface.deliveredReturned(child).some(key => key.includes('The actual child report.')))
      assert.ok(runtimeSurface.deliveredReturned(child).every(key => !key.includes('The closing report follows')))
      assert.equal(runtimeSurface.deliveredFenceOf(child), null)
      assert.equal(dispatch.projectionObservation(runtime.journal, child).activeLogicalRun, null)

      await hooks.event({ event: { type: 'message.updated', properties: { info: stopped.info } } })
      await hooks.event({ event: { type: 'session.idle', properties: { sessionID: child } } })
      await new Promise(resolve => setImmediate(resolve))
      assert.equal(dispatch.projectionObservation(runtime.journal, child).activeLogicalRun, null, 'late observations preserve the durable closure')
      const resumed = await resumeCall(hooks, owner, 'Ada', 'A distinct successor charge')
      assert.match(resumed, /ok = true/)
      const successorPhysical = physicalUserMessageOf(runtime, child).id
      assert.notEqual(successorPhysical, physical.id)
      await hooks.event({ event: { type: 'message.updated', properties: { info: stopped.info } } })
      await hooks.event({ event: { type: 'session.idle', properties: { sessionID: child } } })
      await new Promise(resolve => setImmediate(resolve))
      assert.equal(workLifecycleOf(runtime, owner, agentId, child, successorPhysical), 'Active', 'the old stopped provider cannot complete successor work')
    } finally {
      terminals.dispose()
    }
  })
})

test('WHAT[delegation-024] a real synchronous delegate consumes only the original delivery identities after a later provider stop', async () => {
  await withExecutablePlugin(async (hooks, _directory, _createdIds, runtime) => {
    const owner = 'sync-delivery-keys'
    const physical = 'sync-owner-physical'
    await acceptAuthorityRoot(runtime, owner, 'plan', physical)
    await openIncumbency(runtime, owner, `inc-${owner}`, 'plan')
    const user = {
      info: { id: physical, role: 'user', sessionID: owner, agent: 'plan', model: { providerID: 'provider', modelID: 'plan-model' }, time: { created: 1 } },
      parts: [{ type: 'text', text: 'Investigate the work before planning.' }],
    }
    runtime.pushHostMessage(owner, user)
    await hooks['chat.message']({ sessionID: owner, messageID: physical, agent: 'plan' }, { message: user.info, parts: user.parts })
    const created = Promise.withResolvers()
    const create = runtime.client.session.create
    runtime.client.session.create = async args => {
      const result = await create(args)
      created.resolve(result.data.id)
      return result
    }
    const invocation = executeHostTool(
      hooks, runtime, owner, 'plan', physical, 'sync-owner-provider', 'todowrite-for-plan',
      { program: 'class Js extends JsProgram { async run() { return await this.devops("Investigate the evidence", { 0: "The planning question." }); } }' },
      'sync-owner-call',
    )
    const child = await Promise.race([
      created.promise,
      invocation.then(result => { throw new Error(`the synchronous call returned before creating its delegate: ${result}`) }),
    ])
    await awaitPrompted(child)
    const delivery = await deliverChildTurn(hooks, runtime, child, 'sync-delivery-provider', 'ORIGINAL_SYNC_PROSE', 'SYNC-DELIVERED-MATERIAL', 'devops')
    assert.match(String(delivery.result), /# ok/)
    const cached = runtimeSurface.deliveredValue(child)
    assert.match(String(cached), /ORIGINAL_SYNC_PROSE/)
    assert.deepEqual(runtimeSurface.deliveredReturned(child), [], 'peek preserves captured identities until the invocation is claimed')
    assert.equal(runtimeSurface.deliveredValue(child), cached)
    delivery.assistant.parts.at(-1).state = { ...delivery.assistant.parts.at(-1).state, status: 'completed', output: String(delivery.result) }
    delivery.assistant.info.time.completed = 3
    delivery.assistant.info.finish = 'tool-calls'
    await hooks.event({ event: { type: 'message.updated', properties: { info: delivery.assistant.info } } })
    const stopped = {
      info: { id: 'sync-stop-provider', role: 'assistant', sessionID: child, parentID: physicalUserMessageOf(runtime, child).id, agent: 'devops', providerID: 'provider', modelID: 'devops-model', time: { created: 4 } },
      parts: [{ type: 'text', text: 'LATE_FINAL_TEXT' }],
    }
    runtime.pushHostMessage(child, stopped)
    await hooks.event({ event: { type: 'message.updated', properties: { info: stopped.info } } })
    stopped.info.time.completed = 5
    stopped.info.finish = 'stop'
    await hooks.event({ event: { type: 'message.updated', properties: { info: stopped.info } } })
    await hooks.event({ event: { type: 'session.idle', properties: { sessionID: child } } })
    const result = await invocation
    assert.match(String(result), /SYNC-DELIVERED-MATERIAL/)
    assert.doesNotMatch(String(result), /LATE_FINAL_TEXT/)
    const returned = runtimeSurface.deliveredReturned(child)
    assert.ok(returned.some(key => key.startsWith('prose:') && key.includes('ORIGINAL_SYNC_PROSE')))
    assert.ok(returned.every(key => !key.includes('LATE_FINAL_TEXT')))
    assert.equal(runtimeSurface.deliveredFenceOf(child), null)
  })
})

test('WHAT[delegation-024] different delivered material in two provider steps cannot be chosen by the terminal provider', async () => {
  const owner = 'ambiguous-child-delivery'
  await withRealFork(owner, async ({ hooks, createdIds, runtime }) => {
    const receipt = await forkCall(hooks, owner, 'Ada', 'Report the verified material')
    const agentId = /agentId = "([^"]+)"/.exec(receipt)?.[1]
    assert.ok(agentId)
    const child = createdIds[0]
    const physical = physicalUserMessageOf(runtime, child).id
    const terminals = await observeTerminals(runtime)
    try {
      const first = await deliverChildTurn(hooks, runtime, child, 'ambiguous-provider-first', 'First report.', 'FIRST-MATERIAL')
      assert.match(String(first.result), /# ok/)
      first.assistant.info.time.completed = 3
      first.assistant.info.finish = 'tool-calls'
      await hooks.event({ event: { type: 'message.updated', properties: { info: first.assistant.info } } })

      const second = await deliverChildTurn(hooks, runtime, child, 'ambiguous-provider-second', 'Second report.', 'DIFFERENT-MATERIAL')
      assert.match(String(second.result), /# ok/)
      second.assistant.info.time.completed = 5
      second.assistant.info.finish = 'stop'
      await hooks.event({ event: { type: 'message.updated', properties: { info: second.assistant.info } } })
      await hooks.event({ event: { type: 'session.idle', properties: { sessionID: child } } })
      await new Promise(resolve => setImmediate(resolve))
      assert.equal(terminals.seen.some(entry => entry.sessionId === child && entry.outcome.kind === 'Completed'), false)
      assert.equal(workLifecycleOf(runtime, owner, agentId, child, physical), 'Active', 'ambiguous material cannot complete the work')
    } finally {
      terminals.dispose()
    }
  })
})

test('WHAT[delegation-024] an unobserved child provider may read but cannot establish a delivery or notify completion', async () => {
  const owner = 'unobserved-child-delivery'
  await withRealFork(owner, async ({ hooks, directory, createdIds, runtime }) => {
    const receipt = await forkCall(hooks, owner, 'Ada', 'Investigate the evidence')
    const agentId = /agentId = "([^"]+)"/.exec(receipt)?.[1]
    assert.ok(agentId)
    const child = createdIds[0]
    await runtime.awaitIngress()
    const physical = physicalUserMessageOf(runtime, child)
    const run = 'unobserved-child-provider'
    const readProgram = 'class Js extends JsProgram { async run() { return { text: await this.cat("evidence.txt") }; } }'
    const deliverProgram = 'class Js extends JsProgram { async run() { await this.deliver({ 0: "UNADMITTED-REPORT" }); } }'
    writeFileSync(join(directory, 'evidence.txt'), 'ACTUAL-READ-EVIDENCE')
    runtime.pushHostMessage(child, {
      info: { id: run, role: 'assistant', sessionID: child, parentID: physical.id, agent: 'engineer', time: { created: 2 } },
      parts: [
        { type: 'text', text: 'A report from a provider the Host has not started.' },
        { type: 'tool', tool: 'todowrite-for-engineer', callID: 'call-unobserved-read', state: { status: 'pending', input: { program: readProgram } } },
        { type: 'tool', tool: 'todowrite-for-engineer', callID: 'call-unobserved-deliver', state: { status: 'pending', input: { program: deliverProgram } } },
      ],
    })
    const terminals = await observeTerminals(runtime)
    try {
      const read = await hooks.tool['todowrite-for-engineer'].execute(
        { program: readProgram }, { sessionID: child, agent: 'engineer', callID: 'call-unobserved-read', messageID: run },
      )
      assert.match(String(read), /# ok/)
      assert.match(String(read), /ACTUAL-READ-EVIDENCE/)
      const refused = await hooks.tool['todowrite-for-engineer'].execute(
        { program: deliverProgram }, { sessionID: child, agent: 'engineer', callID: 'call-unobserved-deliver', messageID: run },
      )
      assert.match(String(refused), /DELIVERY_NOT_ESTABLISHED/)
      assert.equal(runtimeSurface.deliveredFenceOf(child), null)
      assert.equal(terminals.seen.some(entry => entry.sessionId === child), false)
      assert.equal(workLifecycleOf(runtime, owner, agentId, child, physical.id), 'Active')
      const events = join(directory, '.Herdchestra', 'wanxiangshu', 'events')
      const facts = readdirSync(events).filter(name => name.endsWith('.ndjson')).flatMap(name =>
        readFileSync(join(events, name), 'utf8').split('\n').filter(Boolean).map(line => JSON.parse(line).payload.Fact))
      assert.equal(facts.some(fact => fact?.[1]?.[0] === 'Host' && fact[1][1]?.[0] === 'TaskDeliveryCommitted'), false)
    } finally {
      terminals.dispose()
    }
  })
})

test('WHAT[delegation-024] pre-join resume preserves both public join delivered views', async () => {
  const owner = 'prejoin-retained-delivery'
  await withRealFork(owner, async ({ hooks, createdIds, runtime }) => {
    const first = await forkCall(hooks, owner, 'Ada', 'FIRST')
    assert.match(first, /Ada/)
    assert.equal(createdIds.length, 1)
    const child = createdIds[0]

    const terminals = await observeTerminals(runtime)
    try {
      await childTurn(hooks, runtime, child, `run-${child}-1`, 'WORK REPORT A', 'DELIVERED-A', terminals.awaitRun)

      const second = await resumeCall(hooks, owner, 'Ada', 'SECOND')
      assert.match(second, /Ada/)
      assert.equal(createdIds.length, 1, 'the same Byname must reuse the same physical child')

      await childTurn(hooks, runtime, child, `run-${child}-2`, 'WORK REPORT B', 'DELIVERED-B', terminals.awaitRun)

      const result = await joinCall(hooks, owner)
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
  const owner = 'delivered-and-undelivered-join'
  await withRealFork(owner, async ({ hooks, createdIds, runtime }) => {
    const first = await forkCall(hooks, owner, 'Ada', 'DELIVER-CHARGE')
    assert.match(first, /Ada/)
    const agentId = /agentId = "([^"]+)"/.exec(first)?.[1]
    assert.ok(agentId, 'the fork receipt must name the delegated agent')
    const child = createdIds[0]
    const deliveredRoot = physicalUserMessageOf(runtime, child).id

    const terminals = await observeTerminals(runtime)
    try {
      await childTurn(
        hooks,
        runtime,
        child,
        `run-${child}-1`,
        'WORK REPORT DELIVERED',
        'DELIVERED-RECORD with files = 2',
        terminals.awaitRun,
      )

      const second = await resumeCall(hooks, owner, 'Ada', 'UNDELIVERED-CHARGE')
      assert.match(second, /Ada/)
      const undeliveredRoot = physicalUserMessageOf(runtime, child).id
      assert.notEqual(undeliveredRoot, deliveredRoot, 'the resumed turn roots a new physical input')

      // A Completed turn without a delivery does not write a completion: the
      // work unit stays Active and no terminal reaches the parent.
      await childTurn(hooks, runtime, child, `run-${child}-2`, 'PLAIN-RETURN-NO-DELIVER', null, () => {})

      assert.equal(workLifecycleOf(runtime, owner, agentId, child, undeliveredRoot), 'Active')

      // The join still reports the delivered turn, and nothing of the
      // undelivered one: the undelivered turn left the child open.
      const joinReceipt = await joinCall(hooks, owner)
      assert.match(joinReceipt, /DELIVERED-RECORD with files = 2/)
      assert.doesNotMatch(joinReceipt, /PLAIN-RETURN-NO-DELIVER/)
    } finally {
      terminals.dispose()
    }
  })
})

test('WHAT[delegation-024] synchronous observed completion refuses undelivered prose and returns only the explicit delivered value', async () => {
  for (const delivered of [false, true]) {
    const owner = `sync-explicit-delivery-${delivered}`
    await withSyncRuntime(owner, async runtime => {
      const execution = sync.startObserved(runtime, owner, `EXPLICIT-DELIVERY-${delivered}`)
      const completion = sync.observedCompletion(execution)
      const count = 1
      await sync.awaitPromptCount(runtime, owner, 'Engineer', count)
      assert.equal(sync.returnPromptOutcome(runtime, owner, 'Engineer', count - 1, dispatch.admittedWithPhysicalMessage(`explicit-physical-${delivered}`)), true)
      const accepted = await sync.observedAdmission(execution)
      if (delivered) {
        await sync.deliver(runtime, accepted.sessionId, accepted.physicalUserMessageId, accepted.authorityRootUserMessageId,
          `explicit-provider-${delivered}`, 'explicit-call', 'PROSE-IS-NOT-DELIVERY', { 0: 'EXPLICIT-DELIVERY\nfiles = 2' })
      } else {
        runtimeSurface.recordFencedDeliveredView(accepted.sessionId, `explicit-provider-${delivered}`, 'cache-only-call',
          { prefixUntil: 0, thenMap: { 0: 'CACHE-WITHOUT-DURABLE-WITNESS' } })
      }
      assert.equal(await sync.settleExactTerminal(runtime, accepted.sessionId, accepted.physicalUserMessageId, accepted.authorityRootUserMessageId, `explicit-provider-${delivered}`, 'PROSE-IS-NOT-DELIVERY', 'REASONING-IS-NOT-DELIVERY'), true)
      if (!delivered) {
        const observed = await Promise.race([completion, new Promise(resolve => setImmediate(() => resolve('pending')))])
        assert.equal(observed, 'pending', 'a live child without a delivery keeps its obligation open')
        assert.equal(sync.abandonPendingCall(runtime, owner, 'Engineer'), true)
      }
      const result = await completion
      assert.equal(result.ok, delivered)
      if (delivered) {
        assert.match(result.value.view, /EXPLICIT-DELIVERY/)
        assert.match(result.value.view, /files = 2/)
        assert.match(result.value.view, /PROSE-IS-NOT-DELIVERY/)
        assert.doesNotMatch(result.value.view, /REASONING-IS-NOT-DELIVERY|TOOL-RESULT-ONLY/)
        assert.equal(result.value.turnFormalText, 'PROSE-IS-NOT-DELIVERY')
        assert.equal(result.value.physicalUserMessageId, accepted.physicalUserMessageId)
      } else {
        assert.match(result.error, /abandon|supersed|cancelled/i)
      }
    })
  }
})

test('WHAT[delegation-024] conflicting durable deliveries cannot settle through the latest provider cell', async () => {
  const owner = 'sync-ambiguous-delivery'
  await withSyncRuntime(owner, async runtime => {
    const execution = sync.startObserved(runtime, owner, 'AMBIGUOUS-DELIVERY')
    const completion = sync.observedCompletion(execution)
    await sync.awaitPromptCount(runtime, owner, 'Engineer', 1)
    assert.equal(sync.returnPromptOutcome(runtime, owner, 'Engineer', 0, dispatch.admittedWithPhysicalMessage('ambiguous-physical')), true)
    const accepted = await sync.observedAdmission(execution)
    for (const step of [1, 2]) {
      await sync.deliver(runtime, accepted.sessionId, accepted.physicalUserMessageId, accepted.authorityRootUserMessageId,
        `ambiguous-provider-${step}`, `ambiguous-call-${step}`, `FORMAL-${step}`, { 0: `MATERIAL-${step}` })
    }
    assert.equal(runtimeSurface.deliveredFenceOf(accepted.sessionId).providerRun, 'ambiguous-provider-2')
    assert.equal(await sync.settleExactTerminal(runtime, accepted.sessionId, accepted.physicalUserMessageId,
      accepted.authorityRootUserMessageId, 'ambiguous-provider-2', 'LATE-TERMINAL-PROSE', ''), true)
    assert.equal(await Promise.race([completion.then(() => 'settled'),
      new Promise(resolve => setImmediate(() => resolve('pending')))]), 'pending')
    assert.deepEqual(runtimeSurface.deliveredReturned(accepted.sessionId), [], 'refused settlement does not consume either delivery')
    assert.equal(sync.abandonPendingCall(runtime, owner, 'Engineer'), true)
    assert.equal((await completion).ok, false)
  })
})

test('WHAT[delegation-024] FORK_TOOL_same_byname_reuse_dispatches_immediately_and_leaves_completion_to_join', async () => {
  const owner = 'manager-reuse'
  await withRealFork(owner, async ({ hooks, createdIds, runtime }) => {
    const terminals = await observeTerminals(runtime)
    try {
      const first = await forkCall(hooks, owner, 'Ada', 'FIRST-FORK-CHARGE')
      assert.equal(createdIds.length, 1)
      const child = createdIds[0]
      assert.notEqual(child, null)
      assert.match(first, /Ada/)
      const agentId = /agentId = "([^"]+)"/.exec(first)?.[1]
      assert.ok(agentId, 'the fork receipt must name the delegated agent')

      await childTurn(hooks, runtime, child, `run-${child}-1`, 'REPORT ONE', 'FIRST-FORK-ANSWER', terminals.awaitRun)
      const firstRoot = physicalUserMessageOf(runtime, child).id

      const second = await resumeCall(hooks, owner, 'Ada', 'SECOND-FORK-CHARGE')
      assert.match(second, /Ada/)
      assert.equal(createdIds.length, 1, 'same Byname must reuse the same physical child')
      assert.doesNotMatch(second, /SECOND-FORK-ANSWER/)
      assert.doesNotMatch(second, /FIRST-FORK-ANSWER/)

      const secondRoot = physicalUserMessageOf(runtime, child).id
      assert.notEqual(secondRoot, firstRoot, 'the reused child received a new physical input')
      assert.equal(
        workLifecycleOf(runtime, owner, agentId, child, secondRoot),
        'Active',
        'fork return must not imply that the reused child already completed',
      )

      await childTurn(hooks, runtime, child, `run-${child}-2`, 'REPORT TWO', 'SECOND-FORK-ANSWER', terminals.awaitRun)
      const thirdRoot = physicalUserMessageOf(runtime, child).id
      assert.equal(
        workLifecycleOf(runtime, owner, agentId, child, thirdRoot),
        'CompletedAwaitingJoin',
        'completion remains a separately consumable join fact',
      )
    } finally {
      terminals.dispose()
    }
  })
})

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { mkdtemp } = await import("node:fs/promises");
const { tmpdir } = await import("node:os");
const { join } = await import("node:path");
const sync = await import("../../../dist/Execution/Delegation/SyncDelegate/Surface.js");

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
  await sync.deliverCurrent(h, owner, role, run, `call-${run}`, answer, {})
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

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const syncDelegate = await import("../../../dist/Execution/Delegation/SyncDelegate/Surface.js");
const jsRuntime = await import("../../../dist/Repository/Programming/Js/RuntimeSurface.js");
const { mkdtempSync, rmSync, readdirSync, readFileSync } = await import("node:fs");
const { tmpdir } = await import("node:os");
const { join } = await import("node:path");

const OWNER = 'owner-sync-delegate-abort'
const CHILD_PROSE = 'DEVOPS PROSE'
const DELIVERED_VIEW = 'THE DEVOPS DELIVERY'

const assertVoidedWithoutCompletion = (directory, child) => {
  const events = join(directory, 'wanxiangshu', 'events')
  const facts = readdirSync(events).filter(name => name.endsWith('.ndjson')).flatMap(name =>
    readFileSync(join(events, name), 'utf8').split('\n').filter(Boolean).map(line => JSON.parse(line).payload.Fact))
  const executions = facts.filter(fact => fact[0] === 'Agent' && fact[1]?.[0] === 'Execution').map(fact => fact[1][1])
  const voids = executions.filter(fact => fact[0] === 'ChildRunVoided' || fact[0] === 'ChildWorkVoided')
  assert.equal(voids.length, 1, 'the interrupted work has one durable void')
  const payload = voids[0][1]
  const childId = payload.Work?.ChildSessionId ?? payload.ChildSessionId
  assert.equal(Array.isArray(childId) ? childId.at(-1) : childId, child)
  assert.equal(executions.filter(fact => fact[0] === 'HandleCompleted' || fact[0] === 'HandleWorkCompleted').length, 0)
}

// 一次真实的同步 devops 成员调用：真子会话、真提示词、真交付格。
const withSyncDelegateCall = async (body) => {
  const directory = mkdtempSync(join(tmpdir(), 'wxs-delegation-sync-abort-'))
  const harness = await syncDelegate.create(directory, [{ sessionId: OWNER, agent: 'manager' }])
  try {
    return await body(harness, directory)
  } finally {
    syncDelegate.dispose(harness)
    rmSync(directory, { recursive: true, force: true })
  }
}

test('WHAT[delegation-024] an interrupted devops member that already delivered is voided without a fabricated completion', async () => {
  await withSyncDelegateCall(async (harness, directory) => {
    // 真实的同步成员调用：等待子会话的完成。
    const work = syncDelegate.invoke(harness, OWNER, 'DevOps', 'INVESTIGATE THE FLAKY TEST')

    // 子会话的提示词被接受（真实物理认领）。
    await syncDelegate.awaitPromptCount(harness, OWNER, 'DevOps', 1)
    assert.equal(syncDelegate.acceptPrompt(harness, OWNER, 'DevOps', 0), true)

    const child = syncDelegate.child(harness, OWNER, 'DevOps')
    assert.ok(child, 'the devops child session must exist')

    // 子会话交付了：真实写入带围栏的交付格。
    await syncDelegate.deliverCurrent(harness, OWNER, 'DevOps', 'devops-run', 'devops-call', CHILD_PROSE, { 0: DELIVERED_VIEW })
    assert.equal(
      jsRuntime.deliveredFenceOf(child)?.providerRun,
      'devops-run',
      'the delivery cell is fenced by this provider run',
    )

    // 子会话随后被中断：终态是 TurnAborted，而不是完成。
    const observed = await syncDelegate.observeTurn(
      harness,
      OWNER,
      'DevOps',
      'TurnAborted',
      CHILD_PROSE,
      'devops-run',
    )
    assert.equal(observed, true, 'the interrupted turn is observed by the real owner')

    const result = await work
    assert.equal(result.ok, false, 'a delivery without a proven completion is not success')
    assert.match(result.error, /voided/)
    assertVoidedWithoutCompletion(directory, child)
  })
})

test('WHAT[delegation-024] an interrupted devops member that delivered nothing is voided and releases its wait', async () => {
  await withSyncDelegateCall(async (harness, directory) => {
    const work = syncDelegate.invoke(harness, OWNER, 'DevOps', 'INVESTIGATE THE FLAKY TEST')
    await syncDelegate.awaitPromptCount(harness, OWNER, 'DevOps', 1)
    assert.equal(syncDelegate.acceptPrompt(harness, OWNER, 'DevOps', 0), true)

    const child = syncDelegate.child(harness, OWNER, 'DevOps')
    assert.ok(child, 'the devops child session must exist')

    // 没有交付：交付格是空的。
    assert.equal(jsRuntime.deliveredFenceOf(child), null, 'no delivery was made')

    const observed = await syncDelegate.observeTurn(
      harness,
      OWNER,
      'DevOps',
      'TurnAborted',
      CHILD_PROSE,
      'devops-run',
    )
    assert.equal(observed, true, 'the exact interrupted invocation is settled')
    const result = await work
    assert.equal(result.ok, false)
    assert.match(result.error, /voided/)
    assertVoidedWithoutCompletion(directory, child)
  })
})
}
