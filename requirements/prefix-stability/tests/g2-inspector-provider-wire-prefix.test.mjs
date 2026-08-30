// PREFIX-STABILITY-001 / PREFIX-STABILITY-013 — the append-only provider prefix
// law on a reused Inspector child. Delegation lifecycle is exercised through its
// opaque owner surface; provider-visible bytes are decoded through the Host codec
// and compared only by the ProviderProjection owner.

import assert from 'node:assert/strict'
import { createHash } from 'node:crypto'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'

import * as canonicalJson from '../../../dist/OpenCode/Codec/CanonicalJsonSurface.js'
import * as providerCodec from '../../../dist/OpenCode/Codec/ProviderProjectionSurface.js'
import * as providerProjection from '../../../dist/Participant/Provider/Projection/Surface.js'
import * as delegation from '../../../dist/Execution/Delegation/SyncDelegate/Surface.js'

const INSPECTOR_AGENT = 'fast-inspector'
const INSPECTOR_PROVIDER = 'g2-test-provider'
const INSPECTOR_MODEL_ID = 'g2-inspector-model'
const OWNER = 'ses_owner_g2_wire'
const QUESTIONS = ['Q1', 'Q2', 'Q3']
const ANSWERS = ['answer Q1', 'answer Q2', 'answer Q3']
const TOOLS = ['read', 'write']
const SYSTEM = ['sys']

const waitFor = async (predicate, message, ms = 2000) => {
  const deadline = Date.now() + ms
  while (!predicate()) {
    if (Date.now() >= deadline) throw new Error(message)
    await new Promise((resolve) => setImmediate(resolve))
  }
}

const textPart = (id, text) => providerCodec.opencodeTextPart(`${id}-part`, 'text', text, false)

const hostMessage = (id, role, text) => {
  const parts = [textPart(id, text)]
  if (role === 'user') {
    return providerCodec.opencodeUserMessage(id, role, OWNER, INSPECTOR_AGENT, null, parts)
  }
  return providerCodec.opencodeAssistantMessage(
    id,
    null,
    role,
    OWNER,
    INSPECTOR_AGENT,
    INSPECTOR_PROVIDER,
    INSPECTOR_MODEL_ID,
    false,
    null,
    parts,
  )
}

const providerWire = (transcript) => {
  const decoded = providerCodec.decodeMessageView(
    transcript.map(({ id, role, text }) => hostMessage(id, role, text)),
  )
  return {
    ...decoded,
    modelId: INSPECTOR_MODEL_ID,
    providerId: INSPECTOR_PROVIDER,
    system: SYSTEM,
    tools: TOOLS,
    variant: null,
  }
}

const bodyOf = (transcript) => ({
  model: INSPECTOR_MODEL_ID,
  tools: TOOLS.map((name) => ({ type: 'function', function: { name } })),
  messages: transcript.map(({ role, text }) => ({ role, content: text })),
})

const digest = (wire) => providerProjection.sealDigest(
  (input) => createHash('sha256').update(input).digest('hex'),
  wire,
)

const resultValue = async (pending, label) => {
  const result = await pending
  assert.equal(result.ok, true, `${label} delegation must complete: ${result.error ?? ''}`)
  return result.value
}

const mutatedWire = (wire, mutation) => mutation({
  ...wire,
  messages: wire.messages.map((message) => ({
    ...message,
    parts: message.parts.map((part) => ({ ...part })),
  })),
})

