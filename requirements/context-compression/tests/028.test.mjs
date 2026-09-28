import assert from 'node:assert/strict'
import test from 'node:test'
import { createHash } from 'node:crypto'
import * as phaseWindow from '../../../dist/Context/Prefix/Surface.js'
import * as xwire from '../../../dist/Context/Prefix/XWireSurface.js'

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
  const sha256Hex = (text) => createHash('sha256').update(text, 'utf8').digest('hex')

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

  // Both the coverage claim and the port's projection use this same content.
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
  const traceParts = []
  let nextSequence = 1

  const appendPart = (role, messageId, turn, partIndex, kind, toolCallId) => {
    const sequence = nextSequence
    nextSequence += 1
    traceParts.push({
      sequence,
      role,
      provenance: `g:0/msg:${messageId}/host-part:prt-${sequence}`,
      turn,
      partIndex,
      kind,
      toolCallId,
      ref: `blobs/part-${sequence}`,
      digest: sha256Hex(`part-${sequence}`),
    })
  }

  appendPart('user', openingMessageId, 0, 0, 'text')
  for (let turn = 1; turn <= frontierTurn; turn += 1) {
    appendPart('assistant', `msg-a${turn}`, turn, 0, 'text')
    const call = phaseCalls.find((candidate) => phaseTurns[candidate] === turn)
    if (call) appendPart('assistant', `msg-a${turn}`, turn, 1, 'tool_result', call)
  }

  const frameBody = (n) => `Blog frame ${n}\ncovered turn ${n}`
  const frame = (n) => ({
    digest: sha256Hex(frameBody(n)),
    ref: `blobs/frame-${n}`,
    coveredFrom: n - 1,
    coveredThrough: n,
    cutoff: n,
  })

  // Stored newest-first, as the projection documents; coverage claims the whole
  // frozen material ends at `coverageCutoff`.
  const blogFrames = []
  for (let n = coverageCutoff; n >= 1; n -= 1) blogFrames.push(frame(n))

  const window = phaseWindow.appendPhase(phaseWindow.defaultK, phaseCalls[0], null)
  assert.deepEqual(
    phaseWindow.appendPhase(phaseWindow.defaultK, phaseCalls[1], window),
    phaseCalls,
    'K = 2 keeps both committed phases raw',
  )

  test('WHAT[context-compression-028] the window folds inside a loop that carries no new user message', async () => {
    // The Surface fails if a real trace fold or authority construction is rejected.
    const output = await xwire.applyPhaseWindow({
      session: sessionId,
      openingText,
      openingMessageId,
      logicalRun: 'run-k-window',
      agent: 'engineer',
      messages: structuredClone(rawMessages),
      projection: jsProjection,
      traceParts,
      phaseCallIds: phaseCalls,
      frames: blogFrames,
      coverage: {
        ingestedThrough: coverageCutoff,
        cutoff: coverageCutoff,
        digest: claim,
        frameCount: coverageCutoff,
      },
      blobs: blogFrames.map((frame) => ({ ref: frame.ref, body: frameBody(frame.cutoff) })),
    })

    assert.ok(output.plan, 'the phase window must freeze an attempt plan inside the loop')
    assert.equal(
      output.plan.choice,
      'UsePrefixProbe',
      'the plan must carry a prefix probe',
    )
    assert.equal(
      output.plan.probe.cutoff,
      phaseTurns['call-A1'],
      'the fold lands on B1, the oldest phase the K = 2 window keeps raw',
    )
    assert.equal(output.writes.length, 1, 'the frozen record prefix must be materialized once')
    assert.ok(
      output.writes.find((blob) => blob.ref === 'blobs/frozen-0').body.includes('Blog frame 1'),
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
