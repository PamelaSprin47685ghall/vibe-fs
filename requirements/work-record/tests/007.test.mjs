import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { mkdtempSync, rmSync } = await import("node:fs");
const { tmpdir } = await import("node:os");
const { join } = await import("node:path");
const journal = await import("../../../dist/Persistence/Journal/Surface.js");
const workRecord = await import("../../../dist/Mission/WorkRecord/Surface.js");

const SEM = 'ses_bounded_lwr'
const openJournal = async () => {
  const dir = mkdtempSync(join(tmpdir(), 'lwr-bounded-'))
  const created = await journal.JournalSurface_boot(dir, 'rt_bounded_lwr', 4242, '2026-01-01T00:00:00Z')
  assert.equal(created.ok, true, created.ok ? '' : JSON.stringify(created.error))
  return {
    handle: created.journal,
    close: () => {
      journal.JournalSurface_dispose(created.journal)
      rmSync(dir, { recursive: true, force: true })
    },
  }
}
const withJournal = async (fn) => {
  const opened = await openJournal()
  try {
    return await fn(opened.handle)
  } finally {
    opened.close()
  }
}
const lastSequence = (trace) => trace.currentHeadSequence - 1
const commitY = async (handle, { from, to, body, n }) => {
  const written = await journal.JournalSurface_writePayload(handle, body)
  assert.equal(written.ok, true, written.ok ? '' : JSON.stringify(written.error))
  const run = `msg_y${n}`
  const result = await workRecord.appendBlogObservation(handle, SEM, run, {
    bloggerSessionId: 'ses_blogger',
    requestId: `req-y${n}`,
    frameEpoch: 0,
    previousIngestedThroughSequence: from,
    nextIngestedThroughSequence: to,
    previousCoverableTurnCutoffExclusive: 0,
    nextCoverableTurnCutoffExclusive: 0,
    nextCoveredPrefixDigest: '',
    textRef: written.blobRef,
    textDigest: written.blobDigest,
    toolCallIds: [],
    tipRuleId: `tip-y${n}`,
    fieldNameAtCommit: `field-y${n}`,
    observedPrefixEpoch: 0,
  })
  assert.equal(result.ok, true, result.ok ? '' : JSON.stringify(result.error))
}
const seedTwoInvocations = async (handle) => {
  await workRecord.captureOpening(handle, SEM, 'first charge', [])

  const inv1 = await workRecord.captureProjection(handle, SEM, {
    messages: [
      { role: 'user', parts: [{ kind: 'text', text: 'first charge' }] },
      { role: 'assistant', parts: [{ kind: 'text', text: 'inv1 work' }] },
    ],
  })
  const inv1Through = lastSequence(inv1)
  // S1 = XTrace.head after inv1 (one-past last part). S2 is likewise one-past inv2.
  const s1 = inv1Through + 1
  assert.equal(inv1Through, 2, `inv1 last part must be 2, got ${inv1Through}`)
  assert.equal(s1, 3, `inv2 StartInclusive must be one-past inv1 (3), got ${s1}`)
  await commitY(handle, { from: 0, to: inv1Through, body: 'PRIOR_Y_INV1', n: 1 })

  const inv2 = await workRecord.captureProjection(handle, SEM, {
    messages: [
      { role: 'user', parts: [{ kind: 'text', text: 'first charge' }] },
      { role: 'assistant', parts: [{ kind: 'text', text: 'inv1 work' }] },
      { role: 'user', parts: [{ kind: 'text', text: 'second charge' }] },
      { role: 'assistant', parts: [{ kind: 'text', text: 'inv2 work' }] },
    ],
  })
  const inv2Through = lastSequence(inv2)
  const s2 = inv2Through + 1
  assert.equal(inv2Through, 4, `inv2 last part must be 4, got ${inv2Through}`)
  assert.equal(s2, 5, `EndExclusive must be one-past last part (5), got ${s2}`)
  assert.equal(inv1Through, s1 - 1, 'inv1 Y CoveredThroughSequence must equal inv2 StartInclusive - 1')
  return { s1, s2, inv1Through, inv2Through }
}

test('WHAT[work-record-007] child_to_parent_run_bounded_LWR_omits_caller_charge', async () => {
  await withJournal(async (journal) => {
    await workRecord.captureOpening(journal, SEM, 'assigned task', [])
    const captured = await workRecord.captureProjection(journal, SEM, {
      messages: [
        { role: 'user', parts: [{ kind: 'text', text: 'assigned task' }] },
        { role: 'assistant', parts: [{ kind: 'text', text: 'did child work' }] },
      ],
    })
    assert.equal(captured.currentHeadSequence, 3)

    const bounded = await workRecord.lifecycleWorkRecordBounded(journal, SEM, {
      StartInclusive: { Sequence: 0 },
      EndExclusive: { Sequence: 3 },
      ProviderRun: 'run-child',
    })

    assert.equal(typeof bounded, 'string')
    assert.doesNotMatch(bounded, /assigned task/)
    assert.match(bounded, /did child work/)
  })
})
}

{
const { test } = await import("node:test");
const { default: assert } = await import("node:assert/strict");
const todo = await import("../../../dist/Mission/Obligation/Todo/MagicTodoSemanticSurface.js");
const workRecord = await import("../../../dist/Mission/WorkRecord/OpeningSemanticSurface.js");
const traceOwner = await import("../../../dist/Context/Trace/SemanticTraceSurface.js");

const xTrace = {
  item: traceOwner.item,
  text: traceOwner.textPart,
  reasoning: traceOwner.reasoningPart,
  toolCall: (name, args) => traceOwner.toolCallPart('fixture-call', name, args),
  toolResult: (result) => traceOwner.toolResultPart('fixture-call', result),
}
const opening = (assignment, requirements = []) => workRecord.opening(assignment, requirements, '')
const materialize = (
  openingValue,
  frames,
  trace,
  coverage,
  openingEnd = { Sequence: 0 },
  includeOpening = true,
) => {
  const gapStart = Math.max(Number(coverage.Sequence), Number(openingEnd.Sequence))
  const gap = traceOwner.render(traceOwner.forWorkRecord(traceOwner.sliceFrom({ sequence: gapStart }, trace)))
  return workRecord.materialize(openingValue, frames, gap, includeOpening)
}
const OPENING_END = { Sequence: 1 }

test('WHAT[work-record-007] LWR_parent_to_child_includes_opening', () => {
  // EXEC-006: parent → child background keeps Opening (includeOpening default true).
  const rendered = materialize(
    opening('assigned task'),
    ['did work'],
    [],
    { Sequence: 0 },
    OPENING_END,
    true,
  )

  assert.equal(rendered.includes('Opening'), true)
  assert.equal(rendered.includes('Opening task'), false)
  assert.equal(rendered.includes('assigned task'), true)
  assert.match(rendered, /Chronicle\ndid work/)
})
test('WHAT[work-record-007] LWR_child_to_parent_omits_opening', () => {
  // EXEC-006: child → parent join omits Opening — assigner already knows the task.
  const rendered = materialize(
    opening('assigned task'),
    ['did work'],
    [xTrace.item({ sequence: 1, role: 'assistant', part: xTrace.text('Final summary') })],
    { Sequence: 0 },
    OPENING_END,
    false,
  )

  assert.equal(rendered.startsWith('Opening\n'), false)
  assert.equal(rendered.includes('assigned task'), false)
  assert.match(rendered, /Chronicle\ndid work/)
  assert.match(rendered, /Recent work/)
  assert.match(rendered, /Final summary/)
  assert.equal(rendered.includes('Closing report'), false)
})
}
