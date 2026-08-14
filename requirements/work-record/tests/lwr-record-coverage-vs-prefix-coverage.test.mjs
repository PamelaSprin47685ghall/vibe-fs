// WORK-RECORD-014 — RecordCoverage ≠ PrefixCoverage, two proof dimensions.
//
// The LWR gap is positioned by RecordCoverage (an XTrace cursor that may sit
// MID-turn); that same position is never a prefix-replacement proof, which
// lives in PrefixCoverage (complete Host turn boundary only). This file pins
// the record side: a mid-turn ingest is legal, self-contained review evidence,
// and the rendered Recent work reflects exactly the uncovered suffix.

import assert from 'node:assert/strict'
import test from 'node:test'
import { xTrace, lifecycleWorkRecord } from '../../verification-system/tests/support/domain.mjs'

const opening = (assignment, requirements = []) => lifecycleWorkRecord.opening({ assignment, requirements })

// One full turn of two parts: user text at 0, assistant reasoning at 1, and a
// second turn: assistant text at 2.
const trace = [
  xTrace.item({ sequence: 0, role: 'user', part: xTrace.text('Charge') }),
  xTrace.item({ sequence: 1, role: 'assistant', part: xTrace.reasoning('thinking') }),
  xTrace.item({ sequence: 2, role: 'assistant', part: xTrace.text('delivered') }),
]

test('LWR_recent_work_can_start_mid_turn_at_record_coverage', () => {
  // RecordCoverage consumed through cursor 1 (the reasoning part) — mid-turn
  // relative to any complete-turn boundary. The gap must start at cursor 2.
  const rendered = lifecycleWorkRecord.materialize(
    opening('Charge'),
    [],
    trace,
    { Sequence: 2 },
    { Sequence: 1 },
    true,
  )

  // The gap holds only the suffix from cursor 2: the assistant text. Reasoning at
  // cursor 1 was consumed by Y and is not re-rendered.
  assert.ok(rendered.includes('delivered'))
  assert.ok(!rendered.includes('thinking'), 'consumed reasoning must not reappear in the gap')

  // This mid-turn position is legal as review evidence — the record does not
  // demand a complete-turn boundary. Prefix replaceability is a different
  // dimension owned by context-compression / prefix-stability.
  const recentStart = rendered.indexOf('Recent work')
  assert.ok(recentStart >= 0)
  const recent = rendered.slice(recentStart)
  assert.ok(recent.includes('delivered'))
  assert.equal((recent.match(/delivered/g) ?? []).length, 1, 'the statement appears exactly once')
})

test('LWR_gap_from_origin_is_full_history_including_partial_turn', () => {
  // With coverage at origin, the gap is the whole trace after the opening end —
  // still NOT turn-bounded: a partial turn is a valid uncovered suffix.
  const rendered = lifecycleWorkRecord.materialize(
    opening('Charge'),
    [],
    trace,
    { Sequence: 0 },
    { Sequence: 1 },
    true,
  )

  assert.ok(rendered.includes('thinking'))
  assert.ok(rendered.includes('delivered'))
})
