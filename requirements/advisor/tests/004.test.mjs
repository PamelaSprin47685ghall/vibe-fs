import assert from 'node:assert/strict'
import test from 'node:test'
import { existsSync } from 'node:fs'
import { join } from 'node:path'
import { parse } from 'smol-toml'
import * as events from '../../../dist/OpenCode/Host/EventsSurface.js'
import * as dispatch from '../../../dist/Interaction/Dispatch/DispatchSurface.js'
import * as authority from '../../../dist/Interaction/Authority/RuntimeSurface.js'
import * as executionStatus from '../../../dist/Execution/Session/ChatExecution/StatusSurface.js'
import { anchors, applyView } from '../../../dist/Repository/Programming/Js/Anchor/AnchorSurface.js'
import { acceptAuthorityRoot, openIncumbency, withExecutablePlugin } from '../../verification-system/tests/support/plugin-fixture.mjs'
import { factPayloads } from '../../verification-system/tests/e2e/support/journal-observer.js'
import { executeHostTool } from '../../delegation/tests/support/real-fork.mjs'

const textOf = prompt => prompt.body.parts.filter(part => part.type === 'text').map(part => part.text).join('\n')

test('WHAT[advisor-004] the registered member reuses its live seat and refuses an empty new answer instead of replaying previous advice', { timeout: 10000 }, async () => {
  const hostObservers = new Set()
  await withExecutablePlugin(async (hooks, directory, children, runtime) => {
    const caller = 'advisor-live-answer-owner'
    await acceptAuthorityRoot(runtime, caller, 'manager', 'caller-material')
    await openIncumbency(runtime, caller)
    const root = { info: { id: 'caller-material', role: 'user', sessionID: caller, agent: 'manager',
      model: { providerID: 'provider', modelID: 'manager-model' }, time: { created: 1 } },
      parts: [{ type: 'text', text: 'CALLER_MATERIAL' }] }
    runtime.pushHostMessage(caller, root)
    await hooks['chat.message']({ sessionID: caller, messageID: root.info.id, agent: 'manager' }, { message: root.info, parts: root.parts })
    applyView(caller, [{ id: 'caller-material', text: 'CALLER_MATERIAL' }])
    const materialAnchor = anchors(caller).find(entry => entry.messageId === root.info.id)
    assert.ok(materialAnchor, 'the caller material has an actual visible anchor')
    const reportMapping = { [materialAnchor.anchor]: null }
    const authority = dispatch.projectionObservation(runtime.journal, caller)
    const originalSend = runtime.client.session.promptAsync
    let pending
    let sent
    let targetCount
    runtime.client.session.promptAsync = async args => {
      const receipt = await originalSend(args)
      if (runtime.prompts.length >= targetCount) sent?.()
      return receipt
    }

    const consult = async (intent, answer, expected) => {
      const start = runtime.prompts.length
      targetCount = start + 1
      const ready = new Promise(resolve => { sent = resolve })
      pending = executeHostTool(hooks, runtime, caller, 'manager', root.info.id, `advisor-owner-assistant-${start}`, 'todowrite-for-manager', {
        program: `class Js extends JsProgram { async run() { return await this.advisor(${JSON.stringify(intent)}, ${JSON.stringify(reportMapping)}); } }`,
      }, `advisor-${start}`)
      await Promise.race([ready, pending.then(output => { throw new Error(`consult ended before its two prompts: ${output}`) })])
      assert.equal(children.length, 1, 'one live caller reuses its actual child')
      const child = children[0]
      const viewPrompt = (await waitForPrompts(runtime, child, start + 1)).at(-1)
      await advisorRun(hooks, runtime, child, `advisor-view-${start}`, viewPrompt, 'VIEW RECEIVED', start * 10 + 1)
      await waitForPrompts(runtime, child, start + 2)
      const messages = (await runtime.client.session.messages({ path: { id: child } })).data
      const physical = messages.filter(message => message.role === 'user').at(-1)
      const runMessage = {
        info: {
          id: `advisor-run-${start}`, sessionID: child, parentID: physical.id,
          role: 'assistant', agent: 'advisor', providerID: 'provider', modelID: 'advisor-model',
          time: { created: start * 10 + 2 },
        },
        parts: [],
      }
      runtime.pushHostMessage(child, runMessage)
      await hooks['experimental.chat.messages.transform']({ sessionID: child }, { messages: [...messages, runMessage] })
      let settled = false
      pending.then(() => { settled = true })
      events.notifyCompletedForRun(runtime.terminalPort, child, 'older-consult-run', 'STALE_ADVICE', 'STALE_ADVICE', 'advisor')
      await new Promise(resolve => setImmediate(resolve))
      assert.equal(settled, false, 'a terminal outside this intent physical input cannot answer')
      if (answer) runMessage.parts.push({ id: `advice-${start}`, type: 'text', text: answer })
      runMessage.info = { ...runMessage.info, finish: answer ? 'stop' : 'tool-calls', time: { created: start * 10 + 2, completed: start * 10 + 3 } }
      const terminalEvent = { event: { type: 'message.updated', properties: { info: runMessage.info } } }
      if (answer) {
        for (const observe of hostObservers) observe(terminalEvent)
        await hooks.event(terminalEvent)
        const idle = { event: { type: 'session.status', properties: { sessionID: child, status: { type: 'idle' } } } }
        for (const observe of hostObservers) observe(idle)
        await hooks.event(idle)
      } else {
        assert.equal(events.notifyCompletedForRun(runtime.terminalPort, child, `advisor-run-${start}`, answer, answer, 'advisor'), true)
      }
      let timer
      const envelope = parse(await Promise.race([
        pending,
        new Promise((_, reject) => { timer = setTimeout(() => reject(new Error('Advisor did not observe its Host terminal: ' + JSON.stringify(dispatch.projectionObservation(runtime.journal, child)))), 3000) }),
      ]).finally(() => clearTimeout(timer)))
      assert.ok(envelope.data, JSON.stringify(envelope))
      const result = envelope.data
      pending = null
      if (!answer) {
        for (const observe of hostObservers) observe(terminalEvent)
        await hooks.event(terminalEvent)
      }
      assert.equal(result.ok, expected)
      assert.deepEqual(dispatch.projectionObservation(runtime.journal, caller), authority, 'consulting cannot create caller authority or claims')
      assert.equal(textOf(runtime.prompts[start + 1]), intent, 'intent is its own physical prompt')
      return { result, view: textOf(runtime.prompts[start]), child }
    }

    try {
      const first = await consult('Review the current material.', 'FIRST_ADVICE', true)
      assert.equal(first.result.advice, 'FIRST_ADVICE')
      assert.match(first.view, /CALLER_MATERIAL/)
      assert.doesNotMatch(first.view, /Review the current material\./)
      const second = await consult('There is no new answer.', '', false)
      assert.equal(second.result.code, 'advisor-unavailable')
      assert.equal(second.result.advice, undefined)
      assert.doesNotMatch(second.view, /CALLER_MATERIAL/, 'the successful prior consult advanced only this live cursor')
      const refused = await hooks.tool['todowrite-for-engineer'].execute({
        program: 'class Js extends JsProgram { async run() { await this.write("advisor-must-not-write", "bad"); } }',
      }, { sessionID: first.child, agent: 'advisor', messageID: 'advisor-assistant', callID: 'advisor-write' })
      assert.match(refused, /denied|not allowed|not available|cannot|不可|无权|不能/i)
      assert.equal(existsSync(join(directory, 'advisor-must-not-write')), false)
    } finally {
      runtime.client.session.promptAsync = originalSend
      if (pending && children.length > 0) {
        events.notifyCompletedForRun(runtime.terminalPort, children[0], `advisor-run-${runtime.prompts.length - 2}`, '', '', 'advisor')
        await Promise.race([pending.catch(() => {}), new Promise(resolve => setTimeout(resolve, 500))])
      }
    }
  }, { events: { listen(observe) {
    hostObservers.add(observe)
    return () => hostObservers.delete(observe)
  } } })
})

