import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { createHash } = await import("node:crypto");
const { mkdtempSync, rmSync } = await import("node:fs");
const { tmpdir } = await import("node:os");
const { join } = await import("node:path");
const { default: test } = await import("node:test");
const canonicalJson = await import("../../../dist/OpenCode/Codec/CanonicalJsonSurface.js");
const providerCodec = await import("../../../dist/OpenCode/Codec/ProviderProjectionSurface.js");
const providerProjection = await import("../../../dist/Participant/Provider/Projection/Surface.js");
const delegation = await import("../../../dist/Execution/Delegation/SyncDelegate/Surface.js");

const ENGINEER_AGENT = 'engineer'
const ENGINEER_PROVIDER = 'g2-test-provider'
const ENGINEER_MODEL_ID = 'g2-engineer-model'
const OWNER = 'ses_owner_g2_wire'
const QUESTIONS = ['Q1', 'Q2', 'Q3']
const ANSWERS = ['answer Q1', 'answer Q2', 'answer Q3']
const TOOLS = ['read', 'write']
const SYSTEM = ['sys']
const textPart = (id, text) => providerCodec.opencodeTextPart(`${id}-part`, 'text', text, false)
const hostMessage = (id, role, text) => {
  const parts = [textPart(id, text)]
  if (role === 'user') {
    return providerCodec.opencodeUserMessage(id, role, OWNER, ENGINEER_AGENT, null, parts)
  }
  return providerCodec.opencodeAssistantMessage(
    id,
    null,
    role,
    OWNER,
    ENGINEER_AGENT,
    ENGINEER_PROVIDER,
    ENGINEER_MODEL_ID,
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
    modelId: ENGINEER_MODEL_ID,
    providerId: ENGINEER_PROVIDER,
    system: SYSTEM,
    tools: TOOLS,
    variant: null,
  }
}
const bodyOf = (transcript) => ({
  model: ENGINEER_MODEL_ID,
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

test('WHAT[prefix-stability-001] G2_engineer_Q1_Q2_Q3_provider_wire_append_only_prefix', async () => {
  const directory = mkdtempSync(join(tmpdir(), 'wxs-g2-engineer-wire-'))
  const harness = await delegation.create(directory, [{ sessionId: OWNER, agent: 'manager' }])
  try {
    const transcript = []
    const wires = []
    const bodies = []
    const childIds = []

    for (let ordinal = 0; ordinal < QUESTIONS.length; ordinal += 1) {
      const question = QUESTIONS[ordinal]
      const answer = ANSWERS[ordinal]
      const pending = delegation.invoke(harness, OWNER, 'Engineer', question)
      await delegation.awaitPromptCount(harness, OWNER, 'Engineer', ordinal + 1)
      assert.equal(delegation.acceptPrompt(harness, OWNER, 'Engineer', ordinal), true)

      const child = delegation.child(harness, OWNER, 'Engineer')
      assert.notEqual(child, null, `${question} child missing`)
      childIds.push(child)
      assert.equal(delegation.childCount(harness), 1, 'Engineer child must be reused')
      assert.equal(
        delegation.vocabulary('Engineer', 'Fast', OWNER).agent,
        ENGINEER_AGENT,
        'delegation surface must retain the Engineer agent binding',
      )

      transcript.push({ id: `user-${ordinal + 1}`, role: 'user', text: question })
      const body = bodyOf(transcript)
      const wire = providerWire(transcript)
      bodies.push(body)
      wires.push(wire)

      assert.equal(body.model, ENGINEER_MODEL_ID)
      assert.equal(body.messages.at(-1).content, question)
      assert.equal(wire.modelId, ENGINEER_MODEL_ID)
      assert.equal(wire.providerId, ENGINEER_PROVIDER)
      assert.equal(wire.messages.at(-1).parts[0].text, question)

      const settled = await delegation.settle(harness, OWNER, 'Engineer', answer, `asst_q${ordinal + 1}`)
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
}

{
const { default: assert } = await import("node:assert/strict");
const { mkdtempSync, rmSync } = await import("node:fs");
const { tmpdir } = await import("node:os");
const { join } = await import("node:path");
const { default: test } = await import("node:test");
const pair = await import("../../../dist/OpenCode/Host/PairProgrammingThoughtSurface.js");
const providerCodec = await import("../../../dist/OpenCode/Codec/ProviderProjectionSurface.js");
const providerProjection = await import("../../../dist/Participant/Provider/Projection/Surface.js");

const {
  tryInjectWithJournal,
  isPairProgrammingThought,
  source,
  text,
  stableCallId,
} = pair
const userMsg = (id, body = 'hello') => ({
  info: { id, role: 'user' },
  parts: [{ type: 'text', text: body }],
})
const assistantText = (id) => ({
  info: { id, role: 'assistant' },
  parts: [{ type: 'text', text: 'ok' }],
})
const toolCall = (id, tool, callID) => ({
  info: { id, role: 'assistant' },
  parts: [{
    type: 'tool',
    tool,
    callID,
    state: { status: 'pending', input: {}, time: { start: 0 } },
  }],
})
const toolResult = (id, tool, callID, output = 'ok') => ({
  info: { id, role: 'assistant' },
  parts: [{
    type: 'tool',
    tool,
    callID,
    state: { status: 'completed', input: {}, output, time: { start: 0, end: 0 } },
  }],
})
const pairMessages = (messages) => messages.filter((m) => isPairProgrammingThought(m))
const inject = async (journal, session, raw, markerText = text) => {
  const result = await tryInjectWithJournal(journal, session, markerText, raw)
  assert.equal(result.ok, true, `HOST-013 transform must commit the pair: ${result.error ?? ''}`)
  return result.value
}
const wire = (raw) => providerCodec.decodeMessageView(raw)
const assertPrefixLaw = (previous, next, label) => {
  assert.equal(
    providerProjection.isAppendOnlyPrefix(wire(previous), wire(next)),
    true,
    `${label}: previous wire must be an exact prefix of next wire`,
  )
}
const assertWireEqual = (a, b, label) => {
  assert.equal(
    providerProjection.isAppendOnlyPrefix(wire(a), wire(b)),
    true,
    `${label}: wire(a) must be a prefix of wire(b)`,
  )
  assert.equal(
    providerProjection.isAppendOnlyPrefix(wire(b), wire(a)),
    true,
    `${label}: wire(b) must be a prefix of wire(a)`,
  )
}
const toolNames = (messages) => messages.map((m) => m.parts[0]?.tool)
const guidanceSuffixCount = (messages) =>
  messages
    .map((m) => m.parts[0]?.state?.output ?? m.parts[0]?.state?.error ?? '')
    .filter((value) => typeof value === 'string')
    .map((value) => (value.match(/\0/g) ?? []).length)
const openJournal = async (dir) => {
  const opened = await pair.createJournal(dir)
  assert.equal(opened.ok, true, JSON.stringify(opened))
  return opened
}
const durablePairCount = (journal, session) => pair.pairCount(journal, session)
const mulberry32 = (seed) => {
  let a = seed >>> 0
  return () => {
    a |= 0
    a = (a + 0x6d2b79f5) | 0
    let t = Math.imul(a ^ (a >>> 15), 1 | a)
    t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296
  }
}

test('WHAT[prefix-stability-001] H13_01_canonical_multi_tool_sequence_is_an_append_only_prefix', async () => {
  const session = 'h13-01'
  const round1Real = [
    toolCall('c1', 'bash', 't1'),
    toolCall('c2', 'read', 't2'),
    toolResult('r1', 'bash', 't1'),
    toolResult('r2', 'read', 't2'),
  ]

  // round 1: Req1 Req2 Resp1 Resp2 — universal cursor mode carries guidance
  // only as a NUL+BOM suffix on the terminal real tool result (Resp2).
  const round1Wire = await inject(undefined, session, round1Real)
  assert.deepEqual(toolNames(round1Wire), ['bash', 'read', 'bash', 'read'])
  assert.equal(pairMessages(round1Wire).length, 0)
  assert.equal(round1Wire[3].parts[0].state.status, 'completed')
  assert.notEqual(round1Wire[3].parts[0].state.status, 'pending')
  assert.equal(round1Wire[3].parts[0].state.output, `ok\0\uFEFF${text}`)
  assert.equal(round1Wire[2].parts[0].state.output, 'ok')

  // round 2 input carries the previous wire's suffixed bytes (Host persists
  // them): Req1 Req2 Resp1 Resp2+suffix Req3 Resp3+suffix
  const round2Real = [
    ...round1Wire,
    toolCall('c3', 'write', 't3'),
    toolResult('r3', 'write', 't3'),
  ]
  const round2Wire = await inject(undefined, session, round2Real)
  assert.deepEqual(toolNames(round2Wire), [
    'bash', 'read', 'bash', 'read',
    'write', 'write',
  ])
  assert.equal(pairMessages(round2Wire).length, 0)
  assert.equal(round2Wire[3].parts[0].state.output, `ok\0\uFEFF${text}`, 'historical guidance bytes never change')
  assert.equal(round2Wire[5].parts[0].state.output, `ok\0\uFEFF${text}`)

  assertPrefixLaw(round1Wire, round2Wire, 'H13-01 canonical sequence')
})
test('WHAT[prefix-stability-001] H13_08_n_round_property_prefix_law_holds', async () => {  const rand = mulberry32(0x1357)
  const session = 'h13-08'
  const rounds = 8

  // history grows exactly as the Host transcript does: previous wire (result
  // guidance bytes included) + the new real content of this round.
  let history = []
  let previousWire
  let previousPairCount = 0

  for (let n = 1; n <= rounds; n++) {
    const fresh = []
    if (rand() < 0.25) {
      // no-tool turn: assistant text only
      fresh.push(assistantText(`a${n}`))
    } else {
      const toolCount = 1 + Math.floor(rand() * 5)
      for (let i = 0; i < toolCount; i++) fresh.push(toolCall(`c${n}_${i}`, 'bash', `t${n}_${i}`))
      for (let i = 0; i < toolCount; i++) fresh.push(toolResult(`r${n}_${i}`, 'bash', `t${n}_${i}`))
    }
    if (rand() < 0.35) fresh.push(userMsg(`u${n}`))

    const wire = await inject(undefined, session, [...history, ...fresh])

    // Universal cursor mode emits zero synthetic rows; guidance replays strip
    // the suffix for placement and re-apply it exactly once — no row ever
    // accumulates a doubled NUL+BOM suffix.
    const pairCount = pairMessages(wire).length
    assert.ok(pairCount >= previousPairCount, `round ${n}: pair count must never shrink`)
    assert.ok(pairCount <= previousPairCount + 1, `round ${n}: at most one new pair per round`)
    for (const suffixes of guidanceSuffixCount(wire)) {
      assert.ok(suffixes <= 1, `round ${n}: at most one guidance suffix per result`)
    }

    if (previousWire !== undefined) {
      assertPrefixLaw(previousWire, wire, `H13-08 round ${n}`)
    }
    history = wire
    previousWire = wire
    previousPairCount = pairCount
  }
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { default: fc } = await import("fast-check");
const prefix = await import("../../../dist/Context/Prefix/Surface.js");
const providerProjection = await import("../../../dist/Participant/Provider/Projection/Surface.js");

const wire = (
  messages,
  {
    tools = ['read', 'write'],
    system = ['sys'],
    providerId = 'openai',
    modelId = 'gpt-4o',
    variant = 'deep',
  } = {},
) => ({
  modelId,
  messages,
  providerId,
  system,
  tools,
  variant,
})
const msg = (id, role, text) => ({
  parts: [{ kind: 'text', text }],
  role,
  id,
})
const textPart = fc.record({ kind: fc.constant('text'), text: fc.string({ maxLength: 48 }) })
const reasoningPart = fc.record({ kind: fc.constant('reasoning'), text: fc.string({ maxLength: 48 }) })
const toolCallPart = fc.record({
  kind: fc.constant('tool-call'),
  callId: fc.string({ maxLength: 24 }),
  name: fc.string({ maxLength: 24 }),
  args: fc.string({ maxLength: 48 }),
})
const toolResultPart = fc.record({
  kind: fc.constant('tool-result'),
  callId: fc.string({ maxLength: 24 }),
  result: fc.string({ maxLength: 48 }),
})
const mediaPart = fc.record({
  kind: fc.constant('media'),
  mediaType: fc.option(fc.string({ maxLength: 24 }), { nil: null }),
  contentDigest: fc.string({ maxLength: 48 }),
})
const wirePart = fc.oneof(textPart, reasoningPart, toolCallPart, toolResultPart, mediaPart)
const message = fc.record({
  role: fc.constantFrom('user', 'assistant'),
  parts: fc.array(wirePart, { minLength: 1, maxLength: 6 }),
})
const mutationTarget = fc
  .tuple(
    fc.constantFrom('user', 'assistant'),
    textPart,
    reasoningPart,
    toolCallPart,
    toolResultPart,
    mediaPart,
  )
  .map(([role, ...parts]) => ({ role, parts }))
const metadata = fc.record({
  tools: fc.array(fc.string({ maxLength: 16 }), { maxLength: 8 }),
  system: fc.array(fc.string({ maxLength: 32 }), { maxLength: 4 }),
  providerId: fc.string({ minLength: 1, maxLength: 16 }),
  modelId: fc.string({ minLength: 1, maxLength: 16 }),
  variant: fc.string({ maxLength: 16 }),
})
const W1 = wire([msg('m1', 'user', 'first')])
const W2 = wire([msg('m1', 'user', 'first'), msg('m2', 'assistant', 'second')])
const W3 = wire([
  msg('m1', 'user', 'first'),
  msg('m2', 'assistant', 'second'),
  msg('m3', 'user', 'third'),
])
const changed = (value) => `${value}\u0000changed`
const replacePart = (messageValue, index, replacement) => ({
  ...messageValue,
  parts: messageValue.parts.map((part, partIndex) => (partIndex === index ? replacement : part)),
})
const historicalMutations = (target) => {
  const mutations = [{ name: 'role', message: { ...target, role: changed(target.role) } }]

  target.parts.forEach((part, index) => {
    const add = (name, replacement) => mutations.push({ name, message: replacePart(target, index, replacement) })
    if (part.kind === 'text' || part.kind === 'reasoning') {
      add(`${part.kind}.text`, { ...part, text: changed(part.text) })
    } else if (part.kind === 'tool-call') {
      add('tool-call.callId', { ...part, callId: changed(part.callId) })
      add('tool-call.name', { ...part, name: changed(part.name) })
      add('tool-call.args', { ...part, args: changed(part.args) })
    } else if (part.kind === 'tool-result') {
      add('tool-result.callId', { ...part, callId: changed(part.callId) })
      add('tool-result.result', { ...part, result: changed(part.result) })
    } else {
      add('media.mediaType', {
        ...part,
        mediaType: part.mediaType === null ? 'changed' : changed(part.mediaType),
      })
      add('media.contentDigest', { ...part, contentDigest: changed(part.contentDigest) })
    }
  })

  return mutations
}

test('WHAT[prefix-stability-001] PREFIX_STABILITY_append_only_law_holds_within_one_epoch', () => {
  assert.equal(providerProjection.isAppendOnlyPrefix(W1, W2), true, 'W1 ⊏ W2')
  assert.equal(providerProjection.isAppendOnlyPrefix(W1, W3), true, 'W1 ⊏ W3')
  assert.equal(providerProjection.isAppendOnlyPrefix(W2, W3), true, 'W2 ⊏ W3')
  assert.equal(providerProjection.isAppendOnlyPrefix(W1, W1), true, 'same wire is a prefix of itself')

  fc.assert(
    fc.property(
      metadata,
      fc.array(message, { maxLength: 24 }),
      fc.array(message, { minLength: 1, maxLength: 24 }),
      (identity, base, extension) => {
        assert.equal(
          providerProjection.isAppendOnlyPrefix(wire(base, identity), wire([...base, ...extension], identity)),
          true,
        )
      },
    ),
    { seed: 0x50524658, numRuns: 1_000 },
  )
})
test('WHAT[prefix-stability-001] PREFIX_STABILITY_modified_historical_bytes_break_the_law', () => {
  const changedWire = wire([msg('m1', 'user', 'FIRST CHANGED')])
  assert.equal(providerProjection.isAppendOnlyPrefix(changedWire, W2), false, 'a changed first message is not a prefix')
  assert.equal(providerProjection.isAppendOnlyPrefix(W1, changedWire), false, 'nor is the old first message a prefix of the changed one')

  fc.assert(
    fc.property(
      metadata,
      mutationTarget,
      fc.array(message, { maxLength: 23 }),
      fc.array(message, { minLength: 1, maxLength: 4 }),
      (identity, target, historyTail, extension) => {
        const previous = [target, ...historyTail]
        assert.equal(
          providerProjection.isAppendOnlyPrefix(wire(previous, identity), wire([...previous, ...extension], identity)),
          true,
        )

        for (const mutation of historicalMutations(target)) {
          assert.equal(
            providerProjection.isAppendOnlyPrefix(
              wire(previous, identity),
              wire([mutation.message, ...historyTail, ...extension], identity),
            ),
            false,
            `historical ${mutation.name} bytes must invalidate the prefix`,
          )
        }
      },
    ),
    { seed: 0x50524659, numRuns: 1_000 },
  )
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const XWireSurface = await import("../../../dist/Context/Prefix/XWireSurface.js");


test('WHAT[prefix-stability-001] compiled XWireSurface exposes the production horizon decision', () => {
  assert.equal(XWireSurface.presentationHorizon(false), 'Current')
  assert.equal(XWireSurface.presentationHorizon(true), 'TentativeCold')
})
test('WHAT[prefix-stability-001] compiled XWireSurface reconciles completion and failure', () => {
  assert.deepEqual(
    XWireSurface.reconcile({
      hasPlan: true,
      outcome: 'completed',
      hasProbe: true,
      currentEpoch: 4,
      probeEpoch: 4,
    }),
    { promoted: true, cleared: true, keptPlan: false },
  )

  assert.deepEqual(
    XWireSurface.reconcile({ hasPlan: true, outcome: 'failed', hasProbe: true }),
    { promoted: false, cleared: true, keptPlan: false },
  )
})
}
