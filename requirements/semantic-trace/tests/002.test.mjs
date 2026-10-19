import test from 'node:test'

for (const address of ['physical', 'call', 'position']) {
  for (const opaque of [false, true]) {
    test(`WHAT[semantic-trace-002] separate captures preserve complete Host message identities (${address}, ${opaque ? 'raw delimiters and UTF-16' : 'slash'})`, async () => {
      const assert = (await import('node:assert/strict')).default
      const trace = await import('../../../dist/Context/Trace/SemanticTraceSurface.js')
      const { withReopenableJournal } = await import('./support/journal.mjs')
      await withReopenableJournal(async (handle, reopen) => {
        const session = `opaque-message-${address}-${opaque}`
        const ids = opaque
          ? ['real/msg:one/host-part:literal\0\ud800', 'real/msg:two/host-part:literal\0\ud801']
          : ['real/one', 'real/two']
        const rawAddress = 'same/msg:literal/host-part:address\0\ud800'
        const part = address === 'call'
          ? { type: 'tool', tool: 'read', callID: rawAddress, state: { status: 'running', input: 'same original bytes' } }
          : { ...(address === 'physical' ? { id: rawAddress } : {}), type: 'text', text: 'same original bytes' }
        const observation = id => [{ hostMessageId: id, message: { info: { id, role: 'assistant' }, parts: [structuredClone(part)] } }]
        for (const id of ids) {
          const captured = await trace.captureObservedMessages(handle, session, observation(id))
          assert.equal(captured.ok, true, JSON.stringify(captured.error))
          assert.equal(captured.receipt.identity, 'stable-host')
          assert.equal(captured.receipt.capturedPartCount, 1)
        }
        const semanticPart = address === 'call'
          ? trace.semanticToolCall('read', 'same original bytes') : trace.semanticText('same original bytes')
        const expected = { messages: ids.map(() => ({ role: 'assistant', parts: [semanticPart] })) }
        const records = trace.traceRecords(trace.snapshot(handle, session))
        for (const temperature of ['warm', 'cold']) {
          const current = temperature === 'warm' ? handle : await reopen()
          const projection = trace.snapshot(current, session)
          const parts = trace.orderedSemanticParts(projection)
          assert.deepEqual(parts.map(part => trace.tryHostMessageIdAt(part.cursor, projection)), ids)
          assert.deepEqual(trace.hostMessageFirstCursors(projection), Object.fromEntries(ids.map((id, index) => [id, BigInt(index + 1)])))
          for (const [index, id] of ids.entries()) {
            assert.deepEqual(trace.partsForHostMessageIds([id], projection).map(part => part.cursor.sequence), [index + 1])
            assert.equal(trace.tryTurnOfHostMessageId(id, projection), index)
            assert.deepEqual(trace.tryContiguousHostRange([id], projection), { start: { sequence: index + 1 }, endExclusive: { sequence: index + 2 } })
          }
          assert.deepEqual(trace.hostMessageIdsBeforeTurn(1, projection), [ids[0]])
          assert.deepEqual(await trace.currentProjection(current, session), expected)
          assert.deepEqual(trace.traceRecords(projection), records)
        }
        const cold = await reopen()
        for (const id of ids) {
          const replay = await trace.captureObservedMessages(cold, session, observation(id))
          assert.equal(replay.ok, true, JSON.stringify(replay.error))
          assert.equal(replay.receipt.identity, 'stable-host')
          assert.equal(replay.receipt.capturedPartCount, 0)
        }
      })
    })
  }
}

for (const root of ['real', 'real/msg:literal']) {
  test(`WHAT[semantic-trace-002] raw message and part delimiters cannot collapse two physical tuples (${root})`, async () => {
    const assert = (await import('node:assert/strict')).default
    const trace = await import('../../../dist/Context/Trace/SemanticTraceSurface.js')
    const { withReopenableJournal } = await import('./support/journal.mjs')
    await withReopenableJournal(async (handle, reopen) => {
      const session = `physical-tuple-${root}`
      const identities = [[`${root}/host-part:middle`, 'last'], [root, 'middle/host-part:last']]
      const observation = (id, partId) => [{ hostMessageId: id, message: { info: { role: 'user' }, parts: [{ id: partId, type: 'text', text: 'same original bytes' }] } }]
      for (const [id, partId] of identities) {
        const captured = await trace.captureObservedMessages(handle, session, observation(id, partId))
        assert.equal(captured.ok, true, JSON.stringify(captured.error))
        assert.equal(captured.receipt.capturedPartCount, 1)
      }
      const records = trace.traceRecords(trace.snapshot(handle, session))
      const cold = await reopen()
      const projection = trace.snapshot(cold, session)
      const parts = trace.orderedSemanticParts(projection)
      assert.equal(new Set(parts.map(part => part.provenance)).size, 2)
      assert.deepEqual(parts.map(part => trace.tryHostMessageIdAt(part.cursor, projection)), identities.map(([id]) => id))
      assert.deepEqual((await trace.currentProjection(cold, session)).messages.map(message => message.parts), [[trace.semanticText('same original bytes')], [trace.semanticText('same original bytes')]])
      assert.deepEqual(trace.traceRecords(projection), records)
      for (const [id, partId] of identities) {
        assert.equal((await trace.captureObservedMessages(cold, session, observation(id, partId))).receipt.capturedPartCount, 0)
      }
    })
  })
}

for (const missingFirst of [false, true]) {
  test(`WHAT[semantic-trace-002] explicit call evidence retains opaque identities across physical replay (${missingFirst ? 'missing' : 'physical'} first)`, async () => {
    const assert = (await import('node:assert/strict')).default
    const trace = await import('../../../dist/Context/Trace/SemanticTraceSurface.js')
    const { withReopenableJournal } = await import('./support/journal.mjs')
    await withReopenableJournal(async (handle, reopen) => {
      const session = `opaque-call-replay-${missingFirst}`
      const id = 'real/msg:literal/host-part:message\0\ud800'
      const call = 'call/msg:literal/host-part:call\0\ud801'
      const physical = 'part/msg:literal/host-part:part\0\ud802'
      const observation = missing => [{ hostMessageId: id, message: { info: { id, role: 'assistant' }, parts: [{ ...(missing ? {} : { id: physical }), type: 'tool', tool: 'read', callID: call, state: { status: 'running', input: 'actual input' } }] } }]
      const first = await trace.captureObservedMessages(handle, session, observation(missingFirst))
      assert.equal(first.ok, true, JSON.stringify(first.error))
      assert.equal(first.receipt.capturedPartCount, 1)
      const records = trace.traceRecords(trace.snapshot(handle, session))
      const cold = await reopen()
      const replay = await trace.captureObservedMessages(cold, session, observation(!missingFirst))
      assert.equal(replay.ok, true, JSON.stringify(replay.error))
      assert.equal(replay.receipt.capturedPartCount, 0)
      const projection = trace.snapshot(cold, session)
      const [part] = trace.orderedSemanticParts(projection)
      assert.equal(trace.tryHostMessageIdAt(part.cursor, projection), id)
      assert.equal(part.hostToolPartId, missingFirst ? null : physical)
      assert.equal(part.toolCallId, call)
      assert.deepEqual(trace.traceRecords(projection), records)
    })
  })
}

