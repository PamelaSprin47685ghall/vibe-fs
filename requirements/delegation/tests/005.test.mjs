import assert from 'node:assert/strict'
import test from 'node:test'
import { parse as parseToml } from 'smol-toml'
import * as join from '../../../dist/Execution/Delegation/Fork/JoinSurface.js'

test('WHAT[delegation-005] completed wire presents Byname and record without exposing supplied physical identities', () => {
  for (const language of ['english', 'zh-CN']) {
    const wire = join.renderBatch(language, [{
      kind: 'completed', agentId: 'PHYSICAL-AGENT-ID', agentName: 'Ada',
      role: 'Engineer', runId: 'PHYSICAL-RUN-ID', returned: [{ text: 'entry-local evidence' }],
    }])
    assert.match(wire, /Ada/)
    assert.match(wire, /entry-local evidence/)
    assert.doesNotMatch(wire, /PHYSICAL-AGENT-ID|PHYSICAL-RUN-ID/)
    assert.deepEqual(parseToml(wire), { returned: [{ text: 'entry-local evidence' }] })
  }
})

test('WHAT[delegation-005] completed wire renders body prose as top comments and returned as carry data table', () => {
  for (const language of ['english', 'zh-CN']) {
    const wire = join.renderBatch(language, [{
      kind: 'completed', agentId: 'PHYSICAL-AGENT-ID', agentName: 'Ada',
      role: 'Engineer', runId: 'PHYSICAL-RUN-ID',
      body: 'Report from child agent Ada.',
      returned: [{ text: 'carry artifact' }],
    }])
    assert.match(wire, /^# Report from child agent Ada\.$/m)
    assert.deepEqual(parseToml(wire), { returned: [{ text: 'carry artifact' }] })
    assert.equal(parseToml(wire).body, undefined)
  }
})

test('WHAT[delegation-005] abandoned wire without a display name falls back to generic prose, never the physical agent id', () => {
  for (const language of ['english', 'zh-CN']) {
    const wire = join.renderBatch(language, [{
      kind: 'abandoned', agentId: 'PHYSICAL-AGENT-ID', reason: 'parent cancellation',
    }])
    assert.doesNotMatch(wire, /PHYSICAL-AGENT-ID/)
    assert.match(wire, /someone|某人/)
  }
})

/* deferred audit placeholder; refine phase replacements */
// task extraction done