// advisor-[004] / F39-01: `this.advisor` through the real plugin. The seat creates its advisor
// child, sends the view and the intent as two Host prompts, and returns the prose the advisor
// child's run actually produced once the production terminal path publishes that run.
// The fixture only plays the Host: it appends the advisor's assistant message (HOST-010/011:
// its id is the provider run, its parentID the user message it answers) and the Host events.
// It never publishes a terminal and never writes a durable fact.

const PROMPT_KEY = 'wanxiangshu_prompt_key'
const SETTLE_MS = 20_000

const advisorCall = (hooks, runtime, owner, intent, callID) =>
  executeHostTool(hooks, runtime, owner, 'manager', `root-${owner}`, `msg-${callID}`, 'todowrite-for-manager',
    {
      program: `class Js extends JsProgram { async run() { const advised = await this.advisor(${JSON.stringify(intent)}, {}); return { advised }; } }`,
    },
    callID,
  )

const pause = (ms) => new Promise((resolve) => setTimeout(resolve, ms))

/** Race one pending tool call against a bound; `null` means it is still pending. */
const within = async (pending, ms) => {
  let timer
  const bound = new Promise((resolve) => {
    timer = setTimeout(() => resolve(null), ms)
  })
  try {
    return await Promise.race([pending, bound])
  } finally {
    clearTimeout(timer)
  }
}