test('WHAT[semantic-trace-002] structured identities preserve proven tool wrapper aliases without broadening absent run evidence', async () => {
  const assert = (await import('node:assert/strict')).default
  const trace = await import('../../../dist/Context/Trace/SemanticTraceSurface.js')
  const { withReopenableJournal } = await import('./support/journal.mjs')
  await withReopenableJournal(async (handle, reopen) => {
    const observation = (wrapper, knownRun) => [{ hostMessageId: wrapper, message: { info: { ...(knownRun ? { id: 'actual-run' } : {}), role: 'assistant' }, parts: [{ id: 'actual/part', type: 'tool', tool: 'read', callID: 'actual/call', state: { status: 'running', input: 'actual input' } }] } }]
    for (const knownRun of [true, false]) {
      const session = `structured-tool-alias-${knownRun}`
      assert.equal((await trace.captureObservedMessages(handle, session, observation('first/msg:wrapper', knownRun))).receipt.capturedPartCount, 1)
      const second = await trace.captureObservedMessages(handle, session, observation('second/host-part:wrapper', knownRun))
      assert.equal(second.ok, true, JSON.stringify(second.error))
      assert.equal(second.receipt.capturedPartCount, knownRun ? 0 : 1)
      assert.equal(trace.orderedSemanticParts(trace.snapshot(handle, session)).length, knownRun ? 1 : 2)
    }
    const cold = await reopen()
    for (const knownRun of [true, false]) {
      assert.equal((await trace.captureObservedMessages(cold, `structured-tool-alias-${knownRun}`, observation('second/host-part:wrapper', knownRun))).receipt.capturedPartCount, 0)
    }
  })
})

for (const missingFirst of [false, true]) {
  test(`WHAT[semantic-trace-002] missing physical evidence and two proven wrappers share one call occurrence (${missingFirst ? 'missing' : 'physical'} first)`, async () => {
    const assert = (await import('node:assert/strict')).default
    const trace = await import('../../../dist/Context/Trace/SemanticTraceSurface.js')
    const { withReopenableJournal } = await import('./support/journal.mjs')
    await withReopenableJournal(async (handle, reopen) => {
      const session = `mixed-call-wrapper-${missingFirst}`
      const observation = (wrapper, missing) => ({ hostMessageId: wrapper, message: { info: { id: 'actual-run', role: 'assistant' }, parts: [{ ...(missing ? {} : { id: 'actual/part' }), type: 'tool', tool: 'read', callID: 'actual/call', state: { status: 'running', input: 'actual input' } }] } })
      const observations = [observation('missing/msg:wrapper', true), observation('first/msg:wrapper', false), observation('second/host-part:wrapper', false)]
      if (!missingFirst) observations.reverse()
      const captured = await trace.captureObservedMessages(handle, session, observations)
      assert.equal(captured.ok, true, JSON.stringify(captured.error))
      assert.equal(captured.receipt.capturedPartCount, 1)
      const records = trace.traceRecords(trace.snapshot(handle, session))
      const cold = await reopen()
      const replay = await trace.captureObservedMessages(cold, session, observations)
      assert.equal(replay.ok, true, JSON.stringify(replay.error))
      assert.equal(replay.receipt.capturedPartCount, 0)
      const projection = trace.snapshot(cold, session)
      const [part] = trace.orderedSemanticParts(projection)
      assert.equal(part.hostToolPartId, missingFirst ? null : 'actual/part')
      assert.equal(trace.tryHostMessageIdAt(part.cursor, projection), observations[0].hostMessageId)
      assert.deepEqual(trace.traceRecords(projection), records)
    })
  })
}

for (const metadataAt of ['info', 'message', 'part']) {
  for (const firstSource of ['observed', 'snapshot']) {
    test(`WHAT[semantic-trace-002] snapshot and transform preserve typed retry exclusion (${metadataAt}, ${firstSource} first)`, async () => {
      const assert = (await import('node:assert/strict')).default
      const trace = await import('../../../dist/Context/Trace/SemanticTraceSurface.js')
      const provider = await import('../../../dist/OpenCode/Codec/ProviderProjectionSurface.js')
      const { withReopenableJournal } = await import('./support/journal.mjs')
      await withReopenableJournal(async (handle, reopen) => {
        const session = `snapshot-retry-${metadataAt}-${firstSource}`
        const retry = { info: { id: 'retry-message', role: 'user' }, parts: [{ id: 'retry-part', type: 'text', text: 'transport control' }] }
        const target = metadataAt === 'info' ? retry.info : metadataAt === 'part' ? retry.parts[0] : retry
        target.metadata = { wanxiangshu_origin: 'ProviderRetryAttempt' }
        const raw = [retry, { info: { id: 'answer-message', role: 'assistant' }, parts: [{ id: 'answer-part', type: 'text', text: 'actual answer' }] }]
        const observations = raw.map(message => ({ message, hostMessageId: provider.hostMessageId(message), origin: provider.promptOriginOfMessage(message) }))
        const capture = (current, source) => source === 'observed'
          ? trace.captureObservedMessages(current, session, observations)
          : trace.captureSessionSnapshot(current, session, raw)
        const first = await capture(handle, firstSource)
        assert.equal(first.ok, true, JSON.stringify(first.error))
        assert.equal((firstSource === 'observed' ? first.receipt : first).capturedPartCount, 1)
        const records = trace.traceRecords(trace.snapshot(handle, session))
        const secondSource = firstSource === 'observed' ? 'snapshot' : 'observed'
        const second = await capture(handle, secondSource)
        assert.equal(second.ok, true, JSON.stringify(second.error))
        assert.equal((secondSource === 'observed' ? second.receipt : second).capturedPartCount, 0)
        const expected = { messages: [{ role: 'assistant', parts: [trace.semanticText('actual answer')] }] }
        assert.deepEqual(await trace.currentProjection(handle, session), expected)
        assert.deepEqual(trace.traceRecords(trace.snapshot(handle, session)), records)
        const cold = await reopen()
        assert.deepEqual(await trace.currentProjection(cold, session), expected)
        assert.deepEqual(trace.traceRecords(trace.snapshot(cold, session)), records)
        assert.equal((await capture(cold, 'snapshot')).capturedPartCount, 0)
      })
    })
  }
}

for (const firstSource of ['observed', 'snapshot']) {
  test(`WHAT[semantic-trace-002] a missing physical tool part ID stays missing across capture sources (${firstSource} first)`, async () => {
    const assert = (await import('node:assert/strict')).default
    const trace = await import('../../../dist/Context/Trace/SemanticTraceSurface.js')
    const { withReopenableJournal } = await import('./support/journal.mjs')
    await withReopenableJournal(async (handle, reopen) => {
      const session = `snapshot-no-part-id-${firstSource}`
      const raw = state => [{ info: { id: 'real-run', role: 'assistant' }, parts: [{ type: 'tool', tool: 'read', callID: 'real-call', state }] }]
      const capture = (current, source, messages) => source === 'observed'
        ? trace.captureObservedMessages(current, session, messages.map(message => ({ hostMessageId: message.info.id, message })))
        : trace.captureSessionSnapshot(current, session, messages)
      const running = raw({ status: 'running', input: { path: 'a' } })
      const completed = raw({ status: 'completed', output: 'actual result' })
      const secondSource = firstSource === 'observed' ? 'snapshot' : 'observed'
      for (const messages of [running, completed]) {
        const first = await capture(handle, firstSource, messages)
        assert.equal(first.ok, true, JSON.stringify(first.error))
        assert.equal((firstSource === 'observed' ? first.receipt : first).capturedPartCount, 1)
        const second = await capture(handle, secondSource, messages)
        assert.equal(second.ok, true, JSON.stringify(second.error))
        assert.equal((secondSource === 'observed' ? second.receipt : second).capturedPartCount, 0)
      }
      const parts = trace.orderedSemanticParts(trace.snapshot(handle, session))
      assert.equal(parts.length, 2)
      assert.deepEqual(parts.map(part => part.hostToolPartId), [null, null])
      const expected = { messages: [{ role: 'assistant', parts: [trace.semanticToolCall('read', '{"path":"a"}'), trace.semanticToolResult('actual result')] }] }
      assert.deepEqual(await trace.currentProjection(handle, session), expected)
      const records = trace.traceRecords(trace.snapshot(handle, session))
      const cold = await reopen()
      assert.deepEqual(trace.traceRecords(trace.snapshot(cold, session)), records)
      assert.deepEqual(await trace.currentProjection(cold, session), expected)
      assert.equal((await trace.captureSessionSnapshot(cold, session, completed)).capturedPartCount, 0)
    })
  })
}