test('WHAT[PREFIX-STABILITY-001] G2_inspector_Q1_Q2_Q3_provider_wire_append_only_prefix', async () => {
  const directory = mkdtempSync(join(tmpdir(), 'wxs-g2-inspector-wire-'))
  const harness = await delegation.create(directory)
  try {
    const transcript = []
    const wires = []
    const bodies = []
    const childIds = []

    for (let ordinal = 0; ordinal < QUESTIONS.length; ordinal += 1) {
      const question = QUESTIONS[ordinal]
      const answer = ANSWERS[ordinal]
      const pending = delegation.invoke(harness, OWNER, 'Inspector', question)
      await waitFor(() => delegation.childCount(harness) === 1, `${question} child missing`)

      const child = delegation.child(harness, OWNER, 'Inspector')
      childIds.push(child)
      assert.equal(delegation.childCount(harness), 1, 'Inspector child must be reused')
      assert.equal(
        delegation.vocabulary('Inspector', 'Fast', OWNER).agent,
        INSPECTOR_AGENT,
        'delegation surface must retain the Inspector agent binding',
      )

      transcript.push({ id: `user-${ordinal + 1}`, role: 'user', text: question })
      const body = bodyOf(transcript)
      const wire = providerWire(transcript)
      bodies.push(body)
      wires.push(wire)

      assert.equal(body.model, INSPECTOR_MODEL_ID)
      assert.equal(body.messages.at(-1).content, question)
      assert.equal(wire.modelId, INSPECTOR_MODEL_ID)
      assert.equal(wire.providerId, INSPECTOR_PROVIDER)
      assert.equal(wire.messages.at(-1).parts[0].text, question)

      const settled = await delegation.settle(harness, OWNER, 'Inspector', answer, `asst_q${ordinal + 1}`)
      assert.equal(settled, true, `Q${ordinal + 1} completion must be observed`)
      const completed = await resultValue(pending, `Q${ordinal + 1}`)
      assert.match(completed, new RegExp(`answer Q${ordinal + 1}`))

      if (ordinal < QUESTIONS.length - 1) {
        transcript.push({ id: `assistant-${ordinal + 1}`, role: 'assistant', text: answer })
      }
    }

    assert.equal(wires.length, 3)
    assert.deepEqual(wires.map((wire) => wire.messages.map((message) => message.parts[0]?.text)), [
      ['Q1'],
      ['Q1', 'answer Q1', 'Q2'],
      ['Q1', 'answer Q1', 'Q2', 'answer Q2', 'Q3'],
    ])
    assert.deepEqual(bodies.map((body) => body.messages.map((message) => message.content)), [
      ['Q1'],
      ['Q1', 'answer Q1', 'Q2'],
      ['Q1', 'answer Q1', 'Q2', 'answer Q2', 'Q3'],
    ])
    assert.deepEqual(childIds, [childIds[0], childIds[0], childIds[0]])

    const [wireQ1, wireQ2, wireQ3] = wires
    assert.equal(providerProjection.isAppendOnlyPrefix(wireQ1, wireQ2), true)
    assert.equal(providerProjection.isAppendOnlyPrefix(wireQ2, wireQ3), true)
    assert.equal(providerProjection.isAppendOnlyPrefix(wireQ2, wireQ1), false, 'prefix must be directional')

    const [digestQ1, digestQ2, digestQ3] = wires.map(digest)
    assert.notEqual(digestQ1, digestQ2, 'the appended Q2 turn changes the provider seal digest')
    assert.notEqual(digestQ2, digestQ3, 'the appended Q3 turn changes the provider seal digest')
    assert.equal(digestQ1, digest(wireQ1), 'seal digest is deterministic')

    const reorderedBody = {
      messages: bodies[0].messages,
      tools: bodies[0].tools,
      model: bodies[0].model,
    }
    assert.equal(
      canonicalJson.canonicalJson(bodies[0]),
      canonicalJson.canonicalJson(reorderedBody),
      'canonical JSON preserves body identity despite object insertion order',
    )

    const changedAnswer = mutatedWire(wireQ2, (value) => {
      value.messages[0].parts[0].text = 'Q1 changed'
      return value
    })
    assert.equal(providerProjection.isAppendOnlyPrefix(wireQ1, changedAnswer), false, 'historical byte mutation breaks PREFIX LAW')

    const changedTools = { ...wireQ2, tools: ['read'] }
    assert.equal(providerProjection.isAppendOnlyPrefix(wireQ1, changedTools), false, 'tool-set mutation breaks PREFIX LAW')
  } finally {
    delegation.dispose(harness)
    rmSync(directory, { recursive: true, force: true })
  }
})
