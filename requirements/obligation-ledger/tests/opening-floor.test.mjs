// AC15: Manager BlindPlan Opening never compressed (dynamic Pre-T1 floor; Post-T1 nail).
// AC16: T1 call/result ∈ OpeningMaterial; WorkRecordStart = OpeningBoundary.

import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'
import * as todo from '../../../dist/Mission/Obligation/Todo/MagicTodoSemanticSurface.js'
import * as opening from '../../../dist/Mission/WorkRecord/OpeningSemanticSurface.js'

const ROOT = join(dirname(fileURLToPath(import.meta.url)), '../../..')
const item = (sequence, role, part) => opening.item(sequence, role, part)

test('WHAT[OBLIGATION-LEDGER-017] Pre-T1: effectiveOpeningFloor tracks XTrace head (Opening never enters Y)', () => {
  const floor = todo.effectiveOpeningFloor(true, false, 1, null, null, 7, [
    { sequence: 1, kind: 'text', text: 'opening' },
    { sequence: 7, kind: 'text', text: 'head' },
  ])
  assert.equal(floor, 7)
  assert.equal(todo.bloggerEffectiveStart(3, floor), 7)
})

test('WHAT[OBLIGATION-LEDGER-017] Pre-T1: no CurrentLife → no floor', () => {
  assert.equal(todo.effectiveOpeningFloor(false, false, 1, null, null, 4, []), null)
})

test('WHAT[OBLIGATION-LEDGER-016] false planning checkpoints do not close Opening; first true commitment nails WorkRecordStart', () => {
  const callId = 't1-call'
  const parts = [
    { sequence: 1, kind: 'text', text: 'opening' },
    { sequence: 5, kind: 'tool_call', toolCallId: callId },
    { sequence: 6, kind: 'tool_result', toolCallId: callId },
    { sequence: 9, kind: 'text', text: 'later' },
  ]
  assert.equal(todo.blindPlanOpeningBoundary(1, 5, callId, parts), 7)
  assert.equal(todo.effectiveOpeningFloor(true, false, 1, null, null, 9, parts), 9)
  const floor = todo.effectiveOpeningFloor(true, true, 1, 5, callId, 9, parts)
  assert.equal(floor, 7)
  assert.equal(todo.bloggerEffectiveStart(7, floor), 7)
  assert.equal(todo.bloggerEffectiveStart(10, floor), 10)
})

test('WHAT[OBLIGATION-LEDGER-016] T1 constitutive body renders in Opening, not Recent', () => {
  const openingCharge = opening.opening('Ship the bridge.', [], '')
  const constitutive = [
    item(5, 'assistant', opening.toolCallPart('todowrite', '{"planComplete":true,"obligations":[]}')),
    item(6, 'tool', opening.toolResultPart('The Manager who will carry it is you.')),
  ]
  const withT1 = opening.withConstitutive(openingCharge, constitutive)
  const trace = [
    item(1, 'user', opening.textPart('Ship the bridge.')),
    ...constitutive,
    item(8, 'assistant', opening.textPart('post-T1 labor')),
  ]

  const rendered = opening.materialize(withT1, [], trace, 0, 7, true)
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
    item(5, 'assistant', opening.toolCallPart('todowrite', '{}')),
    item(6, 'tool', opening.toolResultPart('entrusted')),
  ]
  assert.equal(opening.forOpening(items).length, 2)
  assert.equal(opening.forWorkRecord(items).length, 0)
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
