import assert from 'node:assert/strict'
import test from 'node:test'
import { parse as parseToml } from 'smol-toml'
import * as join from '../../../dist/Execution/Delegation/Fork/OpenCode/JoinSurface.js'

test('WHAT[delegation-005] completed wire presents Byname and record without exposing supplied physical identities', () => {
  for (const language of ['english', 'zh-CN']) {
    const wire = join.renderBatch(language, [{
      kind: 'completed', agentId: 'PHYSICAL-AGENT-ID', agentName: 'Ada',
      role: 'Engineer', runId: 'PHYSICAL-RUN-ID', workRecord: 'entry-local evidence',
    }])
    assert.match(wire, /Ada/)
    assert.match(wire, /entry-local evidence/)
    assert.doesNotMatch(wire, /PHYSICAL-AGENT-ID|PHYSICAL-RUN-ID/)
    assert.deepEqual(parseToml(wire), {})
  }
})

test.todo('WHAT[delegation-005] all public schemas and success/failure returns exclude physical topology, including missing display-name paths (GAP-153)')
