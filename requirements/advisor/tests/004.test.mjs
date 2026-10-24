import assert from 'node:assert/strict'
import test from 'node:test'

import {
  acceptAuthorityRoot,
  openIncumbency,
  withExecutablePlugin,
} from '../../verification-system/tests/support/plugin-fixture.mjs'

// advisor-[004] / F39-01: `this.advisor` through the real plugin. The seat creates its advisor
// child, sends ONLY the current intent as one Host prompt, and returns the prose the advisor
// child's run actually produced once the production terminal path publishes that run.
// The fixture only plays the Host: it appends the advisor's assistant message (HOST-010/011:
// its id is the provider run, its parentID the user message it answers) and the Host events.
// It never publishes a terminal and never writes a durable fact.

const PROMPT_KEY = 'wanxiangshu_prompt_key'
const SETTLE_MS = 20_000
const ADVISOR_PLAN_B =
  'The advisor consultation is still running. Keep waiting for the answer to this consultation’s own intent prompt. Do not treat a view acknowledgement, an earlier consultation, or reuse of the same child session as evidence that the current question is answered. If no answer arrives within the fixture deadline, report the outstanding prompt and run identities instead of returning old prose. The host sends only the current intent; wait for its own physical completed run before assessing the advice.'

const advisorCall = (hooks, owner, intent, callID) =>
  hooks.tool['todowrite-for-manager'].execute(
    {
      name: `Advisor consultation ${callID}`,
      planB: ADVISOR_PLAN_B,
      program: `class Js extends JsProgram { async run() { const advised = await this.advisor(${JSON.stringify(intent)}, []); return { advised }; } }`,
          prefixUntil: 0,
          carry: []
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
    const [prompt] = await waitForPrompts(runtime, child, 1)
    const sent = prompt.parts.map(part => part.text ?? '').join('\n')
    assert.match(sent, /is the rewrite safe\?/, 'current intent is present')
    assert.doesNotMatch(sent, /review the parser rewrite|\[\[context\]\]|attached_view/, 'no caller history reaches Advisor')

    await advisorRun(hooks, runtime, child, 'run-advisor-answer-1', prompt, 'ADVICE-ONE: ship it behind a flag', 20)

    const result = await within(pending, SETTLE_MS)
    assert.notEqual(result, null, 'the published Completed of the intent run must settle the consult (not the 120 s fallback)')
    assert.match(String(result), /ADVICE-ONE: ship it behind a flag/, 'the caller receives the advisor prose')
    assert.doesNotMatch(String(result), /advisor did not answer|advisor-unavailable/)
  })
})

const firstConsultSettled = async (hooks, createdIds, runtime, owner) => {
  const first = advisorCall(hooks, owner, 'first question', 'call-advisor-a')
  const child = await waitForChild(createdIds)
  const [firstPrompt] = await waitForPrompts(runtime, child, 1)
  await advisorRun(hooks, runtime, child, 'run-advisor-a', firstPrompt, 'ADVICE-A', 20)
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
    assert.equal(createdIds.length, 1, 'the follow-up reuses the same advisor child')

    const followUpPrompts = await waitForPrompts(runtime, child, 2)
    const followUpIntent = followUpPrompts[1]
    const text = followUpIntent.parts.map(part => part.text ?? '').join('\n')
    assert.match(text, /second question/)
    assert.doesNotMatch(text, /first question|\[\[context\]\]/, 'no previous intent or prior history copied into a follow-up')

    // Replayed at subscribe time, the earlier consult's terminal must not answer the new intent.
    assert.equal(await within(second, 300), null, 'an earlier consult\'s Completed never answers the follow-up')

    // The follow-up is left to settle on its own intent run, so nothing stays pending.
    await advisorRun(hooks, runtime, child, 'run-advisor-b', followUpIntent, 'ADVICE-B', 30)
    const followUpResult = await within(second, SETTLE_MS)
    assert.notEqual(followUpResult, null, 'the follow-up settles on its own intent run')
    assert.match(String(followUpResult), /ADVICE-B/)
  })
})

test('WHAT[advisor-004] a follow-up consult ignores the earlier consult and answers on its own run', async () => {
  const owner = `sess-advisor-004s-${Math.random().toString(36).slice(2, 8)}`
  await withAdvisorOwner(owner, async ({ hooks, createdIds, runtime }) => {
    const child = await firstConsultSettled(hooks, createdIds, runtime, owner)

    const second = advisorCall(hooks, owner, 'second question', 'call-advisor-b')
    const prompts = await waitForPrompts(runtime, child, 2)
    assert.equal(createdIds.length, 1, 'the follow-up reuses the same advisor child')
    const secondPrompt = prompts[1]

    assert.equal(await within(second, 300), null, 'an earlier consult\'s Completed never answers the follow-up')

    await advisorRun(hooks, runtime, child, 'run-advisor-b', secondPrompt, 'ADVICE-B', 30)
    const secondResult = await within(second, SETTLE_MS)
    assert.notEqual(secondResult, null, 'the follow-up settles on its own intent run')
    assert.match(String(secondResult), /ADVICE-B/)
  })
})
