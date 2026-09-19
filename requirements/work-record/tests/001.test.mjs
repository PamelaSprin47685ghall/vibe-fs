import { test } from 'node:test'
import assert from 'node:assert/strict'
import * as todo from '../../../dist/Mission/Obligation/Todo/MagicTodoSemanticSurface.js'
import * as workRecord from '../../../dist/Mission/WorkRecord/OpeningSemanticSurface.js'
import * as traceOwner from '../../../dist/Context/Trace/SemanticTraceSurface.js'

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

test('WHAT[work-record-001] LWR_same_record_projected_two_ways_shares_work_facts', () => {
  // COMPANION-015 ①：record 属于一段 work，不属于 receiver。同一 canonical record
  // 以 includeOpening=true / false 两种投影物化，work facts（Chronicle / Recent work）
  // 不变，只有 Opening 渲染段不同——投影选择不改变事实。
  const openingValue = opening('assigned task')
  const frames = ['did work']
  const trace = [xTrace.item({ sequence: 1, role: 'assistant', part: xTrace.text('Final summary') })]

  const withOpening = materialize(openingValue, frames, trace, { Sequence: 0 }, OPENING_END, true)
  const withoutOpening = materialize(openingValue, frames, trace, { Sequence: 0 }, OPENING_END, false)

  // 两投影共享 Chronicle 与 Recent work —— 同一段 work 的官方说法只有一份
  assert.match(withOpening, /Chronicle\ndid work/)
  assert.match(withoutOpening, /Chronicle\ndid work/)
  assert.match(withOpening, /Recent work\nassistant: Final summary/)
  assert.match(withoutOpening, /Recent work\nassistant: Final summary/)
  // 差异仅在 Opening 渲染段
  assert.equal(withOpening.includes('Opening'), true)
  assert.equal(withoutOpening.startsWith('Opening\n'), false)
})
