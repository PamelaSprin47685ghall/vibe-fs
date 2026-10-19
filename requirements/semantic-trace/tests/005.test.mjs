import assert from 'node:assert/strict'
import test from 'node:test'
import { Trace } from 'wanxiangli/trace'
import { integrationTest } from '../../verification-system/tests/support/tier-gate.mjs'
import * as trace from '../../../dist/Context/Trace/SemanticTraceSurface.js'

const unwrap = (result) => {
  assert.equal(result.ok, true, result.ok ? '' : result.error)
  return result.projection
}

const partDescriptor = (sequence, overrides = {}) => ({
  sequence,
  role: 'assistant',
  provenance: `g:0/msg:msg-${sequence}/host-part:part-${sequence}`,
  turn: sequence - 1,
  partIndex: 0,
  kind: 'text',
  textRef: `blob-${sequence}`,
  textDigest: `digest-${sequence}`,
  body: `raw-${sequence}`,
  providerRun: `run-${sequence}`,
  ...overrides,
})

test('WHAT[semantic-trace-005] canonical render is deterministic and omits provenance', () => {
  const items = [
    { cursor: trace.cursor(0), role: 'user', provenance: 'run-secret/msg-secret', part: trace.semanticText('Fix it.') },
    { cursor: trace.cursor(1), role: 'assistant', provenance: 'run-secret/msg-secret', part: trace.semanticReasoning('considered') },
    { cursor: trace.cursor(2), role: 'assistant', provenance: 'run-secret/msg-secret', part: trace.semanticToolCall('todowrite-for-engineer', '{}') },
  ]
  const first = trace.render(items)
  assert.equal(trace.render(items), first)
  assert.equal(trace.render(items.map((item) => ({ ...item, provenance: 'other-transport' }))), first)
  assert.notEqual(trace.render([{ ...items[0], part: trace.semanticText('Different task') }]), trace.render([items[0]]))
  assert.match(first, /user: Fix it\./)
  assert.match(first, /\[tool call\] todowrite-for-engineer \{\}/)
  assert.equal(first.includes('secret'), false)
  assert.equal(trace.render([]), '')
})

test('WHAT[semantic-trace-005] composition bridge maps XTraceRange [start, end) to wanxiangli/trace (after, through] cuts and preserves digest chain', () => {
  let projection = trace.emptyProjection()
  projection = unwrap(trace.appendOpening(projection, 'ship cut-03', ['req-1', 'req-2']))
  projection = unwrap(trace.appendPart(projection, partDescriptor(1, { role: 'user', kind: 'text', providerRun: undefined })))
  projection = unwrap(trace.appendPart(projection, partDescriptor(2, { kind: 'reasoning' })))
  projection = unwrap(trace.appendPart(projection, partDescriptor(3, { kind: 'tool_call', toolName: 'todowrite-for-engineer' })))
  projection = unwrap(trace.appendTerminal(projection, {
    textRef: 'blob-term',
    textDigest: 'digest-term',
    body: 'formal terminal',
    providerRun: 'run-3',
  }))

  const records = trace.traceRecords(projection)
  assert.equal(Array.isArray(records), true)
  assert.equal(records.length > 0, true)

  const headCut = trace.traceHeadCut(projection)
  const restored = new Trace(headCut.traceId, records)
  assert.deepEqual(restored.head(), headCut)

  const subRange = trace.createRange(trace.cursor(2), trace.cursor(4))
  const interval = trace.traceIntervalOfRange(subRange, projection)
  assert.equal(interval.after.cursor < interval.through.cursor, true)
  assert.deepEqual(interval.after, trace.traceCutAt(trace.cursor(1), projection))
  assert.deepEqual(interval.through, trace.traceCutAt(trace.cursor(3), projection))

  const slicedParts = trace.slice(subRange, projection)
  assert.deepEqual(slicedParts.map((p) => p.cursor.sequence), [2, 3])

  const rawObservations = restored.read(interval.after, interval.through)
  assert.equal(rawObservations.length > 0, true)

  const emptyRange = trace.createRange(trace.cursor(2), trace.cursor(2))
  const emptyInterval = trace.traceIntervalOfRange(emptyRange, projection)
  assert.deepEqual(emptyInterval.after, emptyInterval.through)
  assert.deepEqual(restored.read(emptyInterval.after, emptyInterval.through), [])

  const afterCoverage = trace.traceIntervalAfterCoverage(trace.cursor(1), projection)
  assert.deepEqual(afterCoverage.after, trace.traceCutAt(trace.cursor(1), projection))
  assert.deepEqual(afterCoverage.through, trace.traceCutAt(trace.cursor(3), projection))

  assert.throws(() => restored.read(interval.through, interval.after), /Trace interval is reversed/)
  assert.throws(
    () => restored.read({ ...interval.after, traceId: 'foreign-trace' }, interval.through),
    /Foreign trace cut/,
  )
  const tamperedRecord = records[0].replace(/"digest":"[0-9a-f]{64}"/, '"digest":"' + '0'.repeat(64) + '"')
  assert.throws(() => new Trace(headCut.traceId, [tamperedRecord, ...records.slice(1)]), /Trace/)
})