for (const missingFirst of [true, false]) {
  test(`WHAT[semantic-trace-002] one call with mixed physical evidence is captured once (${missingFirst ? 'missing' : 'known'} first)`, async () => {
    const assert = (await import('node:assert/strict')).default
    const trace = await import('../../../dist/Context/Trace/SemanticTraceSurface.js')
    const { withReopenableJournal } = await import('./support/journal.mjs')
    await withReopenableJournal(async (handle, reopen) => {
      const session = `mixed-tool-evidence-${missingFirst}`
      const messageId = 'real/run'
      const tool = id => ({ ...(id ? { id } : {}), type: 'tool', tool: 'read', callID: 'real-call', state: { status: 'running', input: { path: 'a' } } })
      const parts = missingFirst ? [tool(null), tool('actual-part')] : [tool('actual-part'), tool(null)]
      const raw = [{ info: { id: messageId, role: 'assistant' }, parts }]
      const captured = await trace.captureSessionSnapshot(handle, session, raw)
      assert.equal(captured.ok, true, JSON.stringify(captured.error))
      assert.equal(captured.capturedPartCount, 1)
      const accepted = trace.orderedSemanticParts(trace.snapshot(handle, session))
      assert.equal(accepted.length, 1)
      assert.equal(accepted[0].hostToolPartId, missingFirst ? null : 'actual-part')
      const replay = await trace.captureObservedMessages(handle, session, [{ hostMessageId: messageId, message: raw[0] }])
      assert.equal(replay.ok, true, JSON.stringify(replay.error))
      assert.equal(replay.receipt.capturedPartCount, 0)
      const records = trace.traceRecords(trace.snapshot(handle, session))
      const cold = await reopen()
      assert.deepEqual(trace.traceRecords(trace.snapshot(cold, session)), records)
      assert.equal((await trace.captureSessionSnapshot(cold, session, raw)).capturedPartCount, 0)
    })
  })
}

test('WHAT[semantic-trace-002] concurrent call-only and physically addressed captures share one confirmed material', async () => {
  const assert = (await import('node:assert/strict')).default
  const trace = await import('../../../dist/Context/Trace/SemanticTraceSurface.js')
  const { withReopenableJournal } = await import('./support/journal.mjs')
  await withReopenableJournal(async (handle, reopen) => {
    const session = 'concurrent-mixed-tool-evidence'
    const missing = { info: { id: 'real-run', role: 'assistant' }, parts: [{ type: 'tool', tool: 'read', callID: 'real-call', state: { status: 'running', input: { path: 'a' } } }] }
    const known = structuredClone(missing)
    known.parts[0].id = 'actual-part'
    const [snapshot, observed] = await Promise.all([
      trace.captureSessionSnapshot(handle, session, [missing]),
      trace.captureObservedMessages(handle, session, [{ hostMessageId: 'real-run', message: known }]),
    ])
    assert.equal(snapshot.ok, true, JSON.stringify(snapshot.error))
    assert.equal(observed.ok, true, JSON.stringify(observed.error))
    assert.equal(snapshot.capturedPartCount + observed.receipt.capturedPartCount, 1)
    const records = trace.traceRecords(trace.snapshot(handle, session))
    assert.equal(trace.orderedSemanticParts(trace.snapshot(handle, session)).length, 1)
    assert.deepEqual(trace.traceRecords(trace.snapshot(await reopen(), session)), records)
  })
})

for (const conflict of ['bytes', 'multiple-addresses', 'existing-address']) {
  test(`WHAT[semantic-trace-002] uncertain call evidence is refused before its first append (${conflict})`, async () => {
    const assert = (await import('node:assert/strict')).default
    const trace = await import('../../../dist/Context/Trace/SemanticTraceSurface.js')
    const { withReopenableJournal } = await import('./support/journal.mjs')
    await withReopenableJournal(async (handle, reopen) => {
      const session = `mixed-tool-conflict-${conflict}`
      const tool = (id, input = 'same') => ({ ...(id ? { id } : {}), type: 'tool', tool: 'read', callID: 'real-call', state: { status: 'running', input } })
      const raw = parts => [{ info: { id: 'real-run', role: 'assistant' }, parts }]
      if (conflict === 'existing-address') {
        assert.equal((await trace.captureSessionSnapshot(handle, session, raw([tool('first-part')]))).ok, true)
      }
      const before = trace.traceRecords(trace.snapshot(handle, session))
      const parts = conflict === 'bytes' ? [tool('actual-part'), tool(null, 'changed')]
        : conflict === 'multiple-addresses' ? [tool('first-part'), tool('second-part'), tool(null)]
          : [tool('second-part'), tool(null)]
      const refused = await trace.captureSessionSnapshot(handle, session, raw(parts))
      assert.equal(refused.ok, false)
      assert.match(JSON.stringify(refused.error), /conflict|ambiguous/i)
      assert.deepEqual(trace.traceRecords(trace.snapshot(handle, session)), before)
      assert.deepEqual(trace.traceRecords(trace.snapshot(await reopen(), session)), before)
    })
  })
}

test('WHAT[semantic-trace-002] a call-only observation cannot choose between two confirmed physical parts', async () => {
  const assert = (await import('node:assert/strict')).default
  const trace = await import('../../../dist/Context/Trace/SemanticTraceSurface.js')
  const { withReopenableJournal } = await import('./support/journal.mjs')
  await withReopenableJournal(async handle => {
    const session = 'ambiguous-call-only'
    const tool = id => ({ ...(id ? { id } : {}), type: 'tool', tool: 'read', callID: 'real-call', state: { status: 'running', input: 'same' } })
    const raw = parts => [{ info: { id: 'real-run', role: 'assistant' }, parts }]
    assert.equal((await trace.captureSessionSnapshot(handle, session, raw([tool('first-part'), tool('second-part')]))).ok, true)
    const before = trace.traceRecords(trace.snapshot(handle, session))
    const refused = await trace.captureSessionSnapshot(handle, session, raw([tool(null)]))
    assert.equal(refused.ok, false)
    assert.match(JSON.stringify(refused.error), /ambiguous/i)
    assert.deepEqual(trace.traceRecords(trace.snapshot(handle, session)), before)
  })
})

