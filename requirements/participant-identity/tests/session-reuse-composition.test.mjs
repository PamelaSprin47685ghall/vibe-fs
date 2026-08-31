import assert from 'node:assert/strict'
import { spawnSync } from 'node:child_process'
import { fileURLToPath } from 'node:url'
import test from 'node:test'

const scenario = fileURLToPath(
  new URL('./support/session-reuse-plugin-scenario.mjs', import.meta.url),
)

test('WHAT[PID-011] production plugin replaces identity only after exact durable Manager closure', () => {
  const child = spawnSync(process.execPath, [scenario], {
    cwd: process.cwd(),
    encoding: 'utf8',
    env: {
      ...process.env,
      WANXIANGSHU_NO_FATAL_EXIT: '1',
      WANXIANGSHU_PROVIDER_LANGUAGE: 'en',
    },
  })

  assert.equal(child.error, undefined, child.error?.message)
  assert.equal(child.status, 0, `subprocess exited ${child.status}\nstdout:\n${child.stdout}\nstderr:\n${child.stderr}`)
  assert.doesNotMatch(child.stderr, /AGENT-028|\bfatal\b|plugin-hook-[^\n]*-failed/i)

  const outputLines = child.stdout.trim().split('\n').filter(Boolean)
  assert.equal(outputLines.length, 1, `expected one structured event line:\n${child.stdout}`)
  const trace = JSON.parse(outputLines[0])

  assert.equal(trace.ok, true)
  assert.deepEqual(
    trace.events.map((event) => event.case),
    [
      'same-session-role-replacement',
      'rolling-restart-durable-identity',
      'cross-plugin-instance-isolation',
    ],
  )

  const replacement = trace.events[0]
  assert.equal(replacement.conflictKind, 'ActiveRunIdentityConflict')
  assert.equal(replacement.providerOrRetryExecutionsBeforeClose, 0)
  assert.equal(replacement.duplicateLeadLogicalRun, replacement.leadLogicalRun)
  assert.notEqual(replacement.operatorLogicalRun, replacement.leadLogicalRun)

  const restart = trace.events[1]
  assert.equal(restart.persona, 'Operator')
  assert.equal(restart.duplicateLogicalRun, replacement.operatorLogicalRun)
  assert.equal(restart.activeLogicalRun, replacement.operatorLogicalRun)

  const concurrent = trace.events[2]
  assert.equal(concurrent.persona, 'Operator')
  assert.equal(concurrent.activeLogicalRun, replacement.operatorLogicalRun)
})
