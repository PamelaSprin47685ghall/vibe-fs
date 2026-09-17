import assert from 'node:assert/strict'
import { execFileSync, spawnSync } from 'node:child_process'
import { mkdtempSync, readFileSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'
import { fileURLToPath } from 'node:url'

const scenario = fileURLToPath(new URL('../support/devops-crash-scenario.mjs', import.meta.url))

const runChild = (mode, workspace, marker) =>
  spawnSync(process.execPath, [scenario, mode, workspace, marker], {
    cwd: process.cwd(),
    encoding: 'utf8',
    env: process.env,
  })

const readMarker = (path) => JSON.parse(readFileSync(path, 'utf8'))

test('WHAT[CRASH-020] DevOps crash recovery maintains single logical authority, locks model, and avoids command auto-replay', () => {
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