test('WHAT[semantic-trace-002] missing tool part IDs retain tool state but cannot become physical locations', async () => {
  const assert = (await import('node:assert/strict')).default
  const snapshot = await import('../../../dist/OpenCode/Host/SessionSnapshotSurface.js')
  const raw = id => ({ info: { id: 'real-run', role: 'assistant' }, parts: [{ ...(id ? { id } : {}), type: 'tool', tool: 'read', callID: 'real-call', state: { status: 'completed', output: 'actual result' } }] })
  const missing = snapshot.projectMessages([raw(null)])
  assert.deepEqual(snapshot.toolPartStateAt(missing, 0, 0), { ok: true, state: 'completed', canonical: '"actual result"' })
  assert.equal(snapshot.locateToolCall('real-call', missing).error, 'Missing')
  const complete = snapshot.locateToolCall('real-call', snapshot.projectMessages([raw('actual-part')]))
  assert.equal(complete.ok, true)
  assert.equal(complete.hostToolPartId, 'actual-part')
  const ambiguous = snapshot.locateToolCall('real-call', snapshot.projectMessages([raw(null), raw('actual-part')]))
  assert.equal(ambiguous.ok, false)
  assert.equal(ambiguous.error, 'Ambiguous', 'an unaddressable duplicate cannot be filtered out before uniqueness is checked')
})

for (const stage of ['pending-claim', 'accepted-physical']) {
  for (const metadataOrigin of ['UnknownOrigin', 'HostInternal', 'BusyAgentNudge']) {
    test(`WHAT[semantic-trace-002] exact durable retry evidence precedes diagnostic Host origin (${stage}, ${metadataOrigin})`, async () => {
      const assert = (await import('node:assert/strict')).default
      const trace = await import('../../../dist/Context/Trace/SemanticTraceSurface.js')
      const dispatch = await import('../../../dist/Interaction/Dispatch/DispatchSurface.js')
      const { withJournal, hostPort, prepareAcceptanceObserver, acceptObservedPhysical } = await import('../../dispatch-protocol/tests/support/authority.mjs')
      await withJournal('trace-origin-precedence', async (handle, reopen) => {
        const context = await prepareAcceptanceObserver(handle, 'continuation', 'trace-origin-precedence')
        const sent = await dispatch.sendContinuation(hostPort(async () => dispatch.admittedWithReceipt('native-origin-precedence')), handle, context.session, 'actual retry control', 'ProviderRetryAttempt', context.profile, 'Await')
        assert.equal(sent.ok, true, sent.error)
        if (stage === 'accepted-physical') assert.equal((await acceptObservedPhysical(handle, context, sent.key, 'physical-retry')).ok, true)
        const metadata = { wanxiangshu_origin: metadataOrigin }
        if (stage === 'pending-claim') metadata.wanxiangshu_prompt_key = sent.key
        const raw = [{ info: { id: 'physical-retry', role: 'user', metadata }, parts: [{ id: 'actual-part', type: 'text', text: 'actual retry control' }] }]
        for (const temperature of ['warm', 'cold']) {
          const current = temperature === 'warm' ? handle : await reopen()
          for (const source of ['observed', 'snapshot']) {
            const captured = source === 'observed'
              ? await trace.captureObservedMessages(current, context.session, raw.map(message => ({ hostMessageId: message.info.id, origin: metadataOrigin, message })))
              : await trace.captureSessionSnapshot(current, context.session, raw)
            assert.equal(captured.ok, true, JSON.stringify(captured.error))
            assert.equal(source === 'observed' ? captured.receipt.capturedPartCount : captured.capturedPartCount, 0, `${temperature} ${source}`)
          }
          assert.deepEqual(await trace.currentProjection(current, context.session), { messages: [] })
          assert.deepEqual(trace.traceRecords(trace.snapshot(current, context.session)), [])
        }
      })
    })
  }
}

for (const stage of ['pending-claim', 'accepted-physical']) {
  test(`WHAT[semantic-trace-002] durable ordinary origin is preserved under a Host retry label (${stage})`, async () => {
    const assert = (await import('node:assert/strict')).default
    const trace = await import('../../../dist/Context/Trace/SemanticTraceSurface.js')
    const dispatch = await import('../../../dist/Interaction/Dispatch/DispatchSurface.js')
    const { withJournal, hostPort, prepareAcceptanceObserver, acceptObservedPhysical } = await import('../../dispatch-protocol/tests/support/authority.mjs')
    await withJournal('trace-ordinary-precedence', async (handle, reopen) => {
      const context = await prepareAcceptanceObserver(handle, 'continuation', 'trace-ordinary-precedence')
      const sent = await dispatch.sendContinuation(hostPort(async () => dispatch.admittedWithReceipt('native-ordinary-precedence')), handle, context.session, 'actual ordinary continuation', 'BusyAgentNudge', context.profile, 'Await')
      assert.equal(sent.ok, true, sent.error)
      if (stage === 'accepted-physical') assert.equal((await acceptObservedPhysical(handle, context, sent.key, 'physical-ordinary')).ok, true)
      const metadata = { wanxiangshu_origin: 'ProviderRetryAttempt' }
      if (stage === 'pending-claim') metadata.wanxiangshu_prompt_key = sent.key
      const raw = [{ info: { id: 'physical-ordinary', role: 'user', metadata }, parts: [{ id: 'actual-part', type: 'text', text: 'actual ordinary continuation' }] }]
      for (const temperature of ['warm', 'cold']) {
        const current = temperature === 'warm' ? handle : await reopen()
        for (const source of ['observed', 'snapshot']) {
          const captured = source === 'observed'
            ? await trace.captureObservedMessages(current, context.session, raw.map(message => ({ hostMessageId: message.info.id, origin: 'ProviderRetryAttempt', message })))
            : await trace.captureSessionSnapshot(current, context.session, raw)
          assert.equal(captured.ok, true, JSON.stringify(captured.error))
          assert.equal(source === 'observed' ? captured.receipt.capturedPartCount : captured.capturedPartCount, temperature === 'warm' && source === 'observed' ? 1 : 0, `${temperature} ${source}`)
        }
        assert.deepEqual(await trace.currentProjection(current, context.session), { messages: [{ role: 'user', parts: [trace.semanticText('actual ordinary continuation')] }] })
      }
    })
  })
}

test('WHAT[semantic-trace-002] accepted physical origin precedes a different exact pending PromptKey', async () => {
  const assert = (await import('node:assert/strict')).default
  const trace = await import('../../../dist/Context/Trace/SemanticTraceSurface.js')
  const dispatch = await import('../../../dist/Interaction/Dispatch/DispatchSurface.js')
  const { withJournal, hostPort, prepareAcceptanceObserver, acceptObservedPhysical } = await import('../../dispatch-protocol/tests/support/authority.mjs')
  await withJournal('trace-physical-precedence', async (handle, reopen) => {
    const context = await prepareAcceptanceObserver(handle, 'continuation', 'trace-physical-precedence')
    const port = hostPort(async () => dispatch.admittedWithReceipt('native-physical-precedence'))
    const retry = await dispatch.sendContinuation(port, handle, context.session, 'actual retry control', 'ProviderRetryAttempt', context.profile, 'Await')
    assert.equal(retry.ok, true, retry.error)
    assert.equal((await acceptObservedPhysical(handle, context, retry.key, 'physical-retry')).ok, true)
    const ordinary = await dispatch.sendContinuation(port, handle, context.session, 'ordinary continuation', 'BusyAgentNudge', context.profile, 'Await')
    assert.equal(ordinary.ok, true, ordinary.error)
    const raw = [{ info: { id: 'physical-retry', role: 'user', metadata: { wanxiangshu_prompt_key: ordinary.key, wanxiangshu_origin: 'HostInternal' } }, parts: [{ id: 'actual-part', type: 'text', text: 'actual retry control' }] }]
    for (const temperature of ['warm', 'cold']) {
      const current = temperature === 'warm' ? handle : await reopen()
      const observed = await trace.captureObservedMessages(current, context.session, raw.map(message => ({ hostMessageId: message.info.id, origin: 'HostInternal', message })))
      const snapshot = await trace.captureSessionSnapshot(current, context.session, raw)
      assert.equal(observed.ok, true, JSON.stringify(observed.error))
      assert.equal(snapshot.ok, true, JSON.stringify(snapshot.error))
      assert.equal(observed.receipt.capturedPartCount, 0)
      assert.equal(snapshot.capturedPartCount, 0)
      assert.deepEqual(trace.traceRecords(trace.snapshot(current, context.session)), [])
    }
  })
})

