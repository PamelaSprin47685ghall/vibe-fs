// Split from tests/unit/execution/reconcile-supervisor.test.mjs (cutover Wave 2a);
// owner: delegation. ForkRuntime.AwaitAgent deadline：永不 settle 的 runner 在
// ~40ms 内以「timed out」Error 返回，不悬挂（AwaitAgentTimeoutMs 有界等待；
// reconcile machinery → host-boundary）。

import assert from 'node:assert/strict'
import test from 'node:test'
import { forkRuntime } from '../../verification-system/tests/support/domain.mjs'

test('WHAT[DELEG-013] EXEC_fork_runtime_await_agent_timeout', async () => {
  // Never settles: no timer handle (child can exit), no late resolution (no
  // asynchronous activity after verdict). AwaitAgent's timeout path races the
  // completion cell via PtyTiming.raceExit and does not depend on the runner.
  const hang = () => new Promise(() => {})
  const rt = forkRuntime.create((_agentId, _role, _prompt) => hang())
  const role = forkRuntime.role('Coder')
  forkRuntime.fork(rt, 'agent-hang', role, 'fast-coder', 'work')

  const started = Date.now()
  const result = await forkRuntime.awaitAgent(rt, 'agent-hang', 40)
  const elapsed = Date.now() - started

  assert.ok(elapsed >= 25, `expected ~40ms wait, got ${elapsed}ms`)
  assert.ok(elapsed < 2000, `must not hang unbounded; got ${elapsed}ms`)
  assert.equal(result.tag, 1, 'Error result')
  assert.match(result.fields[0], /timed out/)
  assert.match(result.fields[0], /agent-hang/)
})
