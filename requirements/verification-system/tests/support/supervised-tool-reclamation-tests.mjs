import assert from 'node:assert/strict'
import { execFileSync, spawn } from 'node:child_process'
import fs from 'node:fs'
import os from 'node:os'
import path from 'node:path'
import { setTimeout as delay } from 'node:timers/promises'
import test from 'node:test'
import { PROCESS_TREE_TIMEOUT_MS, SIGKILL_GRACE_MS } from '../e2e/support/time-budget.js'

function processRows() {
  const output = execFileSync('ps', ['-eo', 'pid=,pgid=,stat='], {
    encoding: 'utf8', timeout: PROCESS_TREE_TIMEOUT_MS,
  })
  return output.trim().split('\n').map(line => {
    const fields = /^\s*(\d+)\s+(\d+)\s+(\S+)\s*$/.exec(line)
    if (!fields) throw new Error(`Invalid actual process record: ${line}`)
    return { pid: Number(fields[1]), pgid: Number(fields[2]), state: fields[3] }
  })
}

async function reclaimCapturedGroup(identity) {
  const rows = processRows()
  const leader = rows.find(row => row.pid === identity.pid)
  if (leader && leader.pgid !== identity.pgid) throw new Error('Captured leader PID no longer belongs to its captured group')
  const descendant = rows.find(row => row.pid === identity.childPid)
  if (descendant && descendant.pgid !== identity.pgid) throw new Error('Captured descendant no longer belongs to its captured group')
  if (!leader && !descendant) return
  try {
    process.kill(-identity.pgid, 'SIGKILL')
  } catch (error) {
    if (error.code !== 'ESRCH') throw error
  }
  const deadline = Date.now() + SIGKILL_GRACE_MS
  while (processRows().some(row => row.pgid === identity.pgid && !/^[ZX]/.test(row.state))) {
    if (Date.now() >= deadline) throw new Error('Captured process group survived its cleanup deadline')
    await delay(20)
  }
}

