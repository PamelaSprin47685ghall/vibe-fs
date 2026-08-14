// Split from tests/unit/execution/handle.test.mjs (cutover Wave 2a);
// owner: process-execution. EXEC-011/010 process 面：kill-ack 有界等待（PROC-006）、
// OneShotAgentTool completion 有界（PROC-004）、typed ProcessRequest 全字段
// （PROC-005）。handle 生命周期 → managed-session-lifecycle；
// deadline/estimate 纯代数 → time-capability。

import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import test from 'node:test'
import { fileURLToPath } from 'node:url'
import {
  caseOf,
  listItems,
  payloadOf,
  processRequest,
} from '../../verification-system/tests/support/domain.mjs'

test('EXEC_011_kill_ack_grace_is_finite_not_MaxTimerWaitMs', () => {
  // After SIGKILL, wait must not use MaxTimerWaitMs (~24.8d) or unbounded Exit.Task.
  // KillAckGraceMs is the management bound; TimedOut + ExitCode=-1 when close never comes.
  const root = join(dirname(fileURLToPath(import.meta.url)), '../../..')
  const waitSrc = readFileSync(join(root, 'src/Wanxiangshu/Process/NodeProcessWait.fs'), 'utf8')

  assert.match(waitSrc, /let KillAckGraceMs = /, 'named kill-ack constant required')
  assert.match(waitSrc, /waitForSignal child KillAckGraceMs/, 'post-kill wait uses kill-ack, not MaxTimerWaitMs')
  assert.doesNotMatch(
    waitSrc,
    /killSent then[\s\S]{0,80}waitSegment Deadline\.MaxTimerWaitMs/,
    'post-kill must not wait MaxTimerWaitMs',
  )
  assert.match(
    waitSrc,
    /ExitCode = -1[\s\S]{0,40}TimedOut = true/,
    'kill-ack expiry returns TimedOut with unknown exit code, not fake success',
  )
  assert.match(waitSrc, /KillNotAcknowledged/, 'kill-ack expiry exits the wait loop')
})

test('EXEC_oneshot_completion_wait_is_bounded_by_management_deadline', () => {
  // OneShotAgentTool must not await completion.Task unbounded.
  const root = join(dirname(fileURLToPath(import.meta.url)), '../../..')
  const oneshot = readFileSync(
    join(root, 'src/Wanxiangshu/Infrastructure/OpenCode/Tools/OneShotAgentTool.fs'),
    'utf8',
  )

  assert.match(oneshot, /CompletionTimeoutMs\s*=\s*600_000/, 'named completion deadline')
  assert.match(oneshot, /PtyTiming\.raceExit/, 'completion races a timer, not bare Task')
  assert.doesNotMatch(
    oneshot,
    /let! output = completion\.Task\s*$/m,
    'must not bare-await completion.Task without race',
  )
  assert.match(
    oneshot,
    /AbortSession childId/,
    'timeout path aborts the child session',
  )
  assert.match(
    oneshot,
    /timed out after/,
    'timeout returns Error with timeout message, not hang',
  )
})

// ── EXEC-010: a process request carries the full executor estimate ─────────────

test('EXEC_010_process_request_carries_all_fields', () => {
  const cmd = processRequest.command({
    fileName: 'sh',
    args: ['-lc', 'echo hi'],
    workingDirectory: '/tmp/wx',
    stdin: 'input',
  })
  const est = processRequest.estimate({ runtimeSeconds: 42, outputBytes: 65536, memory: 'Large' })

  assert.equal(cmd.FileName, 'sh')
  assert.deepEqual(listItems(cmd.Arguments), ['-lc', 'echo hi'])
  assert.equal(cmd.WorkingDirectory, '/tmp/wx')
  assert.equal(cmd.Stdin, 'input')
  assert.equal(payloadOf(est.EstimatedRuntime), 42)
  assert.equal(payloadOf(est.EstimatedOutput), 65536n)
  assert.equal(caseOf(est.EstimatedMemory), 'Large')
})
