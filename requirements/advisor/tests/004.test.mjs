import assert from 'node:assert/strict'
import test from 'node:test'

import {
  acceptAuthorityRoot,
  openIncumbency,
  withExecutablePlugin,
} from '../../verification-system/tests/support/plugin-fixture.mjs'

// advisor-[004] / F39-01: `this.advisor` through the real plugin. The seat creates its advisor
// child, sends the view and the intent as two Host prompts, and returns the prose the advisor
// child's run actually produced once the production terminal path publishes that run.
// The fixture only plays the Host: it appends the advisor's assistant message (HOST-010/011:
// its id is the provider run, its parentID the user message it answers) and the Host events.
// It never publishes a terminal and never writes a durable fact.

const PROMPT_KEY = 'wanxiangshu_prompt_key'
const SETTLE_MS = 20_000

const advisorCall = (hooks, owner, intent, callID) =>
  hooks.tool['todowrite-for-manager'].execute(
    {
      program: `class Js extends JsProgram { async run() { const advised = await this.advisor(${JSON.stringify(intent)}, {}); return { advised }; } }`,
    },
    { sessionID: owner, agent: 'manager', callID, messageID: `msg-${callID}` },
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
    runtime.pushHostMessage(owner, {
      id: `root-${owner}`,
      role: 'user',
      parts: [{ type: 'text', text: 'review the parser rewrite' }],
    })
    return body({ hooks, createdIds, runtime })
  })

test('WHAT[advisor-004] a consult returns the prose of the advisor run that answers its own intent (F39-01)', async () => {
  const owner = `sess-advisor-004-${Math.random().toString(36).slice(2, 8)}`
  await withAdvisorOwner(owner, async ({ hooks, createdIds, runtime }) => {
    const pending = advisorCall(hooks, owner, 'is the rewrite safe?', 'call-advisor-1')
    const child = await waitForChild(createdIds)
    const [view] = await waitForPrompts(runtime, child, 1)

    // Each prompt of a consult owns a run of its own: the view's run closes when its
    // terminal arrives, and only then does the intent prompt root freshly.
    await advisorRun(hooks, runtime, child, 'run-advisor-view-1', view, 'VIEW-ONE', 10)
    const [intent] = await waitForPrompts(runtime, child, 2)
    assert.notEqual(keyOf(view), keyOf(intent), 'view and intent are two prompts with their own keys')

    await advisorRun(hooks, runtime, child, 'run-advisor-answer-1', intent, 'ADVICE-ONE: ship it behind a flag', 20)

    const result = await within(pending, SETTLE_MS)
    assert.notEqual(result, null, 'the published Completed of the intent run must settle the consult (not the 120 s fallback)')
    assert.match(String(result), /ADVICE-ONE: ship it behind a flag/, 'the caller receives the advisor prose')
    assert.doesNotMatch(String(result), /advisor did not answer|advisor-unavailable/)
  })
})

const firstConsultSettled = async (hooks, createdIds, runtime, owner) => {
  const first = advisorCall(hooks, owner, 'first question', 'call-advisor-a')
  const child = await waitForChild(createdIds)
  const [firstView] = await waitForPrompts(runtime, child, 1)
  await advisorRun(hooks, runtime, child, 'run-advisor-a-view', firstView, 'VIEW-A', 10)
  const [firstIntent] = await waitForPrompts(runtime, child, 2)
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
    const second = advisorCall(hooks, owner, 'second question', 'call-advisor-b')
    second.catch(() => {})
    await waitForPrompts(runtime, child, 3)
    assert.equal(createdIds.length, 1, 'the follow-up reuses the same advisor child')

    const followUpPrompts = await waitForPrompts(runtime, child, 3)
    await advisorRun(hooks, runtime, child, 'run-advisor-b-view', followUpPrompts[2], 'VIEW-B', 20)
    const [, followUpIntent] = await waitForPrompts(runtime, child, 4)

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

    const second = advisorCall(hooks, owner, 'second question', 'call-advisor-b')
    const prompts = await waitForPrompts(runtime, child, 3)
    assert.equal(createdIds.length, 1, 'the follow-up reuses the same advisor child')
    const secondView = prompts[2]

    // The view prompt's own run finishing first is not this consult's answer either.
    await advisorRun(hooks, runtime, child, 'run-advisor-b-view', secondView, 'VIEW-ACK', 20)
    assert.equal(await within(second, 300), null, 'the view prompt\'s run never answers the intent')

    // The earlier consult's Completed is still the sticky terminal of this child: replayed at
    // subscribe time it must not answer the new intent.
    assert.equal(await within(second, 300), null, 'an earlier consult\'s Completed never answers the follow-up')

    const [, secondIntent] = await waitForPrompts(runtime, child, 4)
    await advisorRun(hooks, runtime, child, 'run-advisor-b', secondIntent, 'ADVICE-B', 30)
    const secondResult = await within(second, SETTLE_MS)
    assert.notEqual(secondResult, null, 'the follow-up settles on its own intent run')
    assert.match(String(secondResult), /ADVICE-B/)
    assert.doesNotMatch(String(secondResult), /ADVICE-A|VIEW-ACK/)
  })
})
