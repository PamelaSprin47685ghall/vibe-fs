// Moved from tests/unit/glory/rewrite-consistency.test.mjs (cutover Wave 2a); owner: finality.
//
// GLORY-015/013 seal contract. The Birth rewrite is a request-level view
// transform: the Host persists the raw conversation, so EVERY provider request
// must re-apply the narrative to the Life's Opening message with byte-identical
// results — otherwise the next request breaks the ARCH-004 seal (measured in
// e2e as `SEAL-DIFF msg[1] user != user` on the Activation request).

import assert from 'node:assert/strict'
import test from 'node:test'
import { acceptAuthorityRoot, withExecutablePlugin } from '../../verification-system/tests/support/plugin-fixture.mjs'

const SESSION = 'ses_glory_rewrite'
const ROOT = `root-${SESSION}`
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

test('WHAT[FINALITY-023] opening rewrite is byte identical across requests', async () => {
  await withExecutablePlugin(async (hooks, _directory, _createdIds, runtime) => {
    await acceptAuthorityRoot(runtime, SESSION, 'fast-manager')
    const out1 = { messages: [userMessage(ROOT, 'Start manager work.')] }
    await hooks['experimental.chat.messages.transform']({ sessionID: SESSION }, out1)

    const firstRewritten = textOf(out1.messages, ROOT)
    assert.ok(firstRewritten.startsWith('Start manager work.'), `opening must keep the raw text: ${firstRewritten}`)
    assert.ok(firstRewritten.includes('The Planning Table'), 'Planning Table must be attached')

    // Request 2: the Activation request. The Host reads the PERSISTED (raw)
    // conversation, appends the Activation continuation, and transforms again.
    const out2 = {
      messages: [
        userMessage(ROOT, 'Start manager work.'),
        assistantMessage('asst-1', 'Plan: verify, then finish.'),
        userMessage('msg-act-1', ACTIVATION),
      ],
    }
    await hooks['experimental.chat.messages.transform']({ sessionID: SESSION }, out2)

    const secondRewritten = textOf(out2.messages, ROOT)
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

test('WHAT[FINALITY-022] host title request never opens a life', async () => {
  // Host 1.18 title requests carry the marker on the message's top-level
  // `content` field; they are Host-synthesized and must not open a Life or be
  // rewritten (measured e2e regression: the title request opened the Life, the
  // planning turn was skipped, and the Activation fired on the title turn).
  await withExecutablePlugin(async (hooks, _directory, _createdIds, runtime) => {
    await acceptAuthorityRoot(runtime, SESSION, 'fast-manager')

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
    const outPlan = { messages: [userMessage(ROOT, 'Start manager work.')] }
    await hooks['experimental.chat.messages.transform']({ sessionID: SESSION }, outPlan)
    assert.ok(
      textOf(outPlan.messages, ROOT).includes('The Planning Table'),
      'the real HumanRoot must still be Birthed',
    )
  })
})

test('WHAT[FINALITY-022] active HumanRoot profile does not make another user message a root', async () => {
  await withExecutablePlugin(async (hooks, _directory, _createdIds, runtime) => {
    await acceptAuthorityRoot(runtime, SESSION, 'fast-manager')

    const out = { messages: [userMessage('msg-not-root', 'ordinary later user-shaped message')] }
    await hooks['experimental.chat.messages.transform']({ sessionID: SESSION }, out)

    assert.equal(
      textOf(out.messages, 'msg-not-root'),
      'ordinary later user-shaped message',
      'Life opening requires the exact AuthorityRootUserMessageId, not session-level HumanRoot authority',
    )
  })
})

test('WHAT[FINALITY-023] opening rewrite survives a persisted rewritten message', async () => {
  // Worst case: the Host persisted the transform output (the rewritten opening).
  // Re-transforming must not stack a second tail — the narrative derives from
  // the durable blob, never from the message text.
  await withExecutablePlugin(async (hooks, _directory, _createdIds, runtime) => {
    await acceptAuthorityRoot(runtime, SESSION, 'fast-manager')

    const out1 = { messages: [userMessage(ROOT, 'Start manager work.')] }
    await hooks['experimental.chat.messages.transform']({ sessionID: SESSION }, out1)
    const rewritten = textOf(out1.messages, ROOT)

    // Simulate a persisted rewritten opening plus a work-time request.
    const out2 = {
      messages: [
        userMessage(ROOT, rewritten),
        assistantMessage('asst-1', 'Plan.'),
        userMessage('msg-work-1', 'Continue working.'),
      ],
    }
    await hooks['experimental.chat.messages.transform']({ sessionID: SESSION }, out2)

    const re = textOf(out2.messages, ROOT)
    assert.equal(re, rewritten, 'no stacked tail on a persisted rewritten message')
  })
})

test('WHAT[FINALITY-024] work-time messages are never rewritten', async () => {
  await withExecutablePlugin(async (hooks, _directory, _createdIds, runtime) => {
    await acceptAuthorityRoot(runtime, SESSION, 'fast-manager')

    const out1 = { messages: [userMessage(ROOT, 'Start manager work.')] }
    await hooks['experimental.chat.messages.transform']({ sessionID: SESSION }, out1)

    const out2 = {
      messages: [
        userMessage(ROOT, textOf(out1.messages, ROOT)),
        assistantMessage('asst-1', 'Plan.'),
        userMessage('msg-work-1', 'Continue working.'),
      ],
    }
    await hooks['experimental.chat.messages.transform']({ sessionID: SESSION }, out2)

    assert.equal(textOf(out2.messages, 'msg-work-1'), 'Continue working.', 'work-time message never rewritten')
  })
})
