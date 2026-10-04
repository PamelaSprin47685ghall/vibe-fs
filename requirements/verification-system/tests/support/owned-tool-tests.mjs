import assert from 'node:assert/strict'
import fs from 'node:fs'
import os from 'node:os'
import path from 'node:path'
import test from 'node:test'
import { spawnOwnedVerificationTool } from '../../../../scripts/lib/verification-owned-tool.mjs'

async function captureTool(executable, argv, options) {
  const owner = spawnOwnedVerificationTool(executable, argv, options)
  let stdout = ''
  let stderr = ''
  owner.child.stdout.setEncoding('utf8').on('data', chunk => { stdout += chunk })
  owner.child.stderr.setEncoding('utf8').on('data', chunk => { stderr += chunk })
  const outcome = await owner.completed
  return { ...outcome, stdout, stderr, monitorPid: owner.child.pid }
}

export function registerOwnedToolTests() {
  test('WHAT[verification-system-006] owned tool monitoring preserves actual terminals and rejects missing control evidence', {
    skip: process.platform !== 'linux' && process.platform !== 'darwin',
  }, async t => {
    const directory = fs.mkdtempSync(path.join(os.tmpdir(), 'owned-tool-protocol-'))
    const env = { HOME: directory, PATH: '', SELECTED_TOOL_PROOF: 'original selected environment', LANG: 'C', LC_ALL: 'C' }
    if (process.platform === 'darwin') env.__CF_USER_TEXT_ENCODING = `0x${process.getuid().toString(16)}:0x0:0x0`
    try {
      await t.test('WHAT[verification-system-006] empty PATH preserves exact selected environment and actual independent process group', async () => {
        const outcome = await captureTool(process.execPath, ['--input-type=module', '-e', `
import { execFileSync } from 'node:child_process'
console.log(JSON.stringify({ env: process.env, pid: process.pid, pgid: Number(execFileSync('/bin/ps', ['-o', 'pgid=', '-p', String(process.pid)], { encoding: 'utf8' })), ipc: typeof process.send }))
process.stderr.write('actual selected stderr\\n')
`], { cwd: directory, env })
        assert.equal(outcome.failure, null)
        assert.equal(outcome.exitCode, 0)
        assert.equal(outcome.signal, null)
        const selected = JSON.parse(outcome.stdout)
        assert.deepEqual(selected.env, env)
        assert.equal(selected.ipc, 'undefined')
        assert.equal(selected.pgid, selected.pid)
        assert.notEqual(selected.pid, outcome.monitorPid)
        assert.equal(outcome.stderr, 'actual selected stderr\n')
      })

      await t.test('WHAT[verification-system-006] a selected nonzero exit is preserved instead of the monitor exit', async () => {
        const outcome = await captureTool(process.execPath, ['-e', "process.stdout.write('selected result\\n'); process.exitCode = 23"], { cwd: directory, env })
        assert.equal(outcome.failure, null)
        assert.equal(outcome.exitCode, 23)
        assert.equal(outcome.signal, null)
        assert.equal(outcome.stdout, 'selected result\n')
        assert.equal(outcome.stderr, '')
      })

      await t.test('WHAT[verification-system-006] a selected process signal is preserved instead of the monitor exit', async () => {
        const outcome = await captureTool(process.execPath, ['-e', "process.kill(process.pid, 'SIGTERM')"], { cwd: directory, env })
        assert.equal(outcome.failure, null)
        assert.equal(outcome.exitCode, null)
        assert.equal(outcome.signal, 'SIGTERM')
      })

      await t.test('WHAT[verification-system-006] a real selected executable spawn failure retains its OS cause', async () => {
        const executable = path.join(directory, 'absent-selected-tool')
        const outcome = await captureTool(executable, [], { cwd: directory, env })
        assert.equal(outcome.exitCode, null)
        assert.equal(outcome.signal, null)
        assert.ok(outcome.failure instanceof Error)
        assert.equal(outcome.failure.code, 'ENOENT')
        assert.equal(outcome.failure.path, executable)
        assert.match(outcome.failure.syscall, /spawn/)
      })

      for (const kind of ['missing', 'malformed', 'duplicate']) {
        await t.test(`WHAT[verification-system-006] ${kind} monitor terminal before selected spawn rejects acceptance`, async () => {
          const preload = path.join(directory, `${kind}-preload.mjs`)
          const monitorMarker = path.join(directory, `${kind}-monitor-started`)
          const selectedMarker = path.join(directory, `${kind}-selected-started`)
          const terminal = { type: 'verification-tool-terminal', version: 1, exitCode: 0, signal: null, failures: [] }
          const messages = kind === 'missing' ? [] : kind === 'malformed' ? [{ type: 'invalid-terminal' }] : [terminal, terminal]
          fs.writeFileSync(preload, `import fs from 'node:fs'
fs.writeFileSync(${JSON.stringify(monitorMarker)}, 'actual Node bootstrap executed')
for (const message of ${JSON.stringify(messages)}) {
  await new Promise((resolve, reject) => process.send(message, error => error ? reject(error) : resolve()))
}
process.exit(0)
`)
          const outcome = await captureTool(process.execPath, ['-e', `require('node:fs').writeFileSync(${JSON.stringify(selectedMarker)}, 'selected tool ran')`], {
            cwd: directory, env: { ...env, NODE_OPTIONS: `--import=${preload}` },
          })
          assert.equal(fs.readFileSync(monitorMarker, 'utf8'), 'actual Node bootstrap executed')
          assert.equal(fs.existsSync(selectedMarker), false, 'This protocol rejection precedes selected spawn; it does not prove recovery from a running monitor crash')
          assert.ok(outcome.failure instanceof Error)
          assert.match(outcome.failure.message, /terminal record|tool or its monitor failed/)
        })
      }
    } finally {
      fs.rmSync(directory, { recursive: true, force: true })
    }
  })
}
