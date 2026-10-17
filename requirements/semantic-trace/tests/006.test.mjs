import test from 'node:test'

for (const layout of ['single tool', 'late result after text', 'late result after another message']) {
  test(`WHAT[semantic-trace-006] Blogger coverage consumes only the durable prefix it received (${layout})`, async () => {
    const assert = (await import('node:assert/strict')).default
    const trace = await import('../../../dist/Context/Trace/SemanticTraceSurface.js')
    const { withReopenableJournal, commitFrame, record } = await import('../../work-record/tests/support/record.mjs')
    await withReopenableJournal(async (handle, reopen) => {
      const session = `facet-prefix-${layout}`
      const withText = layout === 'late result after text'
      const withOther = layout === 'late result after another message'
      const call = 'CALL-' + 'C'.repeat(220000)
      const result = 'RESULT-MUST-STILL-BE-OFFERED'
      const observations = state => [
        { hostMessageId: 'tool-message', message: { info: { id: 'tool-message', role: 'assistant' }, parts: [
          { id: 'tool-part', type: 'tool', tool: 'read', callID: 'call', state },
          ...(withText ? [{ id: 'text-part', type: 'text', text: 'TEXT-BEFORE-LATE-RESULT' }] : []),
        ] } },
        ...(withOther ? [{ hostMessageId: 'later-message', message: { info: { role: 'user' }, parts: [{ id: 'later-part', type: 'text', text: 'OTHER-MESSAGE' }] } }] : []),
      ]
      assert.equal((await trace.captureObservedMessages(handle, session, observations({ status: 'running', input: call }))).ok, true)
      assert.equal((await trace.captureObservedMessages(handle, session, observations({ status: 'completed', output: result }))).ok, true)
      const canonical = await trace.currentProjection(handle, session)
      const kinds = canonical.messages.flatMap(message => message.parts.map(part => part.kind))
      assert.deepEqual(kinds, ['tool-call', ...(withText || withOther ? ['text'] : []), 'tool-result'])
      const first = await record.nextBloggerRequest(handle, session)
      assert.equal(first.nextSequence, 1n)
      assert.equal(first.toml.includes(result), false)
      assert.equal(first.toml.includes('TEXT-BEFORE-LATE-RESULT'), false)
      assert.equal(first.toml.includes('OTHER-MESSAGE'), false)
      await commitFrame(handle, session, { from: 0, through: first.nextSequence, body: 'Only the offered tool call', id: layout })
      const expectedCursor = withOther ? { turn: 1, partIndex: 0 } : { turn: 0, partIndex: 1 }
      assert.deepEqual(trace.semanticCursorAfter(trace.cursor(1), trace.snapshot(handle, session)), expectedCursor)
      const next = await record.nextBloggerRequest(handle, session)
      assert.equal(next.previousSequence, 1n)
      assert.equal(next.nextSequence, withText || withOther ? 3n : 2n)
      assert.equal(next.toml.includes(result), true)
      assert.equal(next.toml.includes('CALL-'), false)
      if (withText) assert.equal(next.toml.includes('TEXT-BEFORE-LATE-RESULT'), true)
      if (withOther) assert.equal(next.toml.includes('OTHER-MESSAGE'), true)
      const cold = await reopen()
      assert.deepEqual(await record.nextBloggerRequest(cold, session), next)
      assert.deepEqual(await trace.currentProjection(cold, session), canonical)
    })
  })
}

test('WHAT[semantic-trace-006] actual Blogger request evidence preserves a durable cursor beyond Int32', async () => {
  const assert = (await import('node:assert/strict')).default
  const journal = await import('../../../dist/Persistence/Journal/Surface.js')
  const record = await import('../../../dist/Mission/WorkRecord/Surface.js')
  const { withReopenableJournal } = await import('./support/journal.mjs')
  await withReopenableJournal(async (handle, reopen) => {
    const session = 'large-cursor-proof'
    const sequence = 2147483653n
    const body = await journal.JournalSurface_writePayload(handle, 'actual confirmed material')
    assert.equal(body.ok, true)
    const appended = await journal.JournalSurface_appendAgent(handle, { kind: 'Session', session }, 'actual-run', {
      family: 'Companion', case: 'XTracePartAppended', payload: {
        SessionId: session, CursorSequence: sequence, Role: 'assistant', Turn: 0, PartIndex: 0, Kind: 'text',
        ToolName: null, ProviderRun: 'actual-run', ToolCallId: null, HostToolPartId: null,
        TextRef: body.blobRef, TextDigest: body.blobDigest, Provenance: 'g:0/msg:actual-message/part:0',
      },
    })
    assert.equal(appended.ok, true, JSON.stringify(appended.error))
    const offered = await record.nextBloggerRequest(handle, session)
    assert.ok(offered)
    assert.equal(offered.nextSequence, sequence)
    assert.equal(offered.previousSequence, 0n)
    assert.equal(offered.frameEpoch, 0n)
    assert.equal(offered.observedPrefixEpoch, 0n)
    assert.deepEqual(await record.nextBloggerRequest(await reopen(), session), offered)
  })
})

