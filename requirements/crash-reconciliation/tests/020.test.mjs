import assert from 'node:assert/strict'
import { execFileSync, spawnSync } from 'node:child_process'
import { mkdtempSync, readFileSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'
import { fileURLToPath } from 'node:url'
import * as RolesSurface from '../../../dist/Foundation/RolesSurface.js'
import * as resume from '../../../dist/OpenCode/Host/ExplicitResumeSurface.js'
import { integrationTest } from '../../verification-system/tests/support/tier-gate.mjs'

const scenario = fileURLToPath(new URL('./support/devops-crash-scenario.mjs', import.meta.url))

const runChild = (mode, workspace, marker) =>
  spawnSync(process.execPath, [scenario, mode, workspace, marker], {
    cwd: process.cwd(),
    encoding: 'utf8',
    env: process.env,
  })

const readMarker = (path) => JSON.parse(readFileSync(path, 'utf8'))

test('WHAT[crash-reconciliation-020] DevOps crash recovery maintains single logical authority, locks model, and avoids command auto-replay', async () => {
  // 1. RolesSurface must have consolidated DevOps and Engineer
  const all = RolesSurface.allRoleLabels
  assert.ok(all.includes('devops'), 'Role labels must have devops')
  assert.ok(all.includes('engineer'), 'Role labels must have engineer')
  assert.equal(all.includes('coder'), false, 'Role labels must not contain coder')

  // 2. Explicit resume command behavior
  const config = { command: {} }
  resume.registerCommand(config)
  assert.ok(config.command.continue, 'continue command must be registered')

  // Non-continue command is a no-op (no auto-replay of pending commands)
  const actual = await resume.run('status', 'session-1', '')
  assert.deepEqual(actual.parts, [], 'non-continue command must not trigger automatic execution')
})

integrationTest('WHAT[crash-reconciliation-020] DevOps crash recovery maintains single logical authority, locks model, and avoids command auto-replay (scenario)', () => {
  const workspace = mkdtempSync(join(tmpdir(), 'wxs-devops-crash-'))
  const beforeMarker = join(workspace, 'before-crash.json')
  const afterMarker = join(workspace, 'after-reopen.json')

  try {
    execFileSync('git', ['init', '--quiet', workspace])

    const crashed = runChild('crash-with-inflight-command', workspace, beforeMarker)
    assert.equal(crashed.status, 86, crashed.stderr || crashed.stdout)
    const beforeState = readMarker(beforeMarker)
    assert.equal(beforeState.commandInFlight, true)
    assert.equal(beforeState.bindingCount, 1)

    const reopened = runChild('reopen-and-explicit-resume', workspace, afterMarker)
    assert.equal(reopened.status, 0, reopened.stderr || reopened.stdout)
    const afterState = readMarker(afterMarker)
    assert.equal(afterState.initialBindingCount, 0, 'No automatic replay of pending commands on startup')
    assert.equal(afterState.replayedCommands, 0, 'Zero commands automatically replayed')
    assert.equal(afterState.singleDevOpsAuthority, true, 'DevOps must map to single active authority')
    assert.equal(afterState.briefingAcknowledged, true, 'Briefing explicitly notices pending command interruption')
    assert.equal(afterState.modelLocked, true, 'DevOps model drift must be strictly rejected fail-closed')
  } finally {
    rmSync(workspace, { recursive: true, force: true })
  }
})
