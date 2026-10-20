import test from 'node:test'

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

test('WHAT[prefix-stability-014] PREFIX_STABILITY_pair_body_stays_out_of_the_trace_projections', () => {
  const session = 'h13-014'
  const markerBody = 'SECRET synthetic marker body'
  const result = pair.foldAnchoredPair({
    session,
    ordinal: 1,
    callId: stableCallId(session, 1n),
    markerText: markerBody,
    callGapAfter: 'msg_7',
    resultGapAfter: 'msg_7',
  })
  assert.equal(result.ok, true, result.ok ? '' : JSON.stringify(result))

  const flags = result.flags
  // The only projection the pair fact may grow is Guidelines (HOST-013 recovery).
  assert.equal(flags.guidelines, true, 'the anchored fact must land in Guidelines')

  // This fact fold does not create trace state. Message ingestion is
  // a separate path and is not proved by these flags.
  assert.equal(flags.xTrace, false, 'XTrace must not be created by the pair fact')
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const pair = await import("../../../dist/OpenCode/Host/PairProgrammingThoughtSurface.js");

const {
  tryInject,
  isPairProgrammingThought,
  skipAutoInjectedRequested,
  source,
  text,
  stableCallId,
} = pair
const inject = async (session, raw, markerText = text) => {
  const result = await tryInject(session, markerText, raw)
  assert.equal(result.ok, true, `HOST-013 transform must commit the pair: ${result.error ?? ''}`)
  return result.value
}
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
const guidanceSuffix = (markerText) => `\0\uFEFF${markerText}`
const terminalOutputOf = (messages, id) => messages.find((m) => m.info.id === id).parts[0].state.output

test('WHAT[prefix-stability-014] PPT_source_is_the_frozen_side_channel_identity', () => {
  assert.equal(source, 'pair-programming-auto-injected')
  assert.ok(text.length > 0, 'frozen thought text must be non-empty')
  assert.equal(isPairProgrammingThought(null), false)
  assert.equal(isPairProgrammingThought({}), false)
  assert.equal(isPairProgrammingThought({ info: { source: 'other' } }), false)
  assert.equal(isPairProgrammingThought({ info: { source } }), true)
  assert.equal(isPairProgrammingThought({ parts: [] }), false, 'no info.source means not a marker')
})
test('WHAT[prefix-stability-014] PPT_tryInject_user_quoting_the_thought_text_is_not_a_marker', async () => {
  const raw = [userMsg('u1'), assistantText('a1'), userMsg('msg_1', text)]
  const out = await inject('ses_quote', raw)
  assert.equal(isPairProgrammingThought(out[2]), false, 'matching text alone must not classify as marker')
  assert.equal(out.length, 3)
  assert.equal(out[2].info.role, 'user')
})
}

{
const assert = (await import('node:assert/strict')).default
const pair = await import('../../../dist/OpenCode/Host/PairProgrammingThoughtSurface.js')
const trace = await import('../../../dist/Context/Trace/SemanticTraceSurface.js')
const { withReopenableJournal } = await import('../../semantic-trace/tests/support/journal.mjs')

test('WHAT[prefix-stability-014] bare semantic capture preserves supplied bytes rather than guessing guidance from prose', async () => {
  const guidance = 'a unique guidance sentence'
  const raw = [{
    info: { id: 'tool-message', role: 'assistant' },
    parts: [{ id: 'tool-part', type: 'tool', tool: 'read', callID: 'call-1',
      state: { status: 'completed', input: {}, output: 'file content\r\n', time: { start: 0, end: 1 } } }],
  }]
  const injected = await pair.tryInject('ses-trace-guidance', guidance, raw)
  assert.equal(injected.ok, true)
  assert.equal(injected.value[0].parts[0].state.output, `file content\r\n\0\uFEFF${guidance}`)
  const quoted = {
    info: { id: 'user-quote', role: 'user' },
    parts: [{ id: 'quote-part', type: 'text', text: guidance }],
  }
  await withReopenableJournal(async (journal, reopen) => {
    const captured = await trace.captureObservedMessages(journal, 'ses-trace-guidance', [...injected.value, quoted].map(message => ({
      hostMessageId: message.info.id, message,
    })))
    assert.equal(captured.ok, true)
    const recovered = await trace.currentProjection(await reopen(), 'ses-trace-guidance')
    const parts = recovered.messages.flatMap(message => message.parts)
    assert.deepEqual(parts.filter(part => part.kind === 'tool-result'), [{ kind: 'tool-result', result: `file content\r\n\0\uFEFF${guidance}` }])
    assert.deepEqual(parts.filter(part => part.kind === 'text'), [{ kind: 'text', text: guidance }])
  })
})
}

{
const assert = (await import('node:assert/strict')).default
const pair = await import('../../../dist/OpenCode/Host/PairProgrammingThoughtSurface.js')
const trace = await import('../../../dist/Context/Trace/SemanticTraceSurface.js')
const { join } = await import('node:path')
const journal = await import('../../../dist/Persistence/Journal/Surface.js')
const { withGuidanceHost, toolBody, separator, withoutAnchorMarker } = await import('./support/guidance-host.mjs')

test('WHAT[prefix-stability-014] multiple terminal parts keep their physical originals through guidance replay', async () => {
  const secondBody = 'literal' + separator + pair.text
  await withGuidanceHost('completed', async host => {
    const physical = JSON.stringify(host.raw)
    const projected = host.first
    const result = projected.find(message => message.info.id === host.result.info.id)
    assert.match(result.parts[0].state.output, /^\[ANCHOR: \d+\]\0\uFEFF/)
    assert.equal(host.textOf(result), toolBody + separator + host.guidance)
    assert.equal(result.parts[1].state.error, secondBody + separator + host.guidance)
    assert.equal(JSON.stringify(host.raw), physical)
    const captured = await host.current()
    assert.deepEqual(captured.messages.flatMap(message => message.parts)
      .filter(part => part.kind === 'tool-result').map(part => part.result), [toolBody, secondBody])
    assert.deepEqual(await host.transform(host.raw), projected)
    assert.deepEqual(await host.current(), captured)
    assert.deepEqual(host.cold().projection, captured)
  }, 'multi-terminal-parts', { role: 'engineer', openWorkBeforeTool: true, additionalToolParts: [{
    id: 'part-second', type: 'tool', tool: 'read', callID: 'call-second',
    state: { status: 'error', input: {}, error: secondBody, time: { start: 2, end: 3 } },
  }] })
})

test('WHAT[prefix-stability-014] a failed guidance assertion ends its exact mock provider step before another single-slot fixture starts', async () => {
  const previousDecision = globalThis.__wanxiangshu_test_routing_decision
  globalThis.__wanxiangshu_test_routing_decision = (role, running) => running.length === 0
    ? { model: `provider/${role}-model`, reasoning: 'none' }
    : null
  try {
    const failure = new Error('controlled guidance assertion failure')
    await assert.rejects(withGuidanceHost('completed', async () => { throw failure }, 'failed-body'), error => error === failure)
    await withGuidanceHost('completed', async host => {
      assert.deepEqual(host.cold().projection, await host.current())
    }, 'after-failed-body')
  } finally {
    if (previousDecision === undefined) delete globalThis.__wanxiangshu_test_routing_decision
    else globalThis.__wanxiangshu_test_routing_decision = previousDecision
  }
})

for (const status of ['completed', 'error']) {
  test(`WHAT[prefix-stability-014] registered Host ${status} tool presentation leaves physical snapshot and durable originals unchanged`, async () => {
    await withGuidanceHost(status, async host => {
      const before = await host.current()
      const frontier = host.head()
      assert.equal(host.textOf(host.result), toolBody)
      assert.deepEqual(before.messages.flatMap(message => message.parts).filter(part => part.kind === 'tool-result'), [{ kind: 'tool-result', result: toolBody }])
      const completed = { tool: 'read', sessionID: host.sessionID, callID: 'call-read', args: { filePath: 'x' } }
      await host.hooks['tool.execute.after'](completed, { title: 'read', output: toolBody, metadata: {} })
      await host.hooks.event({ event: { type: 'message.part.updated', properties: { sessionID: host.sessionID, part: host.result.parts[0] } } })
      const snapshot = await trace.captureSessionSnapshot(host.runtime.journal, host.sessionID, host.raw)
      assert.equal(snapshot.ok, true, JSON.stringify(snapshot))
      const rawReplay = await host.transform(host.raw)
      assert.ok(rawReplay.some(message => message.info.id === host.result.info.id), 'the current tool must reach capture and rendering rather than being cut by relay')
      const presented = host.first.find(message => message.info.id === host.result.info.id)
      await assert.rejects(host.transform([host.raw[0], presented]), /Confirmed Host (?:physical )?material identity conflict/)
      assert.deepEqual(host.head(), frontier)
      assert.deepEqual(await host.current(), before)
      const cold = host.cold()
      assert.notEqual(cold.pid, process.pid)
      assert.deepEqual(cold.projection, before)
    }, `tool-${status}`, { role: 'engineer', openWorkBeforeTool: true })
  })
}

for (const form of ['plain', 'enclosed', 'embedded', 'position', 'multiple']) {
  test(`WHAT[prefix-stability-014] a real user ${form} quotation of previous guidance is preserved in the request and cold trace`, async () => {
    await withGuidanceHost('completed', async host => {
      const block = `${separator}<system>\n${host.guidance.trim()}\n</system>`
      const text = form === 'plain' ? host.guidance : form === 'enclosed' ? block : `literal${block}\nend`
      const quoted = await host.quote(text, { physicalPart: form !== 'position', multiple: form === 'multiple' })
      assert.ok(quoted.presented.startsWith(text), 'guidance rendering must retain the complete original user body')
      assert.equal(quoted.message.parts.at(-1).text, text)
      const projection = await host.current()
      assert.equal(projection.messages.at(-1).parts.at(-1).text, text)
      assert.deepEqual(host.cold().projection, projection)
    }, form)
  })
}

for (const [name, body] of [['empty', ''], ['whitespace', ' \t\r\n']]) {
  for (const multiple of [false, true]) {
    test(`WHAT[prefix-stability-014] a ${name} terminal user part with ${multiple ? 'multiple parts' : 'a single part'} cannot acquire an unprovable guidance suffix`, async () => {
      await withGuidanceHost('completed', async host => {
        const quoted = await host.quote(body, { multiple })
        const expectedParts = quoted.message.parts
        assert.deepEqual(quoted.presentedParts, expectedParts, 'the inline anchor supplies provider content without inventing a guidance occurrence')
        assert.equal(quoted.message.parts.at(-1).text, body)
        const before = await host.current()
        const frontier = host.head()
        const replay = await host.transform([...host.raw, quoted.message])
        assert.deepEqual(replay.find(message => message.info.id === quoted.message.info.id).parts.map(part =>
          typeof part.text === 'string' ? { ...part, text: withoutAnchorMarker(part.text) } : part), expectedParts)
        assert.deepEqual(host.head(), frontier)
        assert.deepEqual(await host.current(), before)
        assert.deepEqual(host.cold().projection, before)
        const opened = await pair.createJournal(join(host.runtime.directory, '.Herdchestra'))
        assert.equal(opened.ok, true, JSON.stringify(opened))
        try {
          const projected = await pair.tryInjectWithJournal(opened.journal, host.sessionID, 'must not create an occurrence', [...host.raw, quoted.message])
          assert.equal(projected.ok, true, projected.error)
          assert.deepEqual(projected.value.find(message => message.info.id === quoted.message.info.id).parts, quoted.message.parts, 'the guidance owner must preserve every original part and must not move a suffix to an earlier part')
          assert.equal(pair.pairCount(opened.journal, host.sessionID), 1, 'the blank carrier must not leave an invisible durable occurrence')
        } finally { pair.disposeJournal(opened.journal) }
      }, `blank-${name}-${multiple}`)
    })
  }
}

test('WHAT[prefix-stability-014] an old user occurrence without its original text is refused rather than inferred from its presentation', async () => {
  await withGuidanceHost('completed', async host => {
    const message = { info: { id: 'msg-legacy-empty', sessionID: host.sessionID, role: 'user' }, parts: [{ id: 'part-legacy-empty', type: 'text', text: '' }] }
    const opened = await pair.createJournal(join(host.runtime.directory, '.Herdchestra'))
    assert.equal(opened.ok, true, JSON.stringify(opened))
    try {
      const anchored = await pair.appendAnchoredPair(opened.journal, {
        session: host.sessionID, ordinal: 2, callId: pair.stableCallId(host.sessionID, 2n),
        markerText: host.guidance, callGapAfter: message.info.id, resultGapAfter: message.info.id,
      })
      assert.equal(anchored.ok, true, JSON.stringify(anchored))
      const carrier = { ...message, parts: [{ ...message.parts[0], text: `${separator}<system>\n${host.guidance.trim()}\n</system>` }] }
      const original = structuredClone(carrier)
      const before = host.cold().projection
      const replay = await pair.tryInjectWithJournal(opened.journal, host.sessionID, 'unused marker', [...host.raw, carrier])
      assert.equal(replay.ok, false, 'durable placement cannot stand in for a missing original')
      assert.match(replay.error, /original Host user text.*missing/i)
      assert.deepEqual(carrier, original)
      assert.deepEqual(host.cold().projection, before)
      assert.equal(pair.pairCount(opened.journal, host.sessionID), 2)
    } finally { pair.disposeJournal(opened.journal) }
  }, 'old-blank-occurrence')
})

for (const physicalPart of [true, false]) {
  test(`WHAT[prefix-stability-014] a same-generation ${physicalPart ? 'physical' : 'position'} user edit cannot disguise an original conflict as guidance replay`, async () => {
    await withGuidanceHost('completed', async host => {
      const quoted = await host.quote('original content', { physicalPart })
      const before = await host.current()
      const frontier = host.head()
      quoted.message.parts[0] = { ...quoted.message.parts[0], text: quoted.presented }
      const edited = structuredClone(quoted.message.parts)
      await assert.rejects(host.transform([...host.raw, quoted.message]), /Confirmed Host (?:physical )?material identity conflict/)
      assert.deepEqual(quoted.message.parts, edited, 'capture must not rewrite the physical edit before checking its identity')
      assert.deepEqual(host.head(), frontier)
      assert.deepEqual(await host.current(), before)
      assert.deepEqual(host.cold().projection, before)
    }, `same-generation-edit-${physicalPart}`)
  })
}

for (const physicalPart of [true, false]) {
  test(`WHAT[prefix-stability-014] the first reanchored capture of edited ${physicalPart ? 'physical' : 'position'} user text does not borrow retired original evidence`, async () => {
    await withGuidanceHost('completed', async host => {
      const quoted = await host.quote('original content', { physicalPart })
      const body = quoted.presented
      quoted.message.parts[0] = { ...quoted.message.parts[0], text: body }
      const originalParts = structuredClone(quoted.message.parts)
      const prior = trace.partsForHostMessageIds([quoted.message.info.id], trace.snapshot(host.runtime.journal, host.sessionID))
      assert.deepEqual(prior.map(part => part.generation), [0])
      const reanchored = await journal.JournalSurface_appendAgent(host.runtime.journal, { kind: 'Session', session: host.sessionID }, null, {
        family: 'Context', case: 'ContextReanchored', payload: {
          SessionId: host.sessionID, PreviousEpochId: '0', NextEpochId: '1', ObservedCompactionRun: 'guidance-first-capture',
        },
      })
      assert.equal(reanchored.ok, true, JSON.stringify(reanchored))
      const rendered = await host.transform([...host.raw, quoted.message])
      const carrier = rendered.find(message => message.info.id === quoted.message.info.id)
      assert.ok(withoutAnchorMarker(carrier.parts[0].text).startsWith(body), 'retired bytes now quoted by a physical user remain business material')
      assert.deepEqual(quoted.message.parts, originalParts)
      const current = await host.current()
      assert.equal(current.messages.at(-1).parts.at(-1).text, body, 'the first new-generation capture must retain the complete edited body')
      const captured = trace.partsForHostMessageIds([quoted.message.info.id], trace.snapshot(host.runtime.journal, host.sessionID))
      assert.deepEqual(captured.map(part => part.generation), [0, 1])
      assert.deepEqual(captured.filter(part => part.generation === 0), prior)
      const repeated = await host.transform([...host.raw, quoted.message])
      assert.equal(repeated.find(message => message.info.id === quoted.message.info.id).parts[0].text, carrier.parts[0].text)
      assert.deepEqual(await host.current(), current)
      assert.deepEqual(host.cold().projection, current)
    }, `first-generation-${physicalPart}`)
  })
}

for (const physicalPart of [true, false]) {
  for (const changed of [false, true]) {
    test(`WHAT[prefix-stability-014] reanchored ${physicalPart ? 'physical' : 'position'} user proof uses the current capture generation with ${changed ? 'changed' : 'same'} text`, async () => {
      await withGuidanceHost('completed', async host => {
        const quoted = await host.quote(`old literal ${separator} is not a presentation`, { physicalPart })
        const body = changed ? `new literal ${separator} is not a presentation` : quoted.message.parts[0].text
        const message = { ...quoted.message, parts: [{ ...quoted.message.parts[0], text: body }] }
        const reanchored = await journal.JournalSurface_appendAgent(host.runtime.journal, { kind: 'Session', session: host.sessionID }, null, {
          family: 'Context', case: 'ContextReanchored', payload: {
            SessionId: host.sessionID, PreviousEpochId: '0', NextEpochId: '1', ObservedCompactionRun: 'guidance-reanchor',
          },
        })
        assert.equal(reanchored.ok, true, JSON.stringify(reanchored))
        const captured = await trace.captureSessionSnapshot(host.runtime.journal, host.sessionID, [...host.raw, message])
        assert.equal(captured.ok, true, JSON.stringify(captured))
        const parts = trace.partsForHostMessageIds([message.info.id], trace.snapshot(host.runtime.journal, host.sessionID))
        assert.deepEqual(parts.map(part => part.generation), [0, 1])
        const before = host.cold().projection
        assert.equal(before.messages.at(-1).parts.at(-1).text, body)
        const opened = await pair.createJournal(join(host.runtime.directory, '.Herdchestra'))
        assert.equal(opened.ok, true, JSON.stringify(opened))
        let presented
        try {
          const replay = await pair.tryInjectWithJournal(opened.journal, host.sessionID, 'new horizon guidance', [...host.raw, message])
          assert.equal(replay.ok, true, replay.error)
          presented = replay.value.find(item => item.info.id === message.info.id)
          assert.equal(presented.parts[0].text, `${body}${separator}<system>\nnew horizon guidance\n</system>`)
        } finally { pair.disposeJournal(opened.journal) }
        const reopened = await pair.createJournal(join(host.runtime.directory, '.Herdchestra'))
        assert.equal(reopened.ok, true, JSON.stringify(reopened))
        try {
          const replay = await pair.tryInjectWithJournal(reopened.journal, host.sessionID, 'must not replace frozen bytes', [...host.raw, presented])
          assert.equal(replay.ok, true, replay.error)
          assert.equal(replay.value.find(item => item.info.id === message.info.id).parts[0].text, presented.parts[0].text)
          assert.equal(pair.pairCount(reopened.journal, host.sessionID), 3)
        } finally { pair.disposeJournal(reopened.journal) }
        assert.deepEqual(host.cold().projection, before)
      }, `generation-${physicalPart}-${changed}`)
    })
  }
}

test('WHAT[prefix-stability-014] a captured user carrier presentation replays from its proven original without adding trace material', async () => {
  await withGuidanceHost('completed', async host => {
    const quoted = await host.quote('continue the work')
    const before = await host.current()
    const frontier = host.head()
    const reopened = await pair.createJournal(join(host.runtime.directory, '.Herdchestra'))
    assert.equal(reopened.ok, true, JSON.stringify(reopened))
    try {
      const carrier = { ...quoted.message, parts: [{ ...quoted.message.parts[0], text: quoted.presented }] }
      const replayed = await pair.tryInjectWithJournal(reopened.journal, host.sessionID, 'unused marker at the same placement', [...host.raw, carrier])
      assert.equal(replayed.ok, true, replayed.error)
      assert.equal(replayed.value.find(message => message.info.id === quoted.message.info.id).parts.at(-1).text, quoted.presented)
      await assert.rejects(host.transform([...host.raw, carrier]), /Confirmed Host (?:physical )?material identity conflict/)
    } finally {
      pair.disposeJournal(reopened.journal)
    }
    assert.deepEqual(host.head(), frontier)
    assert.deepEqual(await host.current(), before)
    assert.deepEqual(host.cold().projection, before)
  }, 'user-reentry')
})
}

{
const assert = (await import('node:assert/strict')).default
const { execFileSync } = await import('node:child_process')
const { fileURLToPath } = await import('node:url')
const { integrationTest } = await import('../../verification-system/tests/support/tier-gate.mjs')
const { withFinitePhysicalWait } = await import('../../verification-system/tests/support/finite-physical-wait.mjs')

integrationTest('WHAT[prefix-stability-014] actual OpenCode Host provider requests preserve user quotations and keep guidance out of physical tool originals through cold recovery', async t => {
  const output = await withFinitePhysicalWait(t, { operation: 'OpenCode guidance and cold recovery', limitMs: 30000 }, () =>
    execFileSync(process.execPath, [fileURLToPath(new URL('./support/real-guidance-host.mjs', import.meta.url))], { encoding: 'utf8' }))
  const evidence = JSON.parse(output.trim().split('\n').at(-1))
  assert.equal(evidence.quotePreserved, true)
  assert.equal(evidence.persistedUnchanged, true)
  assert.equal(evidence.coldVerified, true)
  assert.equal(evidence.deliveries, 2)
  assert.ok(evidence.providerRequests >= 4)
})
}
