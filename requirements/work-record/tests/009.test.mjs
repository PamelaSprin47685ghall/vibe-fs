import { test } from 'node:test'
import assert from 'node:assert/strict'
import * as workRecord from '../../../dist/Mission/WorkRecord/OpeningSemanticSurface.js'
import * as traceOwner from '../../../dist/Context/Trace/SemanticTraceSurface.js'
import { record, sectionsOf, withReopenableJournal } from './support/record.mjs'

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

test('WHAT[work-record-009] renderer retains tool call and result when explicitly supplied as constitutive Opening', () => {
  // COMPANION-014 ⑨：显式作为 Opening material 供应的 commitment 调用及其结果
  // 不得当 incidental tool 滤入 Recent work（XTrace.forOpening 不过滤 raw）。
  const commitCall = xTrace.item({
    sequence: 1,
    role: 'assistant',
    part: xTrace.toolCall('todowrite-for-plan', '{"view":{"declared":"plan"}}'),
  })
  const commitResult = xTrace.item({ sequence: 2, role: 'assistant', part: xTrace.toolResult('accepted') })
  const openingWithCommitment = workRecord.withConstitutive(
    opening('charge'),
    traceOwner.render(traceOwner.forOpening([commitCall, commitResult])),
  )

  const rendered = materialize(openingWithCommitment, [], [], { Sequence: 0 }, OPENING_END)

  // 供应的 call/result 保留在 Opening（constitutive body），不因 forWorkRecord 被滤除
  assert.equal(rendered.includes('[tool call] todowrite-for-plan'), true)
  assert.equal(rendered.includes('[tool result] accepted'), true)
  assert.equal(rendered.includes('Opening'), true)
  assert.equal(rendered.includes('Recent work'), false)
})

test('WHAT[work-record-009] actual materializer ends Opening at the first semantic part, not at a planning commitment', async () => {
  await withReopenableJournal(async handle => {
    const session = 'record-opening-boundary'
    await record.captureOpening(handle, session, 'original charge', [])
    await record.captureProjection(handle, session, { messages: [
      { role: 'user', parts: [{ kind: 'text', text: 'original charge' }] },
      { role: 'assistant', parts: [{ kind: 'tool-call', callId: 'commit', name: 'todowrite-for-plan', args: '{"view":{"declared":"plan"}}' }] },
      { role: 'tool', parts: [{ kind: 'tool-result', callId: 'commit', result: 'accepted' }] },
      { role: 'assistant', parts: [{ kind: 'text', text: 'work after the charge' }] },
    ] })

    const rendered = await record.lifecycleWorkRecord(handle, session, true)

    // Opening 段承载 invocation 起点的原始 charge，且止于首个语义 part。
    assert.equal(rendered.startsWith('Opening\noriginal charge'), true)
    // 其后的 commitment 调用不是 Opening material；它也不得作为 raw tool 进入工作正文。
    assert.equal(rendered.includes('todowrite-for-plan'), false)
    assert.equal(rendered.includes('[tool result]'), false)
    assert.equal(rendered.includes('work after the charge'), true)
  })
})

test('WHAT[work-record-009] actual materializer keeps the Opening interval raw call/result inside Opening only', async () => {
  await withReopenableJournal(async handle => {
    const session = 'record-opening-raw-channel'
    await record.captureOpening(handle, session, 'original charge', [])
    // invocation 起点的首个语义 part 是 tool result（初始交付物）。该通道不得过滤 raw tool，
    // 也不得把它降级成 incidental tool 丢出记录。
    await record.captureProjection(handle, session, { messages: [
      { role: 'tool', parts: [{ kind: 'tool-result', callId: 'seed', result: 'INITIAL_DELIVERABLE_BYTES' }] },
      { role: 'assistant', parts: [{ kind: 'text', text: 'work after the deliverable' }] },
    ] })

    const withOpening = await record.lifecycleWorkRecord(handle, session, true)
    const sections = sectionsOf(withOpening)

    // 供应的 call/result 完整保留在 Opening（唯一不过滤 raw tool 的通道）。
    assert.ok(sections.Opening.includes('original charge'), withOpening)
    assert.ok(sections.Opening.includes('[tool result] INITIAL_DELIVERABLE_BYTES'), withOpening)
    // 同一 part 不得作为 raw tool 进入工作正文（013：正文只渲染 text/reasoning）。
    assert.equal(sections['Recent work'].includes('[tool result]'), false, withOpening)
    assert.equal(sections['Recent work'].includes('INITIAL_DELIVERABLE_BYTES'), false, withOpening)
    assert.ok(sections['Recent work'].includes('work after the deliverable'), withOpening)

    // 省略 Opening 时该通道整体不渲染，材料也不得换名混入正文。
    const withoutOpening = await record.lifecycleWorkRecord(handle, session, false)
    assert.equal(withoutOpening.includes('INITIAL_DELIVERABLE_BYTES'), false, withoutOpening)
    assert.equal(withoutOpening.includes('[tool result]'), false, withoutOpening)
    assert.ok(withoutOpening.includes('work after the deliverable'), withoutOpening)
  })
})
