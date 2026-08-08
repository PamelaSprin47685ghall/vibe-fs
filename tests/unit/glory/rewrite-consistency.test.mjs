// tests/unit/glory/rewrite-consistency.test.mjs — GLORY-015/013 seal contract.
//
// The Birth rewrite is a request-level view transform: the Host persists the
// raw conversation, so EVERY provider request must re-apply the narrative to
// the Life's Opening message with byte-identical results — otherwise the next
// request breaks the ARCH-004 seal (measured in e2e as
// `SEAL-DIFF msg[1] user != user` on the Activation request).

import assert from 'node:assert/strict'
import test from 'node:test'
import { acceptAuthorityRoot, withExecutablePlugin } from '../plugin/plugin-fixture.mjs'

const SESSION = 'ses_glory_rewrite'

const userMessage = (id, text) => ({
  info: { id, role: 'user', sessionID: SESSION },
  parts: [{ type: 'text', text }],
})

const assistantMessage = (id, text) => ({
  info: { id, role: 'assistant', sessionID: SESSION },
  parts: [{ type: 'text', text }],
})

// Birth/Reawakening is multi-part: human raw + synthetic guidance (synthetic=true).
// Join all text parts so planning tail is visible across the message view.
const textOf = (messages, id) => {
  const message = messages.find((m) => m?.info?.id === id || m?.id === id)
  const texts = (message?.parts ?? [])
    .filter((p) => p?.type === 'text')
    .map((p) => p?.text ?? '')
  if (texts.length === 0) return undefined
  return texts.join('\n')
}

// Host-sent activation is comment-only SyntheticToml; this fixture injects the
// same imperative body the production surface still contains (match substring).
const ACTIVATION =
  '# Now complete it yourself.\n# Carry out the work you described until the final goal is fully achieved.\n#\n# Planning is not completion.\n# Delegation is not completion.\n# A child finishing is not completion.\n# A successful command is not completion while meaningful uncertainty remains.\n# An explanation of the work is not the work itself.\n# A partial implementation is not completion merely because the remaining work is difficult.\n# As long as any useful action remains, continue.\n'

test('GLORY_015_opening_rewrite_is_byte_identical_across_requests', async () => {
  await withExecutablePlugin(async (hooks, _directory, _createdIds, runtime) => {
    acceptAuthorityRoot(runtime, SESSION, 'fast-manager')

    // Request 1: the planning request. The Opening message is rewritten once.
    const out1 = { messages: [userMessage('msg-open-1', 'Start manager work.')] }
    await hooks['experimental.chat.messages.transform']({ sessionID: SESSION }, out1)

    const firstRewritten = textOf(out1.messages, 'msg-open-1')
    assert.ok(firstRewritten.startsWith('Start manager work.'), `opening must keep the raw text: ${firstRewritten}`)
    assert.ok(firstRewritten.includes('If I want to complete the request above'), 'planning tail must be attached')

    // Request 2: the Activation request. The Host reads the PERSISTED (raw)
    // conversation, appends the Activation continuation, and transforms again.
    const out2 = {
      messages: [
        userMessage('msg-open-1', 'Start manager work.'),
        assistantMessage('asst-1', 'Plan: verify, then finish.'),
        userMessage('msg-act-1', ACTIVATION),
      ],
    }
    await hooks['experimental.chat.messages.transform']({ sessionID: SESSION }, out2)

    const secondRewritten = textOf(out2.messages, 'msg-open-1')
    assert.equal(
      secondRewritten,
      firstRewritten,
      'the opening message must rewrite to the same bytes on every request',
    )
    assert.equal(
      textOf(out2.messages, 'msg-act-1'),
      ACTIVATION,
      'the activation continuation must never be rewritten',
    )
  })
})

test('GLORY_012_host_title_request_never_opens_a_life', async () => {
  // Host 1.18 title requests carry the marker on the message's top-level
  // `content` field; they are Host-synthesized and must not open a Life or be
  // rewritten (measured e2e regression: the title request opened the Life, the
  // planning turn was skipped, and the Activation fired on the title turn).
  await withExecutablePlugin(async (hooks, _directory, _createdIds, runtime) => {
    acceptAuthorityRoot(runtime, SESSION, 'fast-manager')

    const outTitle = {
      messages: [
        {
          content: 'Generate a title for this conversation:',
          info: { id: 'msg-title-1', role: 'user', sessionID: SESSION },
          parts: [{ type: 'text', text: 'Generate a title for this conversation:' }],
        },
      ],
    }
    await hooks['experimental.chat.messages.transform']({ sessionID: SESSION }, outTitle)
    assert.equal(
      textOf(outTitle.messages, 'msg-title-1'),
      'Generate a title for this conversation:',
      'title marker must never be rewritten',
    )

    // The real HumanRoot still opens the Life on the next request.
    const outPlan = { messages: [userMessage('msg-open-1', 'Start manager work.')] }
    await hooks['experimental.chat.messages.transform']({ sessionID: SESSION }, outPlan)
    assert.ok(
      textOf(outPlan.messages, 'msg-open-1').includes('If I want to complete the request above'),
      'the real HumanRoot must still be Birthed',
    )
  })
})

test('GLORY_015_rewrite_survives_a_persisted_rewritten_message', async () => {
  // Worst case: the Host persisted the transform output (the rewritten opening).
  // Re-transforming must not stack a second tail — the narrative derives from
  // the durable blob, never from the message text.
  await withExecutablePlugin(async (hooks, _directory, _createdIds, runtime) => {
    acceptAuthorityRoot(runtime, SESSION, 'fast-manager')

    const out1 = { messages: [userMessage('msg-open-1', 'Start manager work.')] }
    await hooks['experimental.chat.messages.transform']({ sessionID: SESSION }, out1)
    const rewritten = textOf(out1.messages, 'msg-open-1')

    // Simulate a persisted rewritten opening plus a work-time request.
    const out2 = {
      messages: [
        userMessage('msg-open-1', rewritten),
        assistantMessage('asst-1', 'Plan.'),
        userMessage('msg-work-1', 'Continue working.'),
      ],
    }
    await hooks['experimental.chat.messages.transform']({ sessionID: SESSION }, out2)

    const re = textOf(out2.messages, 'msg-open-1')
    assert.equal(re, rewritten, 'no stacked tail on a persisted rewritten message')
    assert.equal(textOf(out2.messages, 'msg-work-1'), 'Continue working.', 'work-time message never rewritten')
  })
})