test('WHAT[semantic-trace-002] exact PromptKey resolution works without a physical message identity and ignores unrelated evidence', async () => {
  const assert = (await import('node:assert/strict')).default
  const trace = await import('../../../dist/Context/Trace/SemanticTraceSurface.js')
  const dispatch = await import('../../../dist/Interaction/Dispatch/DispatchSurface.js')
  const { withJournal, hostPort, prepareAcceptanceObserver, acceptObservedPhysical } = await import('../../dispatch-protocol/tests/support/authority.mjs')
  await withJournal('trace-exact-origin', async (handle, reopen) => {
    const context = await prepareAcceptanceObserver(handle, 'continuation', 'trace-exact-origin')
    const sent = await dispatch.sendContinuation(hostPort(async () => dispatch.admittedWithReceipt('native-exact-origin')), handle, context.session, 'same original bytes', 'ProviderRetryAttempt', context.profile, 'Await')
    assert.equal(sent.ok, true, sent.error)
    const rawMissingId = [{ info: { role: 'user', metadata: { wanxiangshu_prompt_key: sent.key, wanxiangshu_origin: 'HostInternal' } }, parts: [{ id: 'actual-part', type: 'text', text: 'same original bytes' }] }]
    for (const temperature of ['warm', 'cold']) {
      const current = temperature === 'warm' ? handle : await reopen()
      const captured = await trace.captureObservedMessages(current, context.session, rawMissingId.map(message => ({ message, origin: 'HostInternal' })))
      assert.equal(captured.ok, true, JSON.stringify(captured.error))
      assert.equal(captured.receipt.capturedPartCount, 0, temperature)
      assert.deepEqual(trace.traceRecords(trace.snapshot(current, context.session)), [])
    }
    const cold = await reopen()
    assert.equal((await acceptObservedPhysical(cold, context, sent.key, 'accepted-retry')).ok, true)
    const foreign = await prepareAcceptanceObserver(cold, 'continuation', 'trace-foreign-origin')
    const checks = [
      { label: 'foreign-session', session: foreign.session, id: 'accepted-retry', key: sent.key, role: 'user' },
      { label: 'foreign-key', session: context.session, id: 'ordinary-message', key: 'unrelated-key', role: 'user' },
      { label: 'accepted-key-only', session: context.session, id: 'other-message', key: sent.key, role: 'user' },
      { label: 'assistant', session: context.session, id: 'accepted-retry', key: sent.key, role: 'assistant' },
    ]
    for (const check of checks) {
      const raw = [{ info: { id: check.id, role: check.role, metadata: { wanxiangshu_prompt_key: check.key, wanxiangshu_origin: 'HostInternal' } }, parts: [{ id: check.label, type: 'text', text: 'same original bytes' }] }]
      const observed = await trace.captureObservedMessages(cold, check.session, raw.map(message => ({ hostMessageId: message.info.id, origin: 'HostInternal', message })))
      const snapshot = await trace.captureSessionSnapshot(cold, check.session, raw)
      assert.equal(observed.ok, true, JSON.stringify(observed.error))
      assert.equal(snapshot.ok, true, JSON.stringify(snapshot.error))
      assert.equal(observed.receipt.capturedPartCount, 1, check.label)
      assert.equal(snapshot.capturedPartCount, 0, check.label)
    }
    const restored = await reopen()
    assert.equal(trace.orderedSemanticParts(trace.snapshot(restored, foreign.session)).length, 1)
    assert.equal(trace.orderedSemanticParts(trace.snapshot(restored, context.session)).length, 3)
  })
})

test('WHAT[semantic-trace-002] snapshot retry exclusion can use exact durable claim and landing evidence after metadata loss', async () => {
  const assert = (await import('node:assert/strict')).default
  const trace = await import('../../../dist/Context/Trace/SemanticTraceSurface.js')
  const dispatch = await import('../../../dist/Interaction/Dispatch/DispatchSurface.js')
  const { withJournal, hostPort, prepareAcceptanceObserver, acceptObservedPhysical } = await import('../../dispatch-protocol/tests/support/authority.mjs')
  await withJournal('trace-source-origin', async (handle, reopen) => {
    const context = await prepareAcceptanceObserver(handle, 'continuation', 'trace-source-origin')
    const sent = await dispatch.sendContinuation(
      hostPort(async () => dispatch.admittedWithReceipt('native-source-origin')),
      handle, context.session, 'same control bytes', 'ProviderRetryAttempt', context.profile, 'Await',
    )
    assert.equal(sent.ok, true, sent.error)
    assert.equal(dispatch.pendingClaimCount(handle, context.session), 1)
    const raw = [{ info: { id: 'actual-retry-message', role: 'user' }, parts: [{ id: 'actual-retry-part', type: 'text', text: 'same control bytes', metadata: { wanxiangshu_prompt_key: sent.key } }] }]
    const pending = await trace.captureSessionSnapshot(handle, context.session, raw)
    assert.equal(pending.ok, true, JSON.stringify(pending.error))
    assert.equal(pending.capturedPartCount, 0, 'an exact pending PromptKey retains its typed Origin')
    const accepted = await acceptObservedPhysical(handle, context, sent.key, 'actual-retry-message')
    assert.equal(accepted.ok, true, accepted.error)
    assert.equal(dispatch.pendingClaimCount(handle, context.session), 0)
    const withoutMetadata = structuredClone(raw)
    delete withoutMetadata[0].parts[0].metadata
    assert.equal((await trace.captureSessionSnapshot(handle, context.session, withoutMetadata)).capturedPartCount, 0)
    const cold = await reopen()
    assert.equal((await trace.captureSessionSnapshot(cold, context.session, withoutMetadata)).capturedPartCount, 0)
    assert.deepEqual(trace.traceRecords(trace.snapshot(cold, context.session)), [])
    const foreign = await trace.captureSessionSnapshot(cold, 'unrelated-session', raw)
    assert.equal(foreign.ok, true, JSON.stringify(foreign.error))
    assert.equal(foreign.capturedPartCount, 1, 'the same bytes and key in another session do not inherit retry Origin')
    assert.deepEqual(await trace.currentProjection(cold, 'unrelated-session'), { messages: [{ role: 'user', parts: [trace.semanticText('same control bytes')] }] })
  })
})

