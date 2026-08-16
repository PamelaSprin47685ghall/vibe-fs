// tests/unit/glory/opening-floor.test.mjs — AC15 / AC16 (TODO-001 / TODO-015 / GLORY-074).
//
// AC15: Manager BlindPlan Opening never compressed (dynamic Pre-T1 floor; Post-T1 nail).
// AC16: T1 call/result ∈ OpeningMaterial; WorkRecordStart = OpeningBoundary.
// Static: BloggerCoordinator + CompanionTransform zero ProtectedPrefixEnd refs.

import assert from 'node:assert/strict'
import test from 'node:test'
import { readFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { lifecycleWorkRecord, magicTodo, xTrace } from '../../verification-system/tests/support/domain.mjs'

const ROOT = join(dirname(fileURLToPath(import.meta.url)), '../../..')

test('WHAT[OBLIGATION-LEDGER-017] Pre-T1: effectiveOpeningFloor tracks XTrace head (Opening never enters Y)', () => {
  const floor = magicTodo.effectiveOpeningFloor(true, false, 1, undefined, undefined, 7, [
    { sequence: 1, kind: 'text' },
    { sequence: 7, kind: 'text' },
  ])
  assert.equal(floor, 7)
  // Coverage behind head still floors at head — Opening suffix stays protected.
  assert.equal(magicTodo.bloggerEffectiveStart(3, floor), 7)
})

test('WHAT[OBLIGATION-LEDGER-017] Pre-T1: no CurrentLife → no floor', () => {
  assert.equal(magicTodo.effectiveOpeningFloor(false, false, 1, undefined, undefined, 4, []), undefined)
})

test('WHAT[OBLIGATION-LEDGER-016] false planning checkpoints do not close Opening; first true commitment nails WorkRecordStart', () => {
  const callId = 't1-call'
  const parts = [
    { sequence: 1, kind: 'text' },
    { sequence: 5, kind: 'tool_call', toolCallId: callId },
    { sequence: 6, kind: 'tool_result', toolCallId: callId },
    { sequence: 9, kind: 'text' },
  ]
  const boundary = magicTodo.blindPlanOpeningBoundary(1, 5, callId, parts)
  assert.equal(boundary, 7) // exclusive end after result at 6

  const stillPlanning = magicTodo.effectiveOpeningFloor(true, false, 1, undefined, undefined, 9, parts)
  assert.equal(stillPlanning, 9, 'accepted planComplete=false checkpoints remain inside dynamic Opening')

  const floor = magicTodo.effectiveOpeningFloor(true, true, 1, 5, callId, 9, parts)
  assert.equal(floor, 7)
  // Material after OpeningBoundary may enter Y; Opening itself stays floored.
  assert.equal(magicTodo.bloggerEffectiveStart(7, floor), 7)
  assert.equal(magicTodo.bloggerEffectiveStart(10, floor), 10)
})

test('WHAT[OBLIGATION-LEDGER-016] T1 constitutive body renders in Opening, not Recent', () => {
  const openingCharge = lifecycleWorkRecord.opening({ assignment: 'Ship the bridge.' })
  const constitutive = [
    xTrace.item({
      sequence: 5,
      role: 'assistant',
      part: xTrace.toolCall('todowrite', '{"planComplete":true,"obligations":[]}'),
    }),
    xTrace.item({
      sequence: 6,
      role: 'tool',
      part: xTrace.toolResult('The Manager who will carry it is you.'),
    }),
  ]
  const opening = lifecycleWorkRecord.withConstitutive(openingCharge, constitutive)
  const trace = [
    xTrace.item({ sequence: 1, role: 'user', part: xTrace.text('Ship the bridge.') }),
    ...constitutive,
    xTrace.item({ sequence: 8, role: 'assistant', part: xTrace.text('post-T1 labor') }),
  ]

  const rendered = lifecycleWorkRecord.materialize(
    opening,
    [],
    trace,
    { Sequence: 0 },
    { Sequence: 7 },
    true,
  )

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
    xTrace.item({ sequence: 5, role: 'assistant', part: xTrace.toolCall('todowrite', '{}') }),
    xTrace.item({ sequence: 6, role: 'tool', part: xTrace.toolResult('entrusted') }),
  ]
  assert.equal(xTrace.forOpening(items).length, 2)
  assert.equal(xTrace.forWorkRecord(items).length, 0)
})

test('WHAT[OBLIGATION-LEDGER-017] static: BloggerCoordinator + CompanionTransform zero ProtectedPrefixEnd refs', () => {
  for (const rel of [
    'src/Wanxiangshu/Context/Companion/Blogger/Runtime/Coordinator.fs',
    'src/Wanxiangshu/Context/Companion/Transform.fs',
  ]) {
    const src = readFileSync(join(ROOT, rel), 'utf8')
    assert.equal(
      src.includes('ProtectedPrefixEnd'),
      false,
      `${rel} must not reference ProtectedPrefixEnd`,
    )
  }
})
