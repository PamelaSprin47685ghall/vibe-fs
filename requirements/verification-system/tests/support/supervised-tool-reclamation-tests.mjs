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
  const scenarios = [
    { phase: null, title: 'silence termination reclaims an actual detached tool and its descendant while preserving the caller\'s foreign group' },
    { phase: 'freeze-confirmation', title: 'native freeze inspection failure preserves its original cause and drains known owned resources while retaining a foreign group' },
    { phase: 'descendant-drain', title: 'native drain inspection failure preserves its original cause and drains captured owned resources while retaining a foreign group' },
  ]
  for (const scenario of scenarios) registerReclamationTest(scenario)
}

function registerReclamationTest(scenario) {
  test(`WHAT[verification-system-006] ${scenario.title}`, {
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
  const monitorPgid = Number(execFileSync('ps', ['-o', 'pgid=', '-p', String(process.ppid)], { encoding: 'utf8' }).trim())
  const identity = { pid: process.pid, pgid, childPid: child.pid, childPgid, monitorPid: process.ppid, monitorPgid }
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
const failingPhase = ${JSON.stringify(scenario.phase)}
const inspections = []
let inspectionFailure
function inspectProcessTree({ phase, timeout }) {
  if (phase === failingPhase && !inspectionFailure) {
    try {
      return execFileSync('/bin/ps', ['-eo', 'invalid_verification_column='], { encoding: 'utf8', timeout })
    } catch (error) {
      inspectionFailure = error
      inspections.push({ phase, failed: true, status: error.status, pid: error.pid })
      throw error
    }
  }
  const output = execFileSync('/bin/ps', ['-eo', 'pid=,ppid=,pgid=,stat='], { encoding: 'utf8', timeout })
  const tool = JSON.parse(fs.readFileSync(${JSON.stringify(ownershipPath)}, 'utf8'))
  const rows = output.trim().split('\\n').map(line => {
    const fields = /^\\s*(\\d+)\\s+(\\d+)\\s+(\\d+)\\s+(\\S+)\\s*$/.exec(line)
    if (!fields) throw new Error('Invalid actual process record during the inspection fixture')
    return { pid: Number(fields[1]), parent: Number(fields[2]), pgid: Number(fields[3]), state: fields[4] }
  })
  inspections.push({ phase, failed: false, rows: rows.filter(row => [tool.pid, tool.childPid, tool.monitorPid].includes(row.pid)) })
  return output
}
function containsFailure(error, target, seen = new Set()) {
  if (error === target) return true
  if (!error || seen.has(error)) return false
  seen.add(error)
  return containsFailure(error.cause, target, seen) ||
    error instanceof AggregateError && error.errors.some(value => containsFailure(value, target, seen))
}
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
    await superviseNodeTest({ files: [${JSON.stringify(heldPath)}], label: 'detached-tool-reclamation', silenceMs: 1000, throwOnFailure: true,
      ...(failingPhase === null ? {} : { inspectProcessTree }),
    })
    throw new Error('Held tool unexpectedly completed')
  } catch (error) {
    originalFailure = { error }
    const tool = JSON.parse(fs.readFileSync(${JSON.stringify(ownershipPath)}, 'utf8'))
    const suite = JSON.parse(fs.readFileSync(${JSON.stringify(path.join(directory, 'suite.json'))}, 'utf8'))
    const rows = processRows()
    fs.writeFileSync(${JSON.stringify(caughtPath)}, JSON.stringify({
      failure: { name: error?.name, message: error?.message },
      inspections,
      nativeErrorPreserved: inspectionFailure ? containsFailure(error, inspectionFailure) : null,
      tool, foreign: identity,
      toolMembers: rows.filter(row => row.pgid === tool.pgid && !/^[ZX]/.test(row.state)),
      monitorMembers: rows.filter(row => row.pgid === tool.monitorPgid && !/^[ZX]/.test(row.state)),
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
      if (scenario.phase !== null) {
        const initial = caught.inspections.find(inspection => inspection.phase === 'initial-capture' && !inspection.failed)
        assert.ok(initial, 'The real initial process snapshot was obtained before the native failure')
        assert.ok(initial.rows.some(row => row.pid === caught.tool.pid && row.parent === caught.tool.monitorPid && row.pgid === caught.tool.pgid), 'The initial native snapshot contains the actual tool and monitor lineage')
        assert.ok(initial.rows.some(row => row.pid === caught.tool.monitorPid && row.pgid === caught.tool.monitorPgid), 'The initial native snapshot contains the actual monitor group')
        const failedInspection = caught.inspections.find(inspection => inspection.phase === scenario.phase && inspection.failed)
        assert.ok(failedInspection, 'The chosen phase reached a real failing native ps invocation')
        assert.equal(failedInspection.status, 1)
        assert.ok(failedInspection.pid > 0)
        if (scenario.phase === 'descendant-drain') {
          assert.ok(caught.inspections.some(inspection => inspection.phase === 'freeze-confirmation' && !inspection.failed), 'Capture completed its real frozen-group inspection before the drain failure')
        }
        assert.deepEqual({
          nativeErrorPreserved: caught.nativeErrorPreserved,
          toolMembers: caught.toolMembers,
          monitorMembers: caught.monitorMembers,
        }, { nativeErrorPreserved: true, toolMembers: [], monitorMembers: [] }, output)
      }
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
          const identity = JSON.parse(fs.readFileSync(markerPath, 'utf8'))
          await reclaimCapturedGroup(identity)
          if (identity.monitorPid) await reclaimCapturedGroup({ pid: identity.monitorPid, pgid: identity.monitorPgid })
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