export function registerSupervisedToolReclamationTests() {
  test('WHAT[verification-system-006] silence termination reclaims an actual detached tool and its descendant while preserving the caller\'s foreign group', {
    skip: !['linux', 'darwin'].includes(process.platform),
  }, async () => {
    const directory = fs.mkdtempSync(path.join(os.tmpdir(), 'supervised-tool-reclamation-'))
    const ownershipPath = path.join(directory, 'tool-ownership.json')
    const foreignOwnershipPath = path.join(directory, 'foreign-ownership.json')
    const caughtPath = path.join(directory, 'caught.json')
    const cleanupPath = path.join(directory, 'foreign-cleanup.json')
    const toolPath = path.join(directory, 'held-tool.mjs')
    const foreignPath = path.join(directory, 'foreign.mjs')
    const heldPath = path.join(directory, 'held.fixture.mjs')
    const launcherPath = path.join(directory, 'supervise.mjs')
    let launcher
    let launcherDrained
    let failure
    const cleanupErrors = []
    try {
      fs.writeFileSync(toolPath, `import fs from 'node:fs'
import { execFileSync, spawn } from 'node:child_process'
const child = spawn(process.execPath, ['-e', ${JSON.stringify(`require('node:fs').watch(${JSON.stringify(directory)}, () => {}); process.stdout.write('ready\\n')`)}], { stdio: ['ignore', 'pipe', 'inherit'] })
child.stdout.once('data', () => {
  const pgid = Number(execFileSync('ps', ['-o', 'pgid=', '-p', String(process.pid)], { encoding: 'utf8' }).trim())
  const childPgid = Number(execFileSync('ps', ['-o', 'pgid=', '-p', String(child.pid)], { encoding: 'utf8' }).trim())
  const identity = { pid: process.pid, pgid, childPid: child.pid, childPgid }
  fs.writeFileSync(${JSON.stringify(`${ownershipPath}.tmp`)}, JSON.stringify(identity))
  fs.renameSync(${JSON.stringify(`${ownershipPath}.tmp`)}, ${JSON.stringify(ownershipPath)})
})
`)
      fs.writeFileSync(foreignPath, `import fs from 'node:fs'
import { execFileSync } from 'node:child_process'
fs.watch(${JSON.stringify(directory)}, () => {})
const pgid = Number(execFileSync('ps', ['-o', 'pgid=', '-p', String(process.pid)], { encoding: 'utf8' }).trim())
const identity = { pid: process.pid, pgid }
fs.writeFileSync(${JSON.stringify(foreignOwnershipPath)}, JSON.stringify(identity))
process.stdout.write(JSON.stringify(identity) + '\\n')
`)
      const probeUrl = new URL('../../../../scripts/lib/verification-tool-probe.mjs', import.meta.url).href
      fs.writeFileSync(heldPath, `import fs from 'node:fs'
import test from 'node:test'
import { runVerificationToolProbe } from ${JSON.stringify(probeUrl)}
test('actual detached selected tool remains unfinished', async () => {
  fs.writeFileSync(${JSON.stringify(path.join(directory, 'suite.json'))}, JSON.stringify({ innerPid: process.ppid, home: process.env.HOME }))
  await runVerificationToolProbe(process.execPath, [${JSON.stringify(toolPath)}], { cwd: ${JSON.stringify(directory)}, env: process.env })
})
`)
      const supervisorUrl = new URL('../e2e/support/supervise-node-test.mjs', import.meta.url).href
      fs.writeFileSync(launcherPath, `import fs from 'node:fs'
import { execFileSync, spawn } from 'node:child_process'
import { superviseNodeTest } from ${JSON.stringify(supervisorUrl)}
${processRows.toString()}
const PROCESS_TREE_TIMEOUT_MS = ${PROCESS_TREE_TIMEOUT_MS}
const foreign = spawn(process.execPath, [${JSON.stringify(foreignPath)}], { detached: true, stdio: ['ignore', 'pipe', 'pipe'] })
const foreignDrained = new Promise((resolve, reject) => {
  foreign.once('error', reject)
  foreign.once('close', (code, signal) => resolve({ code, signal }))
})
const foreignSettled = Promise.allSettled([foreignDrained])
let originalFailure
const cleanupErrors = []
try {
  const identity = await new Promise((resolve, reject) => {
    let output = ''
    foreign.stdout.setEncoding('utf8').on('data', chunk => {
      output += chunk
      if (!output.endsWith('\\n')) return
      try {
        resolve(JSON.parse(output))
      } catch (error) {
        reject(error)
      }
    })
    foreign.once('error', reject)
    foreign.once('close', () => reject(new Error('Foreign group exited before its ready observation')))
    foreign.stderr.resume()
  })
  try {
    await superviseNodeTest({ files: [${JSON.stringify(heldPath)}], label: 'detached-tool-reclamation', silenceMs: 1000, throwOnFailure: true })
    throw new Error('Held tool unexpectedly completed')
  } catch (error) {
    originalFailure = { error }
    const tool = JSON.parse(fs.readFileSync(${JSON.stringify(ownershipPath)}, 'utf8'))
    const suite = JSON.parse(fs.readFileSync(${JSON.stringify(path.join(directory, 'suite.json'))}, 'utf8'))
    const rows = processRows()
    fs.writeFileSync(${JSON.stringify(caughtPath)}, JSON.stringify({
      failure: { name: error?.name, message: error?.message },
      tool, foreign: identity,
      toolMembers: rows.filter(row => row.pgid === tool.pgid && !/^[ZX]/.test(row.state)),
      foreignMembers: rows.filter(row => row.pgid === identity.pgid && !/^[ZX]/.test(row.state)),
      homeExists: fs.existsSync(suite.home),
    }))
  }
} catch (error) {
  if (originalFailure) cleanupErrors.push(error)
  else originalFailure = { error }
} finally {
  try {
    if (foreign.pid) process.kill(-foreign.pid, 'SIGKILL')
  } catch (error) {
    if (error.code !== 'ESRCH') cleanupErrors.push(error)
  }
  const [outcome] = await foreignSettled
  if (outcome.status === 'rejected') cleanupErrors.push(outcome.reason)
  try {
    fs.writeFileSync(${JSON.stringify(cleanupPath)}, JSON.stringify({ pid: foreign.pid, live: processRows().filter(row => row.pgid === foreign.pid && !/^[ZX]/.test(row.state)) }))
  } catch (error) {
    cleanupErrors.push(error)
  }
}
if (cleanupErrors.length > 0) {
  throw new AggregateError([...(originalFailure ? [originalFailure.error] : []), ...cleanupErrors], 'Supervisor observation or foreign cleanup failed', { cause: originalFailure?.error })
}
if (originalFailure) throw originalFailure.error
`)
      const env = { ...process.env, NODE_TEST_CONCURRENCY: '1', TMPDIR: directory }
      delete env.NODE_TEST_CONTEXT
      launcher = spawn(process.execPath, [launcherPath], { env, stdio: ['ignore', 'pipe', 'pipe'] })
      let output = ''
      launcher.stdout.setEncoding('utf8').on('data', chunk => { output += chunk })
      launcher.stderr.setEncoding('utf8').on('data', chunk => { output += chunk })
      launcherDrained = Promise.allSettled([new Promise((resolve, reject) => {
        launcher.once('error', reject)
        launcher.once('close', (code, signal) => resolve({ code, signal }))
      })])
      const [outcome] = await launcherDrained
      assert.equal(outcome.status, 'fulfilled')
      assert.deepEqual(outcome.value, { code: 1, signal: null }, output)
      assert.match(output, /WATCHDOG: 'detached-tool-reclamation' silent for/)
      assert.ok(fs.existsSync(caughtPath), `The caller did not record its caught failure\n${output}`)
      const caught = JSON.parse(fs.readFileSync(caughtPath, 'utf8'))
      assert.match(caught.failure.message, /supervised suite failed/)
      assert.equal(caught.tool.pid, caught.tool.pgid, 'The actual tool is an independent detached group leader')
      assert.equal(caught.tool.childPgid, caught.tool.pgid, 'The actual descendant was observed in its tool owner\'s group before termination')
      assert.notEqual(caught.tool.pgid, caught.foreign.pgid)
      assert.ok(caught.foreignMembers.some(row => row.pid === caught.foreign.pid), 'The unrelated caller-owned group remains alive when silence is caught')
      assert.equal(caught.homeExists, false, 'The exact inner HOME was reclaimed before its caller caught failure')
      assert.deepEqual(caught.toolMembers, [], `The actual detached tool group must already be empty when its caller catches failure\n${output}`)
      assert.deepEqual(JSON.parse(fs.readFileSync(cleanupPath, 'utf8')).live, [])
    } catch (error) {
      failure = { error }
    } finally {
      if (launcher && launcher.exitCode === null && launcher.signalCode === null) {
        try {
          launcher.kill('SIGKILL')
        } catch (error) {
          cleanupErrors.push(error)
        }
      }
      if (launcherDrained) {
        const [outcome] = await launcherDrained
        if (outcome.status === 'rejected') cleanupErrors.push(outcome.reason)
      }
      for (const markerPath of [ownershipPath, foreignOwnershipPath]) {
        if (!fs.existsSync(markerPath)) continue
        try {
          await reclaimCapturedGroup(JSON.parse(fs.readFileSync(markerPath, 'utf8')))
        } catch (error) {
          cleanupErrors.push(error)
        }
      }
      if (cleanupErrors.length === 0) {
        try {
          fs.rmSync(directory, { recursive: true, force: true })
        } catch (error) {
          cleanupErrors.push(error)
        }
      }
    }
    if (cleanupErrors.length > 0) {
      throw new AggregateError([...(failure ? [failure.error] : []), ...cleanupErrors], `Detached tool verification and cleanup failed; retained ownership evidence: ${directory}`, { cause: failure?.error })
    }
    if (failure) throw failure.error
  })
}