test('WHAT[semantic-trace-005] durable originals preserve reasoning, tool results and raw bytes without temporary recapture', async () => {
  const userRaw = '用户原话\r\n\uFEFF\u0000keep-exact-bytes'
  const largeToolResult = 'L'.repeat(600 * 1024) + '\r\n\u0000tail'
  const items = [
    { cursor: trace.cursor(1), role: 'user', provenance: 'g:0/msg:u1/part:0', part: trace.semanticText(userRaw) },
    { cursor: trace.cursor(2), role: 'assistant', provenance: 'g:0/msg:a1/part:0', part: trace.semanticReasoning('internal chain of thought') },
    { cursor: trace.cursor(3), role: 'assistant', provenance: 'g:0/msg:a1/part:1', part: trace.semanticToolCall('todowrite-for-engineer', '{"program":"await this.cat(\'a.txt\')"}') },
    { cursor: trace.cursor(4), role: 'assistant', provenance: 'g:0/msg:a1/part:2', part: trace.semanticToolResult(largeToolResult) },
    { cursor: trace.cursor(5), role: 'assistant', provenance: 'g:0/msg:a1/part:3', part: trace.semanticMedia('image/png', 'sha256-media') },
    { cursor: trace.cursor(6), role: 'assistant', provenance: 'g:0/msg:a2/part:0', part: trace.semanticText('Final prose statement.') },
  ]

  const { withReopenableJournal } = await import('./support/journal.mjs')
  await withReopenableJournal(async (handle, reopen) => {
    const messages = [
      { role: 'user', parts: [trace.semanticText(userRaw)] },
      { role: 'assistant', parts: items.slice(1, 5).map(item => item.part) },
      { role: 'assistant', parts: [trace.semanticText('Final prose statement.')] },
    ]
    const captured = await trace.captureProjection(handle, 'originals-512k', { messages })
    assert.equal(captured.ok, true, JSON.stringify(captured.error))
    const projection = trace.snapshot(handle, 'originals-512k')
    const cut = trace.traceHeadCut(projection)
    const restored = new Trace(cut.traceId, trace.traceRecords(projection))
    const window = restored.materialize(restored.cut(0), cut)
    assert.equal(window.materials.length, 6)
    assert.equal(window.materials[0].content.text, userRaw)
    assert.equal(window.materials[1].kind, 'reasoning')
    assert.equal(window.materials[2].toolName, 'todowrite-for-engineer')
    assert.equal(window.materials[3].content.text, largeToolResult)
    assert.equal(window.materials[4].kind, 'media-omitted')
    assert.equal(window.statement, null, 'confirmed material must not invent message completion')
    assert.equal(restored.inspect().runs, 0)
    const before = await trace.currentProjection(handle, 'originals-512k')
    const cold = await reopen()
    assert.deepEqual(await trace.currentProjection(cold, 'originals-512k'), before)
    assert.deepEqual(trace.traceRecords(trace.snapshot(cold, 'originals-512k')), restored.records())
  })

  const roundTripped = trace.sliceBetween(trace.cursor(1), trace.cursor(7), items)
  assert.equal(roundTripped.length, 6)
  assert.deepEqual(roundTripped[0].part, { kind: 'text', text: userRaw })
  assert.deepEqual(roundTripped[1].part, { kind: 'reasoning', text: 'internal chain of thought' })
  assert.deepEqual(roundTripped[2].part, { kind: 'tool-call', name: 'todowrite-for-engineer', args: '{"program":"await this.cat(\'a.txt\')"}' })
  assert.equal(roundTripped[3].part.kind, 'tool-result')
  assert.equal(roundTripped[3].part.result.length, largeToolResult.length)
  assert.equal(roundTripped[3].part.result, largeToolResult)
  assert.deepEqual(roundTripped[4].part, { kind: 'media', mediaType: 'image/png', digest: 'sha256-media' })
  assert.deepEqual(roundTripped[5].part, { kind: 'text', text: 'Final prose statement.' })

  const middleSlice = trace.sliceBetween(trace.cursor(2), trace.cursor(5), items)
  assert.deepEqual(middleSlice.map((item) => item.cursor.sequence), [2, 3, 4])
})

