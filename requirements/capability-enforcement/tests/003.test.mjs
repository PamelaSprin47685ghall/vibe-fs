// ENF-003: Capability projection narrowing
import assert from 'node:assert/strict'
import test from 'node:test'
import { plan } from '../../../dist/Participant/Provider/Attempt/PlannerSurface.js'

test('WHAT[ENF-003] PROMPT_008_the_request_kind_is_carried_not_inferred', () => {
  for (const kind of ['work-main', 'blogger-main', 'blogger-squash', 'interaction-repair']) {
    const planned = plan({ role: 'coder', tier: 'fast', kind })
    assert.equal(planned.ok, true, planned.error)
    assert.equal(planned.requestKind, kind)
  }
})
