// BlindPlan governs planning/commitment semantics, while context compression
// protects only the true Life Opening (CONTEXT-COMPRESSION-017).

import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'
import * as todo from '../../../dist/Mission/Obligation/Todo/MagicTodoSemanticSurface.js'
import * as opening from '../../../dist/Mission/WorkRecord/OpeningSemanticSurface.js'
import * as traceOwner from '../../../dist/Context/Trace/SemanticTraceSurface.js'

const ROOT = join(dirname(fileURLToPath(import.meta.url)), '../../..')
const item = (sequence, role, part) => traceOwner.item({ sequence, role, part })

test('WHAT[OBLIGATION-LEDGER-017] Pre-T1 BlindPlan does not enlarge the structural Opening floor', () => {
  const floor = todo.effectiveOpeningFloor(true, false, 1, null, null, 7, [
    { sequence: 1, kind: 'text', text: 'opening' },
    { sequence: 7, kind: 'text', text: 'head' },
  ])
  assert.equal(floor, 2)
  assert.equal(todo.bloggerEffectiveStart(3, floor), 3)
})

test('WHAT[OBLIGATION-LEDGER-017] Pre-T1: no CurrentLife → no floor', () => {
  assert.equal(todo.effectiveOpeningFloor(false, false, 1, null, null, 4, []), null)
})

test('WHAT[OBLIGATION-LEDGER-016] T1 constitutive boundary is independent from the compression floor', () => {
  const callId = 't1-call'
  const parts = [
    { sequence: 1, kind: 'text', text: 'opening' },
    { sequence: 5, kind: 'tool_call', toolCallId: callId },
    { sequence: 6, kind: 'tool_result', toolCallId: callId },
    { sequence: 9, kind: 'text', text: 'later' },
  ]
  assert.equal(todo.blindPlanOpeningBoundary(1, 5, callId, parts), 7)
  assert.equal(todo.effectiveOpeningFloor(true, false, 1, null, null, 9, parts), 2)
  const floor = todo.effectiveOpeningFloor(true, true, 1, 5, callId, 9, parts)
  assert.equal(floor, 2)
  assert.equal(todo.bloggerEffectiveStart(7, floor), 7)
  assert.equal(todo.bloggerEffectiveStart(10, floor), 10)
})

test('WHAT[OBLIGATION-LEDGER-016] T1 constitutive body renders in Opening, not Recent', () => {
  const openingCharge = opening.opening('Ship the bridge.', [], '')
  const constitutive = [
    item(5, 'assistant', traceOwner.toolCallPart('t1', 'todowrite', '{"planComplete":true,"workingOn":"","obligations":[]}')),
    item(6, 'tool', traceOwner.toolResultPart('t1', 'The Manager who will carry it is you.')),
  ]
  const withT1 = opening.withConstitutive(openingCharge, traceOwner.render(traceOwner.forOpening(constitutive)))
  const trace = [
    item(1, 'user', traceOwner.textPart('Ship the bridge.')),
    ...constitutive,
    item(8, 'assistant', traceOwner.textPart('post-T1 labor')),
  ]

  const gap = traceOwner.render(traceOwner.forWorkRecord(traceOwner.sliceFrom({ sequence: 7 }, trace)))
  const rendered = opening.materialize(withT1, [], gap, true)
  assert.match(rendered, /^Opening\n/)
  assert.match(rendered, /Ship the bridge/)
  assert.match(rendered, /\[tool call\] todowrite/)
  assert.match(rendered, /The Manager who will carry it is you/)
  assert.match(rendered, /Recent work\n/)
  assert.match(rendered, /post-T1 labor/)
  const recentSection = rendered.slice(rendered.indexOf('Recent work'))
  assert.doesNotMatch(recentSection, /todowrite/)
  assert.doesNotMatch(recentSection, /The Manager who will carry it is you/)
})

test('WHAT[OBLIGATION-LEDGER-016] XTrace.forOpening keeps T1 tools; forWorkRecord drops them', () => {
  const items = [
    item(5, 'assistant', traceOwner.toolCallPart('t1', 'todowrite', '{}')),
    item(6, 'tool', traceOwner.toolResultPart('t1', 'entrusted')),
  ]
  assert.equal(traceOwner.forOpening(items).length, 2)
  assert.equal(traceOwner.forWorkRecord(items).length, 0)
})

test('WHAT[OBLIGATION-LEDGER-017] static: BloggerCoordinator + CompanionTransform zero ProtectedPrefixEnd refs', () => {
  for (const rel of [
    'src/Wanxiangshu/Context/Companion/Blogger/Runtime/Coordinator.fs',
    'src/Wanxiangshu/Context/Companion/Transform.fs',
  ]) {
    const src = readFileSync(join(ROOT, rel), 'utf8')
    assert.equal(src.includes('ProtectedPrefixEnd'), false, `${rel} must not reference ProtectedPrefixEnd`)
  }
})