const userPromptsOf = (runtime, child) =>
  runtime.messages.filter((message) => message.id?.startsWith(`msg-${child}-`) && message.role === 'user')

const waitForPrompts = async (runtime, child, count) => {
  const deadline = Date.now() + SETTLE_MS
  while (Date.now() < deadline) {
    const prompts = userPromptsOf(runtime, child)
    if (prompts.length >= count) return prompts
    await pause(20)
  }
  assert.fail(`the advisor child ${child} must receive ${count} prompts`)
}

const waitForChild = async (createdIds) => {
  const deadline = Date.now() + SETTLE_MS
  while (Date.now() < deadline) {
    if (createdIds.length > 0) return createdIds[0]
    await pause(20)
  }
  assert.fail('the advisor seat must create its advisor child')
}

/** One Host run of the advisor child answering one physical user message. */
const advisorRun = async (hooks, runtime, child, run, answering, prose, created) => {
  await runtime.awaitIngress()
  const assistant = {
    info: {
      id: run,
      role: 'assistant',
      sessionID: child,
      parentID: answering.id,
      agent: 'advisor',
      providerID: 'provider',
      modelID: 'advisor-model',
      time: { created },
    },
    parts: [{ type: 'text', text: prose }],
  }
  runtime.pushHostMessage(child, assistant)
  const user = {
    info: {
      id: answering.id,
      role: 'user',
      sessionID: child,
      agent: 'advisor',
      model: { providerID: 'provider', modelID: 'advisor-model' },
      time: { created: created - 1 },
    },
    parts: answering.parts ?? [],
  }
  await hooks['experimental.chat.messages.transform']({ sessionID: child }, { messages: [user] })
  await hooks.event({ event: { type: 'message.updated', properties: { info: assistant.info } } })
  assistant.info.time.completed = created + 1
  assistant.info.finish = 'stop'
  await hooks.event({ event: { type: 'message.updated', properties: { info: assistant.info } } })
  await hooks.event({ event: { type: 'session.idle', properties: { sessionID: child } } })
}

const keyOf = (message) => message.metadata?.[PROMPT_KEY] ?? message.parts?.[0]?.metadata?.[PROMPT_KEY]

const withAdvisorOwner = (owner, body) =>
  withExecutablePlugin(async (hooks, directory, createdIds, runtime) => {
    await acceptAuthorityRoot(runtime, owner, 'manager')
    await openIncumbency(runtime, owner)
    const root = {
      info: { id: `root-${owner}`, role: 'user', sessionID: owner, agent: 'manager',
        model: { providerID: 'provider', modelID: 'manager-model' }, time: { created: 1 } },
      parts: [{ type: 'text', text: 'review the parser rewrite' }],
    }
    runtime.pushHostMessage(owner, root)
    await hooks['chat.message']({ sessionID: owner, messageID: root.info.id, agent: 'manager' }, { message: root.info, parts: root.parts })
    return body({ hooks, createdIds, runtime })
  })