test('WHAT[semantic-trace-002] a legacy snapshot address fabricated from callID is read without copying it into new evidence', async () => {
  const assert = (await import('node:assert/strict')).default
  const trace = await import('../../../dist/Context/Trace/SemanticTraceSurface.js')
  const journal = await import('../../../dist/Persistence/Journal/Surface.js')
  const { withReopenableJournal } = await import('./support/journal.mjs')
  await withReopenableJournal(async (handle, reopen) => {
    const session = 'legacy-call-id-address'
    const payload = await journal.JournalSurface_writePayload(handle, '{"path":"a"}')
    assert.equal(payload.ok, true)
    const appended = await journal.JournalSurface_appendAgent(handle, { kind: 'Session', session }, 'real-run', {
      family: 'Companion', case: 'XTracePartAppended', payload: {
        SessionId: session, CursorSequence: 1n, Role: 'assistant', Turn: 0, PartIndex: 0, Kind: 'tool_call', ToolName: 'read',
        TextRef: payload.blobRef, TextDigest: payload.blobDigest, ProviderRun: 'real-run', ToolCallId: 'real-call', HostToolPartId: 'real-call',
        Provenance: 'g:0/msg:real-run/host-part:real-call/tool-call',
      },
    })
    assert.equal(appended.ok, true, JSON.stringify(appended.error))
    const before = trace.traceRecords(trace.snapshot(handle, session))
    const raw = [{ info: { id: 'real-run', role: 'assistant' }, parts: [{ type: 'tool', tool: 'read', callID: 'real-call', state: { status: 'running', input: { path: 'a' } } }] }]
    const replay = await trace.captureSessionSnapshot(handle, session, raw)
    assert.equal(replay.ok, true, JSON.stringify(replay.error))
    assert.equal(replay.capturedPartCount, 0)
    assert.equal(trace.orderedSemanticParts(trace.snapshot(handle, session))[0].hostToolPartId, 'real-call', 'accepted history is preserved, not rewritten')
    const cold = await reopen()
    assert.deepEqual(trace.traceRecords(trace.snapshot(cold, session)), before)
    assert.equal((await trace.captureSessionSnapshot(cold, session, raw)).capturedPartCount, 0)
  })
})

for (const facet of ['tool-call', 'tool-result']) {
  test(`WHAT[semantic-trace-002] the published 0.6.3 ${facet} suffix replays without backfilling missing physical evidence`, async () => {
    const assert = (await import('node:assert/strict')).default
    const trace = await import('../../../dist/Context/Trace/SemanticTraceSurface.js')
    const journal = await import('../../../dist/Persistence/Journal/Surface.js')
    const { withReopenableJournal } = await import('./support/journal.mjs')
    await withReopenableJournal(async (handle, reopen) => {
      const session = `published-suffix-${facet}`
      const body = facet === 'tool-call' ? '{"path":"a"}' : 'actual result'
      const payload = await journal.JournalSurface_writePayload(handle, body)
      assert.equal(payload.ok, true)
      const appended = await journal.JournalSurface_appendAgent(handle, { kind: 'Session', session }, 'real-run', {
        family: 'Companion', case: 'XTracePartAppended', payload: {
          SessionId: session, CursorSequence: 1n, Role: 'assistant', Turn: 0, PartIndex: 0,
          Kind: facet.replace('-', '_'), ToolName: facet === 'tool-call' ? 'read' : null,
          TextRef: payload.blobRef, TextDigest: payload.blobDigest, ProviderRun: 'real-run', ToolCallId: 'real-call', HostToolPartId: null,
          Provenance: `g:0/msg:real-run/host-part:actual-part/${facet}`,
        },
      })
      assert.equal(appended.ok, true, JSON.stringify(appended.error))
      const raw = [{ info: { id: 'real-run', role: 'assistant' }, parts: [{ id: 'actual-part', type: 'tool', tool: 'read', callID: 'real-call', state: facet === 'tool-call' ? { status: 'running', input: { path: 'a' } } : { status: 'completed', output: body } }] }]
      const before = trace.traceRecords(trace.snapshot(handle, session))
      const replay = await trace.captureSessionSnapshot(handle, session, raw)
      assert.equal(replay.ok, true, JSON.stringify(replay.error))
      assert.equal(replay.capturedPartCount, 0)
      assert.equal(trace.orderedSemanticParts(trace.snapshot(handle, session))[0].hostToolPartId, null)
      const cold = await reopen()
      assert.deepEqual(trace.traceRecords(trace.snapshot(cold, session)), before)
      assert.equal((await trace.captureSessionSnapshot(cold, session, raw)).capturedPartCount, 0)
    })
  })
}

for (const facet of ['tool-call', 'tool-result']) {
  for (const shape of ['raw-id-suffix', 'facet-suffix']) {
    test(`WHAT[semantic-trace-002] a verified legacy ${shape} preserves the actual physical address (${facet})`, async () => {
      const assert = (await import('node:assert/strict')).default
      const trace = await import('../../../dist/Context/Trace/SemanticTraceSurface.js')
      const journal = await import('../../../dist/Persistence/Journal/Surface.js')
      const { withReopenableJournal } = await import('./support/journal.mjs')
      await withReopenableJournal(async (handle, reopen) => {
        const session = `verified-legacy-id-${shape}-${facet}`
        const actualPartId = shape === 'raw-id-suffix' ? `part/${facet}` : 'part'
        const newPartId = shape === 'raw-id-suffix' ? 'part' : `part/${facet}`
        const body = facet === 'tool-call' ? '{"path":"a"}' : 'actual result'
        const payload = await journal.JournalSurface_writePayload(handle, body)
        assert.equal(payload.ok, true)
        const appended = await journal.JournalSurface_appendAgent(handle, { kind: 'Session', session }, 'actual-message', {
          family: 'Companion', case: 'XTracePartAppended', payload: {
            SessionId: session, CursorSequence: 1n, Role: 'assistant', Turn: 0, PartIndex: 0, Kind: facet.replace('-', '_'),
            ToolName: facet === 'tool-call' ? 'read' : null, TextRef: payload.blobRef, TextDigest: payload.blobDigest,
            Provenance: `g:0/msg:actual-message/host-part:part/${facet}`, ProviderRun: 'actual-message', ToolCallId: 'actual-call', HostToolPartId: actualPartId,
          },
        })
        assert.equal(appended.ok, true, JSON.stringify(appended.error))
        const captured = await trace.captureObservedMessages(handle, session, [{ hostMessageId: 'actual-message', message: { info: { id: 'actual-message', role: 'assistant' }, parts: [{ id: newPartId, type: 'text', text: 'separate physical material' }] } }])
        assert.equal(captured.ok, true, JSON.stringify(captured.error))
        assert.equal(captured.receipt.capturedPartCount, 1)
        assert.equal(trace.orderedSemanticParts(trace.snapshot(handle, session)).length, 2)
        const records = trace.traceRecords(trace.snapshot(handle, session))
        assert.deepEqual(trace.traceRecords(trace.snapshot(await reopen(), session)), records)
      })
    })
  }
}

test('WHAT[semantic-trace-002] one batch can confirm both facets of the same physical tool', async () => {
  const assert = (await import('node:assert/strict')).default
  const trace = await import('../../../dist/Context/Trace/SemanticTraceSurface.js')
  const { withReopenableJournal } = await import('./support/journal.mjs')
  await withReopenableJournal(async (handle, reopen) => {
    const session = 'one-batch-physical-facets'
    const parts = [
      { id: 'actual-part', type: 'tool', tool: 'read', callID: 'actual-call', state: { status: 'running', input: { path: 'a' } } },
      { id: 'actual-part', type: 'tool', tool: 'read', callID: 'actual-call', state: { status: 'completed', output: 'actual result' } },
    ]
    const observation = [{ hostMessageId: 'actual-message', message: { info: { id: 'actual-message', role: 'assistant' }, parts } }]
    const captured = await trace.captureObservedMessages(handle, session, observation)
    assert.equal(captured.ok, true, JSON.stringify(captured.error))
    assert.equal(captured.receipt.capturedPartCount, 2)
    const expected = { messages: [{ role: 'assistant', parts: [trace.semanticToolCall('read', '{"path":"a"}'), trace.semanticToolResult('actual result')] }] }
    assert.deepEqual(await trace.currentProjection(handle, session), expected)
    const records = trace.traceRecords(trace.snapshot(handle, session))
    const cold = await reopen()
    assert.deepEqual(trace.traceRecords(trace.snapshot(cold, session)), records)
    assert.deepEqual(await trace.currentProjection(cold, session), expected)
    assert.equal((await trace.captureObservedMessages(cold, session, observation)).receipt.capturedPartCount, 0)
  })
})