test('WHAT[semantic-trace-006] a physical prefix cannot cut across repeated canonical segments of one Host message', async () => {
  const assert = (await import('node:assert/strict')).default
  const trace = await import('../../../dist/Context/Trace/SemanticTraceSurface.js')
  const { withReopenableJournal } = await import('./support/journal.mjs')
  await withReopenableJournal(async (handle, reopen) => {
    const session = 'late-facet-prefix-boundary'
    const observed = state => [
      { hostMessageId: 'opening', message: { info: { role: 'user' }, parts: [{ id: 'u', type: 'text', text: 'task' }] } },
      { hostMessageId: 'a', message: { info: { id: 'a', role: 'assistant' }, parts: [{ id: 'tool', type: 'tool', tool: 'read', callID: 'call', state }] } },
      { hostMessageId: 'b', message: { info: { role: 'user' }, parts: [{ id: 'b-part', type: 'text', text: 'later' }] } },
    ]
    assert.equal((await trace.captureObservedMessages(handle, session, observed({ status: 'running', input: 'old call' }))).ok, true)
    const first = trace.snapshot(handle, session)
    assert.deepEqual(trace.hostMessageIdsBeforeTurn(2, first), ['opening', 'a'])
    assert.equal((await trace.captureObservedMessages(handle, session, observed({ status: 'completed', output: 'late result' }))).ok, true)
    const current = trace.snapshot(handle, session)
    assert.deepEqual(trace.hostMessageIdsBeforeTurn(2, current), [])
    assert.deepEqual(trace.hostMessageIdsBeforeTurn(3, current), [])
    assert.deepEqual(trace.hostMessageIdsBeforeTurn(4, current), ['opening', 'a', 'b'])
    const cold = trace.snapshot(await reopen(), session)
    assert.deepEqual(trace.hostMessageIdsBeforeTurn(2, cold), [])
    assert.deepEqual(trace.hostMessageIdsBeforeTurn(4, cold), ['opening', 'a', 'b'])
  })
})

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const trace = await import("../../../dist/Context/Trace/SemanticTraceSurface.js");

const unwrap = (result) => {
  assert.equal(result.ok, true, result.ok ? '' : result.error)
  return result.projection
}
const append = (projection, sequence, options = {}) => unwrap(trace.appendPart(projection, {
  sequence,
  role: 'assistant',
  provenance: `g:0/msg:${options.messageId ?? 'message-a'}/host-part:${options.hostToolPartId ?? `part-${sequence}`}`,
  turn: options.turn ?? 0,
  partIndex: options.partIndex ?? sequence - 1,
  kind: options.kind ?? 'tool_call',
  toolName: options.toolName ?? 'todowrite',
  textRef: `blob-${sequence}`,
  textDigest: `digest-${sequence}`,
  body: `raw-${sequence}`,
  providerRun: options.providerRun ?? 'provider-run-a',
  toolCallId: options.toolCallId ?? 'call-a',
  hostToolPartId: options.hostToolPartId ?? `part-${sequence}`,
}))
const fixture = () => {
  let projection = trace.emptyProjection()
  projection = append(projection, 1, { messageId: 'message-a', hostToolPartId: 'part-a', partIndex: 0 })
  projection = append(projection, 2, { messageId: 'message-a', hostToolPartId: 'part-b', partIndex: 1, kind: 'tool_result' })
  projection = append(projection, 3, { messageId: 'message-b', providerRun: 'provider-run-b', toolCallId: 'call-b', hostToolPartId: 'part-c', turn: 1, partIndex: 0 })
  return projection
}

test('WHAT[semantic-trace-006] Host message set resolves only to its exact contiguous range', () => {
  const projection = fixture()
  assert.deepEqual(trace.tryContiguousHostRange(['message-a'], projection), {
    start: { sequence: 1 },
    endExclusive: { sequence: 3 },
  })
  assert.equal(trace.tryContiguousHostRange(['message-a', 'missing'], projection), undefined)
  assert.equal(trace.tryContiguousHostRange([], projection), undefined)
  assert.deepEqual(trace.tryContiguousHostRange(['message-b', 'message-a'], projection), {
    start: { sequence: 1 }, endExclusive: { sequence: 4 },
  })
})
test('WHAT[semantic-trace-006] a cursor gap or intervening unrequested identity cannot be a contiguous range', () => {
  const gap = append(append(trace.emptyProjection(), 1), 3)
  assert.equal(trace.tryContiguousHostRange(['message-a'], gap), undefined)
  let interleaved = append(trace.emptyProjection(), 1, { messageId: 'message-a' })
  interleaved = append(interleaved, 2, { messageId: 'other-message' })
  interleaved = append(interleaved, 3, { messageId: 'message-a' })
  assert.equal(trace.tryContiguousHostRange(['message-a'], interleaved), undefined)
  assert.deepEqual(trace.tryContiguousHostRange(['message-a', 'other-message'], interleaved), {
    start: { sequence: 1 }, endExclusive: { sequence: 4 },
  })
})
test('WHAT[semantic-trace-006] range and frontier queries preserve half-open boundaries', () => {
  const projection = fixture()
  const range = trace.tryContiguousHostRange(['message-a'], projection)
  assert.deepEqual(trace.slice(range, projection).map((part) => part.cursor.sequence), [1, 2])
  assert.deepEqual(trace.rangeOfPart(trace.orderedSemanticParts(projection)[1]), {
    start: { sequence: 2 },
    endExclusive: { sequence: 3 },
  })
  assert.deepEqual(trace.semanticCursorAfter(trace.cursor(2), projection), { turn: 1, partIndex: 0 })
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const trace = await import("../../../dist/Context/Trace/SemanticTraceSurface.js");


test('WHAT[semantic-trace-006] range vocabulary is half-open', () => {
  const range = trace.createRange(trace.cursor(1), trace.cursor(3))
  assert.equal(trace.rangeContains(trace.cursor(0), range), false)
  assert.equal(trace.rangeContains(trace.cursor(1), range), true)
  assert.equal(trace.rangeContains(trace.cursor(2), range), true)
  assert.equal(trace.rangeContains(trace.cursor(3), range), false)
  assert.equal(trace.rangeIsEmpty(trace.createRange(trace.cursor(2), trace.cursor(2))), true)
})
}