test('WHAT[advisor-004] a consult returns the prose of the advisor run that answers its own intent (F39-01)', async () => {
  const owner = `sess-advisor-004-${Math.random().toString(36).slice(2, 8)}`
  await withAdvisorOwner(owner, async ({ hooks, createdIds, runtime }) => {
    const pending = advisorCall(hooks, runtime, owner, 'is the rewrite safe?', 'call-advisor-1')
    const child = await waitForChild(createdIds)
    const [view] = await waitForPrompts(runtime, child, 1)

    // Each prompt of a consult owns a run of its own: the view's run closes when its
    // terminal arrives, and only then does the intent prompt root freshly.
    await advisorRun(hooks, runtime, child, 'run-advisor-view-1', view, 'VIEW-ONE', 10)
    const intent = (await waitForPrompts(runtime, child, 2)).at(-1)
    assert.notEqual(keyOf(view), keyOf(intent), 'view and intent are two prompts with their own keys')

    await advisorRun(hooks, runtime, child, 'run-advisor-answer-1', intent, 'ADVICE-ONE: ship it behind a flag', 20)

    const result = await within(pending, SETTLE_MS)
    assert.notEqual(result, null, 'the published Completed of the intent run must settle the consult (not the 120 s fallback)')
    assert.match(String(result), /ADVICE-ONE: ship it behind a flag/, 'the caller receives the advisor prose')
    assert.doesNotMatch(String(result), /advisor did not answer|advisor-unavailable/)
  })
})

const firstConsultSettled = async (hooks, createdIds, runtime, owner) => {
  const first = advisorCall(hooks, runtime, owner, 'first question', 'call-advisor-a')
  const child = await waitForChild(createdIds)
  const [firstView] = await waitForPrompts(runtime, child, 1)
  await advisorRun(hooks, runtime, child, 'run-advisor-a-view', firstView, 'VIEW-A', 10)
  const firstIntent = (await waitForPrompts(runtime, child, 2)).at(-1)
  await advisorRun(hooks, runtime, child, 'run-advisor-a', firstIntent, 'ADVICE-A', 20)
  const firstResult = await within(first, SETTLE_MS)
  assert.notEqual(firstResult, null, 'positive control: the first consult settles on its own intent run')
  assert.match(String(firstResult), /ADVICE-A/)
  return child
}

test('WHAT[advisor-004] the earlier consult\'s sticky Completed never answers a follow-up consult (stale-run rejection)', async () => {
  const owner = `sess-advisor-004r-${Math.random().toString(36).slice(2, 8)}`
  await withAdvisorOwner(owner, async ({ hooks, createdIds, runtime }) => {
    const child = await firstConsultSettled(hooks, createdIds, runtime, owner)
    const second = advisorCall(hooks, runtime, owner, 'second question', 'call-advisor-b')
    second.catch(() => {})
    await waitForPrompts(runtime, child, 3)
    assert.equal(createdIds.length, 1, 'the follow-up reuses the same advisor child')

    const followUpPrompts = await waitForPrompts(runtime, child, 3)
    await advisorRun(hooks, runtime, child, 'run-advisor-b-view', followUpPrompts[2], 'VIEW-B', 20)
    const followUpIntent = (await waitForPrompts(runtime, child, 4)).at(-1)

    // Replayed at subscribe time, the earlier consult's terminal must not answer the new intent.
    assert.equal(await within(second, 300), null, 'an earlier consult\'s Completed never answers the follow-up')

    // The follow-up is left to settle on its own intent run, so nothing stays pending.
    await advisorRun(hooks, runtime, child, 'run-advisor-b', followUpIntent, 'ADVICE-B', 30)
    const followUpResult = await within(second, SETTLE_MS)
    assert.notEqual(followUpResult, null, 'the follow-up settles on its own intent run')
    assert.match(String(followUpResult), /ADVICE-B/)
  })
})

