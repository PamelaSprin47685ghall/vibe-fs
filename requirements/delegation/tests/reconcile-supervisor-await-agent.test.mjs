// AwaitAgent timeout remains bounded in the Host Join owner.
import assert from 'node:assert/strict'
import test from 'node:test'
import { readFileSync } from 'node:fs'

const source = readFileSync(new URL('../../../src/Wanxiangshu/Execution/Delegation/Fork/Host/Join.fs', import.meta.url), 'utf8')

test('WHAT[DELEG-013] EXEC_fork_runtime_await_agent_timeout', () => {
  assert.match(source, /let awaitAgent/)
  assert.match(source, /timeoutMs/)
  assert.match(source, /AwaitAgent\(agentId/)
  assert.match(source, /runtime\.Runtime\.AwaitAgent\(agentId|timeoutMs = timeoutMs/)
})