test('WHAT[semantic-trace-005] opaque projection and wanxiangli/trace reject state mutation, reentrancy and conflicting replay', () => {
  let projection = unwrap(trace.appendOpening(trace.emptyProjection(), 'initial task', ['r1']))
  projection = unwrap(trace.appendPart(projection, partDescriptor(1)))

  const openingCopy = trace.openingEvidence(projection)
  openingCopy.assignmentText = 'mutated outside'
  openingCopy.authoritativeRequirements.push('mutated-req')
  assert.deepEqual(trace.openingEvidence(projection), {
    assignmentText: 'initial task',
    authoritativeRequirements: ['r1'],
    constitutiveBody: '',
  })

  const partsCopy = trace.orderedSemanticParts(projection)
  partsCopy[0].role = 'user'
  partsCopy.push(partDescriptor(99))
  assert.equal(trace.orderedSemanticParts(projection).length, 1)
  assert.equal(trace.orderedSemanticParts(projection)[0].role, 'assistant')

  const rawTrace = new Trace('encapsulation-guard')
  const bytesA = new TextEncoder().encode('alpha')
  const bytesB = new TextEncoder().encode('beta')
  const first = rawTrace.capture({ type: 'Opening', bytes: bytesA })
  const replayed = rawTrace.capture({ type: 'Opening', bytes: bytesA })
  assert.equal(first.status, 'Appended')
  assert.equal(replayed.status, 'Replayed')
  assert.equal(replayed.cursor, first.cursor)
  assert.throws(
    () => rawTrace.capture({ type: 'Opening', bytes: bytesB }),
    /Observation identity conflict/,
  )

  // 访问器拒绝与 Proxy 重入是不同的公开边界，分别验证。
  const reentrantPayload = {}
  Object.defineProperty(reentrantPayload, 'type', {
    enumerable: true,
    get() {
      rawTrace.inspect()
      return 'RunStarted'
    },
  })
  assert.throws(
    () => rawTrace.capture(reentrantPayload),
    /Missing field or accessor: type/,
  )
  let entered = false
  const guardedPayload = new Proxy({ type: 'Opening', bytes: bytesA }, {
    getOwnPropertyDescriptor(target, key) {
      if (key === 'type') {
        entered = true
        assert.throws(() => rawTrace.inspect(), /Trace does not allow reentrant messages/)
        assert.throws(() => rawTrace.capture({ type: 'Opening', bytes: bytesB }), /Trace does not allow reentrant messages/)
      }
      return Reflect.getOwnPropertyDescriptor(target, key)
    },
  })
  assert.equal(rawTrace.capture(guardedPayload).status, 'Replayed')
  assert.equal(entered, true)
  assert.equal(rawTrace.inspect().cursor, first.cursor)
})

test('WHAT[semantic-trace-005] long append sequences preserve authenticated chronological records and range boundaries', () => {
  let projection = unwrap(trace.appendOpening(trace.emptyProjection(), 'bulk append', []))
  const N = 300
  for (let i = 1; i <= N; i++) {
    projection = unwrap(trace.appendPart(projection, partDescriptor(i)))
  }

  const records = trace.traceRecords(projection)
  // Opening + one RunStarted/MessageObserved/PartConfirmed/MessageFinished/RunFinished
  // run per part (assistant role carries a run). Assert records are non-empty and
  // the trace reconstructs to the same head — the exact ordering guarantee.
  assert.equal(records.length > N, true)

  const headCut = trace.traceHeadCut(projection)
  const restored = new Trace(headCut.traceId, records)
  assert.deepEqual(restored.head(), headCut)

  // Chronological part cursors must be exactly 1..N in order.
  const parts = trace.orderedSemanticParts(projection)
  assert.deepEqual(
    parts.map((p) => p.cursor.sequence),
    Array.from({ length: N }, (_, i) => i + 1),
  )

  // A later slice must resolve against the same chronological cuts.
  const midRange = trace.createRange(trace.cursor(100), trace.cursor(200))
  const sliced = trace.slice(midRange, projection)
  assert.deepEqual(
    sliced.map((p) => p.cursor.sequence),
    Array.from({ length: 100 }, (_, i) => i + 100),
  )
})