test('WHAT[advisor-004] a follow-up consult ignores the earlier consult and its own view run, and answers on its intent run', async () => {
  const owner = `sess-advisor-004s-${Math.random().toString(36).slice(2, 8)}`
  await withAdvisorOwner(owner, async ({ hooks, createdIds, runtime }) => {
    const child = await firstConsultSettled(hooks, createdIds, runtime, owner)

    const second = advisorCall(hooks, runtime, owner, 'second question', 'call-advisor-b')
    const prompts = await waitForPrompts(runtime, child, 3)
    assert.equal(createdIds.length, 1, 'the follow-up reuses the same advisor child')
    const secondView = prompts[2]

    // The view prompt's own run finishing first is not this consult's answer either.
    await advisorRun(hooks, runtime, child, 'run-advisor-b-view', secondView, 'VIEW-ACK', 20)
    assert.equal(await within(second, 300), null, 'the view prompt\'s run never answers the intent')

    // The earlier consult's Completed is still the sticky terminal of this child: replayed at
    // subscribe time it must not answer the new intent.
    assert.equal(await within(second, 300), null, 'an earlier consult\'s Completed never answers the follow-up')

    const secondIntent = (await waitForPrompts(runtime, child, 4)).at(-1)
    await advisorRun(hooks, runtime, child, 'run-advisor-b', secondIntent, 'ADVICE-B', 30)
    const secondResult = await within(second, SETTLE_MS)
    assert.notEqual(secondResult, null, 'the follow-up settles on its own intent run')
    assert.match(String(secondResult), /ADVICE-B/)
    assert.doesNotMatch(String(secondResult), /ADVICE-A|VIEW-ACK/)
  })
})

const identityValue = value => Array.isArray(value) ? value.at(-1) : value
const advisorRoots = (runtime, child) => factPayloads(runtime.directory, 'AuthorityRootAccepted')
  .filter(payload => identityValue(payload.SessionId) === child)

const claimAdvisorInput = async (runtime, owner, child, label) => {
  const profile = dispatch.projectionObservation(runtime.journal, owner).activeLogicalRun
  const issued = authority.issueInheritedIdentitySeed('advisor', profile)
  assert.equal(issued.ok, true, issued.error)
  let metadata
  const sent = await dispatch.sendAgentOwnerRoot({
    SubscribeTerminal: () => ({ Dispose() {} }),
    SendPrompt: async (session, _text, options) => {
      assert.equal(session, child)
      metadata = options.Metadata
      return dispatch.admittedWithReceipt(`advisor-proof-receipt-${label}`)
    },
  }, runtime.journal, child, `Advisor ownership proof ${label}`, issued.value)
  assert.equal(sent.ok, true, sent.error)
  assert.equal(metadata[PROMPT_KEY], sent.key, 'the physical input carries the key actually handed to the Host')
  return {
    id: `advisor-proof-physical-${label}`, sessionID: child, role: 'user', agent: 'advisor',
    model: { providerID: 'provider', modelID: 'advisor-model' }, time: { created: 2 },
    metadata: { [PROMPT_KEY]: sent.key },
    parts: [{ type: 'text', text: `Advisor ownership proof ${label}`, metadata: { [PROMPT_KEY]: sent.key } }],
  }
}

const presentAdvisorInput = async (hooks, runtime, input) => {
  runtime.pushHostMessage(input.sessionID, input)
  await hooks['chat.message']({ sessionID: input.sessionID, messageID: input.id, agent: 'advisor' },
    { message: input, parts: input.parts })
}

const assertNoAdvisorRoot = (runtime, child, physical) => {
  assert.equal(dispatch.projectionObservation(runtime.journal, child).activeLogicalRun, null,
    'a missing live seat proof must not establish an authority root')
  assert.equal(advisorRoots(runtime, child).length, 0, 'no AuthorityRootAccepted is written for the unproved child')
  assert.equal(executionStatus.query(runtime.journal, child, physical).accepted, false,
    'the unproved physical input has no ChatExecution Accepted')
}