for (const facet of ['tool-call', 'tool-result']) {
  test(`WHAT[semantic-trace-002] raw physical IDs cannot collide with the ${facet} facet`, async () => {
    const assert = (await import('node:assert/strict')).default
    const trace = await import('../../../dist/Context/Trace/SemanticTraceSurface.js')
    const { withReopenableJournal } = await import('./support/journal.mjs')
    await withReopenableJournal(async (handle, reopen) => {
      const session = `facet-namespace-${facet}`
      const observation = [{ hostMessageId: 'message', message: {
        info: { id: 'message', role: 'assistant' },
        parts: [
          { id: `part/${facet}`, type: 'text', text: 'separate original' },
          { id: 'part', type: 'tool', tool: 'read', callID: 'call', state: facet === 'tool-call'
            ? { status: 'running', input: 'input' } : { status: 'completed', output: 'output' } },
        ],
      } }]
      const first = await trace.captureObservedMessages(handle, session, observation)
      assert.equal(first.ok, true, first.error)
      assert.equal(first.receipt.capturedPartCount, 2)
      const records = trace.traceRecords(trace.snapshot(handle, session))
      const cold = await reopen()
      assert.deepEqual(trace.traceRecords(trace.snapshot(cold, session)), records)
      assert.equal((await trace.captureObservedMessages(cold, session, observation)).receipt.capturedPartCount, 0)
      assert.deepEqual((await trace.currentProjection(cold, session)).messages[0].parts.map(part => part.kind), ['text', facet])
    })
  })
}

test('WHAT[semantic-trace-002] a real Host tool part keeps its confirmed call and completed result as distinct material', async () => {
  const assert = (await import('node:assert/strict')).default
  const { withReopenableJournal } = await import('./support/journal.mjs')
  const trace = await import('../../../dist/Context/Trace/SemanticTraceSurface.js')
  await withReopenableJournal(async (handle, reopen) => {
    const observation = state => [{ hostMessageId: 'tool-message', message: { info: { id: 'actual-run', role: 'assistant' }, parts: [{ id: 'actual-tool-part', type: 'tool', tool: 'read', callID: 'actual-call', state }] } }]
    const running = observation({ status: 'running', input: { path: 'a' } })
    const completed = observation({ status: 'completed', output: 'actual-result' })
    assert.equal((await trace.captureObservedMessages(handle, 'tool-facets', running)).ok, true)
    const result = await trace.captureObservedMessages(handle, 'tool-facets', completed)
    assert.equal(result.ok, true, JSON.stringify(result.error))
    assert.equal(result.receipt.capturedPartCount, 1)
    const original = await trace.currentProjection(handle, 'tool-facets')
    assert.deepEqual(original.messages[0].parts, [trace.semanticToolCall('read', '{"path":"a"}'), trace.semanticToolResult('actual-result')])
    assert.equal((await trace.captureObservedMessages(handle, 'tool-facets', completed)).receipt.capturedPartCount, 0)
    assert.deepEqual(await trace.currentProjection(await reopen(), 'tool-facets'), original)
  })
})

