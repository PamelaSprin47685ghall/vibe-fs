// Process ownership checks: bounded kill acknowledgement and typed request data.

import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import test from 'node:test'
import { fileURLToPath } from 'node:url'

const {
  command,
  commandView,
  estimate,
  estimateView,
} = await import('../../../dist/Process/Surface.js')

test('WHAT[PROC-006] EXEC_011_kill_ack_grace_is_finite_not_MaxTimerWaitMs', () => {
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

test('WHAT[PROC-004] EXEC_oneshot_completion_wait_is_bounded_by_management_deadline', () => {
  const root = join(dirname(fileURLToPath(import.meta.url)), '../../..')
  const oneshot = readFileSync(
    join(root, 'src/Wanxiangshu/Execution/Delegation/Handle/OpenCode/OneShotTool.fs'),
    'utf8',
  )

  assert.match(oneshot, /CompletionTimeoutMs\s*=\s*600_000/, 'named completion deadline')
  assert.match(oneshot, /PtyTiming\.raceExit/, 'completion races a timer, not bare Task')
  assert.doesNotMatch(
    oneshot,
    /let! output = completion\.Task\s*$/m,
    'must not bare-await completion.Task without race',
  )
  assert.match(oneshot, /AbortSession childId/, 'timeout path aborts the child session')
  assert.match(oneshot, /timed out after/, 'timeout returns Error with timeout message, not hang')
})

test('WHAT[PROC-005] EXEC_010_process_request_carries_all_fields', () => {
  const cmd = command('sh', ['-lc', 'echo hi'], '/tmp/wx', 'input')
  const cmdView = commandView(cmd)
  const estView = estimateView(estimate(42, 65536, 'large'))

  assert.equal(cmdView.fileName, 'sh')
  assert.deepEqual(cmdView.arguments, ['-lc', 'echo hi'])
  assert.equal(cmdView.workingDirectory, '/tmp/wx')
  assert.equal(cmdView.stdin, 'input')
  assert.equal(estView.runtimeSeconds, 42)
  assert.equal(estView.outputBytes, 65536)
  assert.equal(estView.memory, 'large')
})
