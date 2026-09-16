import assert from 'node:assert/strict'
import { spawnSync } from 'node:child_process'
import { fileURLToPath } from 'node:url'
import test from 'node:test'

const scenario = fileURLToPath(
  new URL('../../participant-identity/tests/support/session-reuse-plugin-scenario.mjs', import.meta.url),
)

test('WHAT[MANAGED-SESSION-020] fresh identity waits for exact durable prior-run closure on the public plugin canary', () => {
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

  const lines = child.stdout.trim().split('\n').filter(Boolean)
  assert.equal(lines.length, 1, `expected one structured event line:\n${child.stdout}`)
  const trace = JSON.parse(lines[0])
  assert.equal(trace.ok, true)

  const replacement = trace.events.find((event) => event.case === 'same-session-role-replacement')
  assert.ok(replacement)
  assert.equal(replacement.conflictKind, 'ActiveRunIdentityConflict')
  assert.equal(replacement.providerOrRetryExecutionsBeforeClose, 0)
  assert.equal(replacement.duplicateLeadLogicalRun, replacement.leadLogicalRun)
  assert.notEqual(replacement.operatorLogicalRun, replacement.leadLogicalRun)

  const restart = trace.events.find((event) => event.case === 'rolling-restart-durable-identity')
  assert.ok(restart)
  assert.equal(restart.activeLogicalRun, replacement.operatorLogicalRun)
  assert.equal(restart.duplicateLogicalRun, replacement.operatorLogicalRun)
})