{
const { default: assert } = await import("node:assert/strict");
const { readFileSync } = await import("node:fs");
const { default: test } = await import("node:test");
const trace = await import("../../../dist/Context/Trace/SemanticTraceSurface.js");

const unwrap = (result) => {
  assert.equal(result.ok, true, result.ok ? '' : result.error)
  return result.projection
}
const partDescriptor = {
  sequence: 1,
  role: 'assistant',
  provenance: 'g:0/msg:message-1/host-part:part-1',
  turn: 0,
  partIndex: 0,
  kind: 'tool_call',
  toolName: 'todowrite-for-engineer',
  textRef: 'blob-1',
  textDigest: 'digest-1',
  body: '{}',
  providerRun: 'provider-run-1',
  toolCallId: 'call-1',
  hostToolPartId: 'part-1',
}

test('WHAT[semantic-trace-002] copied semantic evidence excludes transport metadata', () => {
  const projection = unwrap(trace.appendPart(trace.emptyProjection(), partDescriptor))
  const evidence = trace.orderedSemanticParts(projection)[0]
  assert.equal(evidence.providerRun, 'provider-run-1')
  assert.equal(evidence.toolName, 'todowrite-for-engineer')
  for (const forbidden of ['usage', 'cost', 'timestamp', 'elapsed', 'directory', 'finishReason', 'runtimeId', 'tokens', 'uiDelta']) {
    assert.equal(Object.hasOwn(evidence, forbidden), false, `semantic evidence must not carry ${forbidden}`)
  }
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { mkdtempSync, readFileSync, rmSync } = await import("node:fs");
const { tmpdir } = await import("node:os");
const { join, resolve } = await import("node:path");
const journal = await import("../../../dist/Persistence/Journal/Surface.js");
const trace = await import("../../../dist/Context/Trace/SemanticTraceSurface.js");

const SESSION = 'ses_semantic_capture'
const withJournal = async (fn) => {
  const dir = mkdtempSync(join(tmpdir(), 'semantic-trace-'))
  const created = await journal.JournalSurface_boot(dir, 'rt_semantic_trace', 4242, '2026-01-01T00:00:00Z')
  assert.equal(created.ok, true, created.ok ? '' : JSON.stringify(created.error))
  try {
    return await fn(created.journal)
  } finally {
    journal.JournalSurface_dispose(created.journal)
    rmSync(dir, { recursive: true, force: true })
  }
}
const projection = (messages) => ({ messages })

test('WHAT[semantic-trace-002] typed retry observation retains stable identity but appends no semantics', async () => {
  await withJournal(async (handle) => {
    const captured = await trace.captureObservedMessages(handle, SESSION, [
      {
        hostMessageId: 'retry-message',
        origin: 'ProviderRetryAttempt',
        message: { info: { id: 'retry-run', role: 'user' }, parts: [{ id: 'retry-part', type: 'text', text: 'transport retry' }] },
      },
      {
        hostMessageId: 'answer-message',
        message: { info: { id: 'answer-run', role: 'assistant' }, parts: [{ id: 'answer-part', type: 'text', text: 'semantic answer' }] },
      },
    ])
    assert.equal(captured.ok, true, captured.ok ? '' : captured.error)
    assert.equal(captured.receipt.identity, 'stable-host')
    assert.equal(captured.receipt.capturedPartCount, 1)
    assert.deepEqual(trace.orderedSemanticParts(captured.projection).map((part) => trace.tryHostMessageIdAt(part.cursor, captured.projection)), ['answer-message'])
    assert.deepEqual(
      trace.orderedSemanticParts(captured.projection),
      trace.orderedSemanticParts(trace.snapshot(handle, SESSION)),
      'the returned opaque current projection is the resulting owner state',
    )
  })
})
test('WHAT[semantic-trace-002] retry-like ordinary text is preserved and metadata does not become semantic material', async () => {
  await withJournal(async (handle) => {
    const body = '上一物理尝试失败，请继续。\n/real/path is part of the task.'
    const ordinary = {
      hostMessageId: 'ordinary',
      message: {
        info: { id: 'ordinary-run', role: 'user', cost: 42, timestamp: 123, directory: '/metadata-only' },
        parts: [{ id: 'ordinary-part', type: 'text', text: body }],
      },
    }
    const retry = {
      hostMessageId: 'retry', origin: 'ProviderRetryAttempt',
      message: { info: { id: 'retry-run', role: 'user' }, parts: [{ id: 'retry-part', type: 'text', text: 'arbitrary control text' }] },
    }
    const captured = await trace.captureObservedMessages(handle, SESSION, [retry, ordinary])
    assert.equal(captured.ok, true)
    assert.equal(captured.receipt.capturedPartCount, 1)
    assert.deepEqual(await trace.currentProjection(handle, SESSION), {
      messages: [{ role: 'user', parts: [{ kind: 'text', text: body }] }],
    })
    const replay = await trace.captureObservedMessages(handle, SESSION, [ordinary])
    assert.equal(replay.receipt.capturedPartCount, 0, 'removing the transport row must not renumber the existing physical part')
  })
})
test('WHAT[semantic-trace-002] missing, blank or duplicate Host ids cannot receive stable capture identity', async () => {
  await withJournal(async (handle) => {
    const observation = (id, index) => ({
      hostMessageId: id,
      message: { info: { id: `run-${index}`, role: 'assistant' }, parts: [{ id: `part-${index}`, type: 'text', text: 'body' }] },
    })
    for (const [index, ids] of [[undefined], [' '], ['same', 'same'], ['a', 'b']].entries()) {
      const result = await trace.captureObservedMessages(handle, `${SESSION}-${index}`, ids.map(observation))
      assert.equal(result.ok, true)
      assert.equal(result.receipt.identity, index === 3 ? 'stable-host' : 'positional')
    }
    await trace.captureProjection(handle, 'legacy-session', { messages: [{ role: 'user', parts: [trace.semanticText('legacy')] }] })
    const legacy = await trace.captureObservedMessages(handle, 'legacy-session', [observation('stable-id', 0)])
    assert.equal(legacy.receipt.identity, 'positional')
  })
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const trace = await import("../../../dist/Context/Trace/SemanticTraceSurface.js");


test('WHAT[semantic-trace-002] capture mapper copies text and reasoning semantics', () => {
  assert.deepEqual(trace.mapPart(trace.textPart('hello')), { kind: 'text', text: 'hello' })
  assert.deepEqual(trace.mapPart(trace.reasoningPart('considering')), { kind: 'reasoning', text: 'considering' })
})
test('WHAT[semantic-trace-002] capture mapper drops transport call identities', () => {
  assert.deepEqual(trace.mapPart(trace.toolCallPart('call-1', 'todowrite-for-engineer', '{}')), {
    kind: 'tool-call',
    name: 'todowrite-for-engineer',
    args: '{}',
  })
  assert.deepEqual(trace.mapPart(trace.toolResultPart('call-1', 'output')), {
    kind: 'tool-result',
    result: 'output',
  })
})
test('WHAT[semantic-trace-002] activity bookkeeping has no semantic part', () => {
  assert.equal(trace.mapPart(trace.activityPart('step-start')), undefined)
})
}

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

test('WHAT[semantic-trace-002] provider-run query returns copied semantic evidence', () => {
  const parts = trace.providerRunParts('provider-run-a', fixture())
  assert.deepEqual(parts.map((part) => part.cursor.sequence), [1, 2])
  assert.deepEqual(parts.map((part) => part.providerRun), ['provider-run-a', 'provider-run-a'])
})
test('WHAT[semantic-trace-002] exact provider tool and Host identities are queryable', () => {
  const projection = fixture()
  assert.deepEqual(trace.toolResultParts('provider-run-a', 'call-a', projection).map((part) => part.cursor.sequence), [2])
  assert.deepEqual(
    trace.toolPartsForHostIdentity('provider-run-a', 'call-a', 'part-a', projection).map((part) => part.cursor.sequence),
    [1],
  )
})
}

{
const { default: assert } = await import('node:assert/strict')
const trace = await import('../../../dist/Context/Trace/SemanticTraceSurface.js')
const { PlanningSurface } = await import('../../../dist/Mission/Planning/Surface.js')

test('WHAT[semantic-trace-002] later parts and reanchor copies cannot move a retired physical exchange into the current tenure', () => {
  let projection = trace.emptyProjection()
  for (const [sequence, messageId, generation, kind] of [
    [1, 'user-opening', 0, 'text'],
    [2, 'retired-exchange', 0, 'tool_call'],
    [5, 'retired-exchange', 0, 'tool_result'],
    [6, 'current-answer', 0, 'text'],
    [7, 'retired-exchange', 1, 'tool_result'],
    [8, 'current-answer', 1, 'text'],
    [9, 'user-followup', 1, 'text'],
  ]) {
    const appended = trace.appendPart(projection, {
      sequence,
      role: messageId.startsWith('user-') ? 'user' : 'assistant',
      provenance: `g:${generation}/msg:${messageId}/host-part:part-${sequence}`,
      turn: sequence,
      partIndex: 0,
      kind,
      toolName: kind.startsWith('tool_') ? 'todowrite-for-plan' : undefined,
      textRef: `blob-${sequence}`,
      textDigest: `digest-${sequence}`,
      body: `raw-${sequence}`,
      providerRun: messageId.startsWith('user-') ? undefined : messageId,
      toolCallId: kind.startsWith('tool_') ? 'retired-call' : undefined,
      hostToolPartId: kind.startsWith('tool_') ? 'retired-tool-part' : undefined,
    })
    assert.equal(appended.ok, true, appended.error)
    projection = appended.projection
  }

  const opening = {
    info: { id: 'user-opening', role: 'user' },
    parts: [{ type: 'text', text: 'Original requirement' }],
  }
  const retired = {
    info: { id: 'retired-exchange', role: 'assistant' },
    parts: [{
      id: 'retired-tool-part', type: 'tool', callID: 'retired-call', tool: 'todowrite-for-plan',
      state: { status: 'completed', input: { program: 'previous work' }, output: 'retired output' },
    }],
  }
  const answer = {
    info: { id: 'current-answer', role: 'assistant' },
    parts: [{ type: 'text', text: 'CURRENT_ANSWER' }],
  }
  const followup = {
    info: { id: 'user-followup', role: 'user' },
    parts: [{ type: 'text', text: 'CURRENT_FOLLOWUP' }],
  }
  const result = PlanningSurface.assembleTenureMessages(
    [opening, retired, answer, followup],
    {
      workId: 'trace-tenure', incumbencyId: 'inc-current', stage: 'S2',
      openingCursor: 4n, isFreshHandover: false,
    },
    trace.hostMessageFirstCursors(projection),
  )
  assert.deepEqual(
    result.messages.map((message) => message.info.id),
    ['user-opening', 'current-answer', 'user-followup'],
  )
  assert.equal(result.messages[1], answer)
  assert.equal(result.messages[2], followup)
  assert.deepEqual(result.messages.flatMap((message) => message.parts.map((part) => part.text)), [
    'Original requirement', 'CURRENT_ANSWER', 'CURRENT_FOLLOWUP',
  ])
})
}