const assertInternalLeafRoot = (runtime, owner, child, physical) => {
  const accepted = advisorRoots(runtime, child)
    .find(payload => identityValue(payload.AuthorityRootUserMessageId) === physical)
  assert.ok(accepted, 'a real successful seat permits the exact physical root')
  assert.equal(accepted.ExpectedClosureKind, 'AgentOwnerInternalLeaf')
  assert.equal(accepted.AgentOwnerLife, 'InternalLeaf')
  assert.equal(identityValue(accepted.OwnerLogicalRunId),
    dispatch.projectionObservation(runtime.journal, owner).activeLogicalRun.logicalRun)
  assert.equal(executionStatus.query(runtime.journal, child, physical).accepted, true)
}

test('WHAT[advisor-004] a successful seat in another journal cannot authorize the same caller and child labels', { timeout: 10000 }, async () => {
  const owner = `advisor-proof-journal-${Math.random().toString(36).slice(2, 8)}`
  await withAdvisorOwner(owner, async (a) => {
    const child = await firstConsultSettled(a.hooks, a.createdIds, a.runtime, owner)
    await withAdvisorOwner(owner, async (b) => {
      assert.notEqual(a.runtime.runtimeId, b.runtime.runtimeId)
      assert.notEqual(a.runtime.directory, b.runtime.directory)
      const unproved = await claimAdvisorInput(b.runtime, owner, child, `${owner}-foreign`)
      await presentAdvisorInput(b.hooks, b.runtime, unproved).catch(() => {})
      assertNoAdvisorRoot(b.runtime, child, unproved.id)
      assert.equal(b.createdIds.length, 0, 'the second journal never created this advisor seat')

      const proved = await claimAdvisorInput(a.runtime, owner, child, `${owner}-local`)
      await presentAdvisorInput(a.hooks, a.runtime, proved)
      assertInternalLeafRoot(a.runtime, owner, child, proved.id)
      await advisorRun(a.hooks, a.runtime, child, `${owner}-local-run`, proved, 'LOCAL SEAT PROOF', 30)
    })
  })
})

test('WHAT[advisor-004] a pending Host child creation proves no internal leaf until the real creation succeeds', { timeout: 10000 }, async () => {
  const owner = `advisor-proof-pending-${Math.random().toString(36).slice(2, 8)}`
  await withAdvisorOwner(owner, async ({ hooks, createdIds, runtime }) => {
    const createEntered = Promise.withResolvers()
    const releaseCreate = Promise.withResolvers()
    const originalCreate = runtime.client.session.create
    runtime.client.session.create = async args => {
      const created = await originalCreate(args)
      createEntered.resolve(created.data.id)
      await releaseCreate.promise
      return created
    }
    const pending = advisorCall(hooks, runtime, owner, 'review after the Host confirms creation', 'advisor-proof-create')
    pending.catch(() => {})
    try {
      const child = await Promise.race([createEntered.promise,
        pending.then(output => { throw new Error(`consult ended before Host creation: ${output}`) })])
      const unproved = await claimAdvisorInput(runtime, owner, child, `${owner}-before-create`)
      await presentAdvisorInput(hooks, runtime, unproved).catch(() => {})
      assertNoAdvisorRoot(runtime, child, unproved.id)
      assert.equal(runtime.prompts.length, 0, 'the held creation has not sent the consult view')

      releaseCreate.resolve()
      const [view] = await waitForPrompts(runtime, child, 1)
      await runtime.awaitIngress()
      assertInternalLeafRoot(runtime, owner, child, view.id)
      assert.equal(executionStatus.query(runtime.journal, child, unproved.id).accepted, false,
        'successful creation does not retroactively accept the earlier refused physical input')
      await advisorRun(hooks, runtime, child, `${owner}-view-run`, view, 'VIEW AFTER CREATE', 10)
      const intent = (await waitForPrompts(runtime, child, 2)).at(-1)
      await advisorRun(hooks, runtime, child, `${owner}-intent-run`, intent, 'ADVICE AFTER CREATE', 20)
      const result = await within(pending, 3000)
      assert.notEqual(result, null, 'the real successful creation and Host terminals settle the consult')
      assert.match(String(result), /ADVICE AFTER CREATE/)
      assert.equal(createdIds.length, 1)
    } finally {
      releaseCreate.resolve()
      runtime.client.session.create = originalCreate
    }
  })
})
