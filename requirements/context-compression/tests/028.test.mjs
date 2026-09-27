import assert from 'node:assert/strict'
import test from 'node:test'
import * as phaseWindow from '../../../dist/Context/Prefix/Surface.js'

// context-compression-028 pins the K-window formula. The table below is the clause's
// own worked example: with committed phases A1..AN and Bi the start of Ai's turn, the
// desired cutoff is Bj for j = max(1, N − K + 1).
const turnStarts = (n) => Array.from({ length: n }, (_, index) => index + 1)

const cutoffOf = (k, n) => {
  const decision = phaseWindow.desiredCutoff(k, turnStarts(n))
  return decision.kind === 'KeepFrom' ? decision.cutoffExclusive : null
}

test('WHAT[context-compression-028] N=0 yields no cutoff and no synthetic phase', () => {
  for (const k of [1, 2, 5]) {
    assert.equal(cutoffOf(k, 0), null, `K=${k} must not invent a zero-th assume call`)
  }
})

test('WHAT[context-compression-028] K=1 keeps only the current phase turn', () => {
  assert.equal(cutoffOf(1, 1), 1)
  assert.equal(cutoffOf(1, 2), 2)
  assert.equal(cutoffOf(1, 3), 3)
  assert.equal(cutoffOf(1, 4), 4)
})

test('WHAT[context-compression-028] K=2 keeps the previous phase as well', () => {
  assert.equal(cutoffOf(2, 1), 1)
  assert.equal(cutoffOf(2, 2), 1)
  assert.equal(cutoffOf(2, 3), 2)
  assert.equal(cutoffOf(2, 4), 3)
})

test('WHAT[context-compression-028] two commits inside one turn share their boundary', () => {
  // A2 and A3 land in the same semantic turn, so their Bi is equal: the window keeps
  // the whole turn instead of cutting a tool call away from its result.
  const decision = phaseWindow.desiredCutoff(2, [1, 4, 4, 7])
  assert.equal(decision.kind, 'KeepFrom')
  assert.equal(decision.cutoffExclusive, 4)
})

test('WHAT[context-compression-028] a non-positive K is refused', () => {
  assert.equal(phaseWindow.validateK(0).ok, false)
  assert.equal(phaseWindow.validateK(-1).ok, false)
  assert.match(phaseWindow.validateK(0).error, /positive integer/)
  assert.equal(phaseWindow.validateK(1).ok, true)
})

test('WHAT[context-compression-028] the frozen default is 2 and is a legal window', () => {
  assert.equal(phaseWindow.defaultK, 2, 'the owner opens with K = 2 unless something records otherwise')
  assert.equal(phaseWindow.validateK(phaseWindow.defaultK).ok, true)
})

test('WHAT[context-compression-028] the bounded window yields the same boundary as the full history', () => {
  // The projection only ever holds the last K commits, so the formula must agree when
  // it is handed that window instead of A1..AN: the oldest retained phase IS A_(N-K+1).
  for (const n of [1, 2, 3, 7]) {
    for (const k of [1, 2, 3]) {
      const allTurns = turnStarts(n)
      const window = allTurns.slice(-k)
      const decision = phaseWindow.desiredCutoffOfWindow(
        window.map((_, index) => `call-${index}`),
        window,
      )
      assert.equal(decision.kind, 'KeepFrom')
      assert.equal(
        decision.cutoffExclusive,
        cutoffOf(k, n),
        `K=${k} N=${n}: the window-sized list must select the same Bj`,
      )
    }
  }
})

test('WHAT[context-compression-028] the window keeps at most K commits in commit order', () => {
  let window = []
  for (const callId of ['a', 'b', 'c', 'd']) {
    window = phaseWindow.appendPhase(2, callId, window)
  }
  assert.deepEqual(window, ['c', 'd'], 'only the last K commits stay; order is commit order')

  assert.deepEqual(phaseWindow.appendPhase(2, 'a', null), ['a'])
})