test('WHAT[semantic-trace-005] appending two branches preserves the old projection and both authenticated histories', () => {
  const base = unwrap(trace.appendPart(
    unwrap(trace.appendOpening(trace.emptyProjection(), 'branch proof', [])),
    partDescriptor(1),
  ))
  const baseRecords = trace.traceRecords(base)
  const baseCut = trace.traceHeadCut(base)
  const left = unwrap(trace.appendPart(base, partDescriptor(2, { textRef: 'left-ref', textDigest: 'left-digest', body: 'left-body' })))
  const leftRecords = trace.traceRecords(left)
  const leftCut = trace.traceHeadCut(left)
  const right = unwrap(trace.appendPart(base, partDescriptor(2, { textRef: 'right-ref', textDigest: 'right-digest', body: 'right-body' })))
  const rightRecords = trace.traceRecords(right)
  const rightCut = trace.traceHeadCut(right)
  unwrap(trace.appendPart(left, partDescriptor(3)))

  assert.deepEqual(trace.traceRecords(base), baseRecords)
  assert.deepEqual(trace.traceHeadCut(base), baseCut)
  assert.deepEqual(trace.traceRecords(left), leftRecords)
  assert.deepEqual(trace.traceHeadCut(left), leftCut)
  assert.deepEqual(trace.traceRecords(right), rightRecords)
  assert.deepEqual(trace.traceHeadCut(right), rightCut)
  assert.notDeepEqual(leftCut, rightCut)
  assert.deepEqual(new Trace(leftCut.traceId, leftRecords).head(), leftCut)
  assert.deepEqual(new Trace(rightCut.traceId, rightRecords).head(), rightCut)
})

