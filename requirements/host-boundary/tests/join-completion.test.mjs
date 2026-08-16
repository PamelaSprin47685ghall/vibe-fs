// Split from tests/unit/execution/join-completion.test.mjs (cutover Wave 2a);
// owner: host-boundary. HostEventPort 观察可靠性（HOST-BOUNDARY-016）：sticky
// terminal 对 late subscriber 重放一次；无 provider run 的 Failed outcome 不去重。
// Join mailbox / 立即 claim 断言 → delegation。

import assert from 'node:assert/strict'
import test from 'node:test'
import { caseOf, hostEventPort, sessionId } from '../../verification-system/tests/support/domain.mjs'

// ── sticky terminal ──────────────────────────────────────────────────────────

test('WHAT[HOST-BOUNDARY-016] EXEC_join_NotifyTerminal_then_late_SubscribeTerminal_replays_sticky', () => {
  const port = hostEventPort.create()
  const child = sessionId('ses_sticky_child')
  const seen = []

  const delivered = hostEventPort.notify(port, child, hostEventPort.failed('early-terminal'))
  assert.equal(delivered, false, 'hasListeners=false when nobody subscribed yet')

  hostEventPort.subscribe(port, (_sid, outcome) => {
    seen.push(caseOf(outcome))
  })

  assert.equal(seen.length, 1, 'late subscriber must receive sticky terminal once')
  assert.equal(seen[0], 'Failed')
})

test('WHAT[HOST-BOUNDARY-016] EXEC_join_Failed_outcomes_are_not_provider_run_deduped', () => {
  const port = hostEventPort.create()
  const child = sessionId('ses_dedupe')
  let count = 0
  hostEventPort.subscribe(port, () => {
    count += 1
  })
  hostEventPort.notify(port, child, hostEventPort.failed('a'))
  hostEventPort.notify(port, child, hostEventPort.failed('b'))
  assert.equal(count, 2)
})