test('WHAT[context-compression-028] a phase with no addressable turn proves no boundary', () => {
  // The oldest retained phase is the one the cutoff would land on. If its turn is gone
  // (a voided numbering), folding there would name a boundary the prefix cannot point
  // to, so the honest answer is "no cutoff" rather than the next phase's turn.
  const decision = phaseWindow.desiredCutoffOfWindow(['call-old', 'call-live'], [null, 42])
  assert.equal(decision.kind, 'NoPhases')

  const addressable = phaseWindow.desiredCutoffOfWindow(['call-old', 'call-live'], [7, 42])
  assert.equal(addressable.kind, 'KeepFrom')
  assert.equal(addressable.cutoffExclusive, 7, 'the oldest retained phase decides, not the newest')
})
{
  // The window's desire must be reachable the way the product actually runs: one user
  // message drives a long agent loop, so its own turn stays the oldest turn in the
  // request for the whole loop. The request bound is therefore the newest message the
  // request carries (the loop's own assistant/tool history), not the trailing user
  // message — otherwise every phase commit inside the loop folds nothing and the raw
  // history grows unbounded.
  const { default: assert } = await import('node:assert/strict')
  const { default: test } = await import('node:test')
  const { createHash } = await import('node:crypto')
  const { ofArray } = await import('../../../dist/fable_modules/fable-library-js.5.13.0/List.js')
  const { FSharpResult$2 } = await import('../../../dist/fable_modules/fable-library-js.5.13.0/Result.js')
  const xwire = await import('../../../dist/Context/Prefix/XWireSurface.js')
  const wire = await import('../../../dist/Context/Prefix/Wire.js')
  const wirePort = await import('../../../dist/Context/Prefix/WirePort.js')
  const trace = await import('../../../dist/Context/Trace/Projection.js')
  const blogProjection = await import('../../../dist/Context/Companion/Blogger/Projection.js')
  const identity = await import('../../../dist/Foundation/Identity.js')
  const authority = await import('../../../dist/Interaction/Authority/Model.js')
  const origin = await import('../../../dist/Interaction/Authority/Origin.js')
  const persona = await import('../../../dist/Participant/Persona/Identity.js')
  const scope = await import('../../../dist/OpenCode/Host/PluginRecoveryScope.js')
  const providerModel = await import('../../../dist/Participant/Provider/Projection/Model.js')
  const { PhaseWindow_PhaseCommitWindow: PhaseCommitWindow } = await import('../../../dist/Context/Prefix/PhaseWindow.js')

  const sha256Hex = (text) => createHash('sha256').update(text, 'utf8').digest('hex')
  const ok = (value) => new FSharpResult$2(0, [value])

  const sessionId = 'ses-k-window-loop'
  const openingText = '# Common Law\nopening task charter'
  const openingMessageId = 'msg-opening'
  const phaseCalls = ['call-A1', 'call-A2']
  const phaseTurns = { 'call-A1': 2, 'call-A2': 6 }
  const frontierTurn = 8

  const userMessage = (id, text) => ({
    info: { id, role: 'user', sessionID: sessionId },
    parts: [{ type: 'text', text }],
  })
  const assistantMessage = (id, text) => ({
    info: { id, role: 'assistant', sessionID: sessionId },
    parts: [{ type: 'text', text }],
  })

  // The request a loop step actually sends: the only role=user message is the one
  // that opened the loop; everything after it is assistant/tool history.
  const rawMessages = [userMessage(openingMessageId, openingText)]
  for (let turn = 1; turn <= frontierTurn; turn += 1) {
    const call = phaseCalls.find((candidate) => phaseTurns[candidate] === turn)
    rawMessages.push(assistantMessage(`msg-a${turn}`, call ? `assume ${call}` : `step ${turn}`))
  }

  // The typed projection the port materializes, and the same content in the shape
  // `coveredPrefixDigest` hashes: the coverage claim must agree with what production
  // recomputes from X's current prefix.
  const typedProjection = new providerModel.ProviderSemanticProjection(
    undefined,
    undefined,
    undefined,
    [],
    [],
    ofArray(
      rawMessages.map(
        (message) =>
          new providerModel.SemanticMessage(
            message.info.role,
            ofArray(message.parts.map((part) => new providerModel.SemanticPart(0, [part.text]))),
          ),
      ),
    ),
  )
  const jsProjection = {
    messages: rawMessages.map((message) => ({
      role: message.info.role,
      parts: message.parts.map((part) => ({ kind: 'text', text: part.text })),
    })),
  }
  const coverageCutoff = 2
  const claim = xwire.coveredPrefixDigest(jsProjection, coverageCutoff)

  // XTrace: the same request, captured. The two assume calls committed phases in
  // turns 2 and 6, so the K = 2 window keeps both and desires B1 = turn 2.
  const opening = trace.XTraceProjection_applyOpening(openingText, [], trace.XTraceProjection_empty)
  assert.equal(opening.tag, 0, 'opening must fold')
  let xTrace = opening.fields[0]
  let nextSequence = 1

  const appendPart = (role, messageId, turn, partIndex, kind, toolCallId) => {
    const sequence = nextSequence
    nextSequence += 1
    const folded = trace.XTraceProjection_applyPart(
      BigInt(sequence),
      role,
      `g:0/msg:${messageId}/host-part:prt-${sequence}`,
      turn,
      partIndex,
      kind,
      undefined,
      undefined,
      toolCallId === undefined ? undefined : identity.ToolCallIdModule_create(toolCallId),
      undefined,
      identity.BlobRefModule_create(`blobs/part-${sequence}`),
      identity.BlobDigestModule_create(sha256Hex(`part-${sequence}`)),
      xTrace,
    )
    assert.equal(folded.tag, 0, 'part must fold')
    xTrace = folded.fields[0]
  }

  appendPart('user', openingMessageId, 0, 0, 'text')
  for (let turn = 1; turn <= frontierTurn; turn += 1) {
    appendPart('assistant', `msg-a${turn}`, turn, 0, 'text')
    const call = phaseCalls.find((candidate) => phaseTurns[candidate] === turn)
    if (call) appendPart('assistant', `msg-a${turn}`, turn, 1, 'tool_result', call)
  }

  const frameBody = (n) => `Blog frame ${n}\ncovered turn ${n}`
  const frame = (n) =>
    new blogProjection.BlogFrame(
      blogProjection.BlogFrameKind.Entry,
      identity.BlobDigestModule_create(sha256Hex(frameBody(n))),
      identity.BlobRefModule_create(`blobs/frame-${n}`),
      BigInt(n - 1),
      BigInt(n),
      n,
    )

  // Stored newest-first, as the projection documents; coverage claims the whole
  // frozen material ends at `coverageCutoff`.
  const blogFrames = []
  for (let n = coverageCutoff; n >= 1; n -= 1) blogFrames.push(frame(n))

  const blogState = new blogProjection.BlogProjectionState(
    blogProjection.BlogProjection_empty.FrameEpochId,
    ofArray(blogFrames),
    new blogProjection.BlogCoverage(BigInt(coverageCutoff), coverageCutoff, claim, coverageCutoff),
  )

  const window = phaseWindow.appendPhase(phaseWindow.defaultK, phaseCalls[0], null)
  assert.deepEqual(
    phaseWindow.appendPhase(phaseWindow.defaultK, phaseCalls[1], window),
    phaseCalls,
    'K = 2 keeps both committed phases raw',
  )

  const state = new wirePort.WireSessionState(
    xTrace,
    blogState,
    undefined,
    new PhaseCommitWindow(ofArray(phaseCalls.map(identity.ToolCallIdModule_create))),
  )

  const resolved = persona.ParticipantIdentityModule_resolveAtRoot('engineer')
  assert.equal(resolved.tag, 0, 'identity must resolve')

  const profile = authority.createAuthorityExecutionProfile(
    identity.SessionIdModule_create(sessionId),
    identity.LogicalRunIdModule_create('run-k-window'),
    identity.AuthorityRootUserMessageIdModule_create(openingMessageId),
    origin.PromptRootAuthorityKind.HumanRoot,
    resolved.fields[0],
  )
  assert.equal(profile.tag, 0, 'authority must build')

  const acceptedOrigin = new origin.PromptOrigin(0, [origin.PromptRootAuthorityKind.HumanRoot])
  const view = new wirePort.WireSnapshotView(state, false, profile.fields[0], undefined, () => acceptedOrigin)

  const written = new Map()
  let frozenPlan = null
  const attempts = {
    TryAttemptPlan: () => undefined,
    TryBindAttemptPlan: () => undefined,
    ConsumeAttemptPlan: () => undefined,
    TryPendingAttemptPlan: () => undefined,
    FreezePendingAttemptPlan: (_session, _physical, plan) => {
      frozenPlan = plan
      return new scope.PendingAttemptPlanAdmission(0, [plan])
    },
  }

  const port = new wirePort.WireJournalPort(
    () => view,
    (blobRef) => {
      const ref = identity.BlobRefModule_value(blobRef)
      if (written.has(ref)) return Promise.resolve(ok(written.get(ref)))
      const match = /^blobs\/frame-(\d+)$/.exec(ref)
      return Promise.resolve(ok(match === null ? '' : frameBody(Number(match[1]))))
    },
    (content) => {
      const ref = `blobs/frozen-${written.size}`
      written.set(ref, content)
      return Promise.resolve(
        ok(
          new wirePort.WireBlobRecord(
            identity.BlobRefModule_create(ref),
            identity.BlobDigestModule_create(sha256Hex(content)),
          ),
        ),
      )
    },
    () => Promise.resolve(ok(typedProjection)),
    () => Promise.resolve(ok(undefined)),
    () => Promise.resolve(ok(undefined)),
  )

  const output = { messages: structuredClone(rawMessages) }

  test('WHAT[context-compression-028] the window folds inside a loop that carries no new user message', async () => {
    await wire.XWire_applyTransform(() => false, undefined, port, attempts, output)

    assert.ok(frozenPlan, 'the phase window must freeze an attempt plan inside the loop')
    assert.equal(
      frozenPlan.ProjectionChoice.cases()[frozenPlan.ProjectionChoice.tag],
      'UsePrefixProbe',
      'the plan must carry a prefix probe',
    )
    assert.equal(
      frozenPlan.ProjectionChoice.fields[0].Candidate.CutoffExclusive,
      phaseTurns['call-A1'],
      'the fold lands on B1, the oldest phase the K = 2 window keeps raw',
    )
    assert.equal(written.size, 1, 'the frozen record prefix must be materialized once')
    assert.ok(
      written.get('blobs/frozen-0').includes('Blog frame 1'),
      'the frozen material must be the coverable frame subset',
    )

    const messageIds = output.messages.map((message) => message.info.id)
    const texts = output.messages.map((message) => (message.parts ?? []).map((part) => part.text ?? '').join(''))

    assert.equal(messageIds[0], openingMessageId, 'the raw Opening stays the first message')
    assert.ok(
      texts.some((text) => text.includes('Blog frame 1')),
      'the covered prefix must come back as the LWR memory, not as raw history',
    )
    assert.equal(messageIds.includes('msg-a1'), false, 'a turn before B1 is folded')
    assert.equal(messageIds.includes('msg-a2'), true, 'B1 itself stays raw inside the window')
  })
}