integrationTest('WHAT[semantic-trace-005] independently compile a consumer and reject access to opaque trace state, cursor and append refs', async () => {
  const { cpSync, mkdirSync, mkdtempSync, readFileSync, rmSync, statSync, writeFileSync } = await import('node:fs')
  const { createHash } = await import('node:crypto')
  const { dirname: dirnameOf, join: joinPath, relative: relativeOf, resolve: resolveRoot } = await import('node:path')
  const { tmpdir } = await import('node:os')
  const { compileOwnerProject, planOwnerCompile } = await import('../../../scripts/lib/owner-compile.mjs')

  const ROOT = resolveRoot(import.meta.dirname, '../../..')
  const SOURCE_ROOT = joinPath(ROOT, 'src/Wanxiangshu')
  const TRACE_MODEL_SHARD = joinPath(
    SOURCE_ROOT,
    'Wanxiangshu.Owner.semantic-trace.context-trace-model.fsproj',
  )

  const POSITIVE_PROBE = [
    'namespace Wanxiangshu.Probe',
    '',
    'open Wanxiangshu.Context.Trace',
    '',
    'module LegalTraceConsumer =',
    '    let startCursor = XTraceCursor.originCursor',
    '    let nextCursor = XTraceCursor.nextCursor startCursor',
    '    let validRange = XTraceRange.create startCursor nextCursor',
    '    let emptyState = XTraceProjection.empty',
    '    let copiedParts = XTraceProjection.orderedSemanticParts emptyState',
    '    let currentHead = XTraceProjection.headCursor emptyState',
    '    let currentCut = XTraceProjection.traceHeadCut emptyState',
    '',
  ].join('\n')

  const NEGATIVE_STATE_AND_CURSOR_PROBE = [
    'namespace Wanxiangshu.Probe',
    '',
    'open Wanxiangshu.Context.Trace',
    '',
    'module IllegalPrivateStateConsumer =',
    '    let forgedCursor = XTraceCursor.XTraceCursor 42L',
    '    let leakedParts (state: XTraceProjectionState) = state.Parts',
    '',
  ].join('\n')

  const NEGATIVE_RANGE_CONSTRUCTOR_PROBE = [
    'namespace Wanxiangshu.Probe',
    '',
    'open Wanxiangshu.Context.Trace',
    '',
    'module IllegalPrivateRangeConsumer =',
    '    let forgedRange =',
    '        XTraceRange.XTraceRange(XTraceCursor.originCursor, XTraceCursor.originCursor)',
    '',
  ].join('\n')

  const isolate = (plan, probeSource) => {
    const iso = mkdtempSync(joinPath(tmpdir(), 'wxs-trace005-iso-'))
    const items = plan.compileItems.map((item) => {
      const dest = joinPath(iso, 'src', relativeOf(SOURCE_ROOT, item))
      mkdirSync(dirnameOf(dest), { recursive: true })
      cpSync(item, dest)
      return dest
    })
    const probe = joinPath(iso, 'probe-trace-consumer.fs')
    writeFileSync(probe, probeSource)
    return { plan: { ...plan, compileItems: [...items, probe] }, iso }
  }

  const digestOf = (file) => ({
    hash: createHash('sha256').update(readFileSync(file)).digest('hex'),
    mtime: statSync(file).mtimeMs,
  })

  const realCursor = joinPath(SOURCE_ROOT, 'Context/Trace/Cursor.fs')
  const realProjection = joinPath(SOURCE_ROOT, 'Context/Trace/Projection.fs')
  const before = [digestOf(realCursor), digestOf(realProjection)]

  const scratch = mkdtempSync(joinPath(tmpdir(), 'wxs-trace005-compile-'))
  const positiveIso = isolate(planOwnerCompile({ projectPath: TRACE_MODEL_SHARD }), POSITIVE_PROBE)
  const negativeStateIso = isolate(
    planOwnerCompile({ projectPath: TRACE_MODEL_SHARD }),
    NEGATIVE_STATE_AND_CURSOR_PROBE,
  )
  const negativeRangeIso = isolate(
    planOwnerCompile({ projectPath: TRACE_MODEL_SHARD }),
    NEGATIVE_RANGE_CONSTRUCTOR_PROBE,
  )

  try {
    const positive = await compileOwnerProject({
      projectPath: TRACE_MODEL_SHARD,
      scratchRoot: scratch,
      stdio: 'pipe',
      compilePlan: positiveIso.plan,
    })
    assert.equal(
      positive.ok,
      true,
      'legal public trace consumer must compile: ' + String(positive.stdout ?? '').slice(-400),
    )

    const negativeState = await compileOwnerProject({
      projectPath: TRACE_MODEL_SHARD,
      scratchRoot: scratch,
      stdio: 'pipe',
      compilePlan: negativeStateIso.plan,
    })
    assert.equal(
      negativeState.ok,
      false,
      'foreign consumer must not construct private XTraceCursor or read private XTraceProjectionState fields',
    )
    assert.match(
      String(negativeState.stdout ?? '') + String(negativeState.stderr ?? ''),
      /not accessible|not defined|private|cannot be accessed|FS0039|FS0491|FS1093/i,
    )

    const negativeRange = await compileOwnerProject({
      projectPath: TRACE_MODEL_SHARD,
      scratchRoot: scratch,
      stdio: 'pipe',
      compilePlan: negativeRangeIso.plan,
    })
    assert.equal(
      negativeRange.ok,
      false,
      'foreign consumer must not bypass XTraceRange.create to construct private XTraceRange',
    )
    assert.match(
      String(negativeRange.stdout ?? '') + String(negativeRange.stderr ?? ''),
      /not accessible|not defined|private|cannot be accessed|FS0039|FS0491|FS1093/i,
    )

    const after = [digestOf(realCursor), digestOf(realProjection)]
    assert.deepEqual(after, before, 'the real Context/Trace sources are untouched')
  } finally {
    rmSync(scratch, { recursive: true, force: true })
    rmSync(positiveIso.iso, { recursive: true, force: true })
    rmSync(negativeStateIso.iso, { recursive: true, force: true })
    rmSync(negativeRangeIso.iso, { recursive: true, force: true })
  }
})
