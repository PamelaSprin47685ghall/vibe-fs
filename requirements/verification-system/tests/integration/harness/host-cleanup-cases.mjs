import assert from 'node:assert/strict'
import { execFile, execFileSync, spawn } from 'node:child_process'
import fs from 'node:fs'
import os from 'node:os'
import path from 'node:path'
import { promisify } from 'node:util'
import { terminateTree } from '../../e2e/support/process-lifecycle.js'
import { PROCESS_TREE_TIMEOUT_MS, WATCHDOG_TIMEOUT_MS } from '../../e2e/support/time-budget.js'

const execFileAsync = promisify(execFile)
const hostModule = new URL('../../e2e/support/process-host.js', import.meta.url).href
const scenarioModule = new URL('../../e2e/support/scenario-parallel.js', import.meta.url).href
const driverModule = new URL('../../e2e/support/scenario-driver.mjs', import.meta.url).href
const fixture = new URL('../../support/fixtures/fatal-host.fixture.mjs', import.meta.url).href

function processRows() {
  return execFileSync('/bin/ps', ['-eo', 'pid=,pgid=,stat='], {
    encoding: 'utf8', timeout: PROCESS_TREE_TIMEOUT_MS,
  }).trim().split('\n').map(line => {
    const fields = /^\s*(\d+)\s+(\d+)\s+(\S+)\s*$/.exec(line)
    assert.ok(fields, line)
    return { pid: Number(fields[1]), group: Number(fields[2]), state: fields[3] }
  }).filter(row => !/^[ZX]/.test(row.state))
}

async function hostCleanupCase(mode) {
  const directory = fs.mkdtempSync(path.join(os.tmpdir(), 'forced-host-cleanup-'))
  const receiptPath = path.join(directory, 'owned.json')
  const executable = path.join(directory, 'host.mjs')
  fs.writeFileSync(executable, `#!${process.execPath}\nawait import(${JSON.stringify(fixture)})\n`, { mode: 0o755 })
  const foreign = spawn(process.execPath, ['-e', `require('node:fs').watch(${JSON.stringify(directory)}, () => {})`], {
    detached: true, stdio: 'ignore',
  })
  let foreignStarted = false
  let failure
  const cleanupErrors = []
  try {
    await new Promise((accept, reject) => {
      foreign.once('spawn', () => { foreignStarted = true; accept() })
      foreign.once('error', reject)
    })
    const env = { ...process.env, OPENCODE_BIN: executable, WXS_FATAL_HOST_RECEIPT: receiptPath }
    if (mode === 'failure') {
      fs.writeFileSync(path.join(directory, 'ps'), `#!${process.execPath}
import fs from 'node:fs'
const counter = ${JSON.stringify(path.join(directory, 'inspections'))}
const count = fs.existsSync(counter) ? Number(fs.readFileSync(counter, 'utf8')) + 1 : 1
fs.writeFileSync(counter, String(count))
process.stderr.write('controlled-native-inspection-' + count + '\\n')
process.exit(17)
`, { mode: 0o755 })
      env.PATH = directory + path.delimiter + (env.PATH ?? '')
    }
    const subject = mode === 'join' ? `
import assert from 'node:assert/strict'
import { ProcessHost } from ${JSON.stringify(hostModule)}
const host = new ProcessHost()
await host.start({ scenarioDir:${JSON.stringify(path.join(directory, 'scenario'))}, providerUrl:'http://127.0.0.1:1/v1', pluginPaths:[] })
const first = host.stop({force:true})
const second = host.stop({force:true})
assert.equal(first, second, 'Both callers own the same actual forced stop')
await Promise.all([first, second])
assert.equal(host.pid, null)
process.stdout.write('[forced-host-result] joined\\n')
` : mode === 'flow-failure' ? `
import { runCanary } from ${JSON.stringify(driverModule)}
const code = await runCanary('long-stroke', { preFlow(scenario) {
  const failed = Promise.withResolvers()
  const onFatal = scenario.provider.onFatal
  scenario.provider.onFatal = (fatal, error) => {
    onFatal(fatal, error)
    failed.reject(new Error('controlled-independent-preFlow-failure'))
  }
  fetch(scenario.provider.url + '/v1/chat/completions', {
    method:'POST', headers:{'content-type':'application/json'},
    body:JSON.stringify({model:'test-model',messages:[{role:'user',content:'undeclared-fatal-input'}]})
  }).catch(() => {})
  return failed.promise
} })
process.exitCode = code
` : `
import { setupScenarioParallel } from ${JSON.stringify(scenarioModule)}
const scenario = await setupScenarioParallel({plugin:false}, () => ${JSON.stringify(path.join(directory, 'scenario'))})
fetch(scenario.provider.url + '/v1/chat/completions', {
  method:'POST', headers:{'content-type':'application/json'},
  body:JSON.stringify({model:'test-model',messages:[{role:'user',content:'undeclared-fatal-input'}]})
}).catch(() => {})
`
    let outcome
    try {
      const output = await execFileAsync(process.execPath, ['--input-type=module', '-e', subject], {
        env, timeout: WATCHDOG_TIMEOUT_MS, killSignal: 'SIGKILL',
      })
      outcome = { code: 0, signal: null, ...output }
    } catch (error) {
      outcome = { code: error.code, signal: error.signal, stdout: error.stdout, stderr: error.stderr }
    }
    assert.equal(outcome.signal, null, outcome.stderr)
    assert.equal(outcome.code, mode === 'join' ? 0 : 1, outcome.stderr)
    const owned = JSON.parse(fs.readFileSync(receiptPath, 'utf8'))
    const rows = processRows()
    assert.deepEqual(rows.filter(row => row.group === owned.root), [], 'The actual serve group has exited before the caller continues')
    assert.ok(rows.some(row => row.pid === foreign.pid && row.group === foreign.pid), 'The foreign group remains alive')
    if (mode === 'failure') {
      assert.match(outcome.stderr, /FIRST SCRIPT MISMATCH/)
      assert.match(outcome.stderr, /Fatal scenario failure and owned cleanup failed/)
      assert.match(outcome.stderr, /controlled-native-inspection-1/)
      assert.match(outcome.stderr, /controlled-native-inspection-2/)
      assert.ok(rows.some(row => row.pid === owned.guardian && row.group === owned.guardian), 'Failed inventory cannot authorize an unknown detached group')
    } else {
      assert.deepEqual(rows.filter(row => row.group === owned.guardian), [], 'The real guardian and its target have drained')
      if (mode === 'join') assert.match(outcome.stdout, /\[forced-host-result\] joined/)
      else assert.match(outcome.stderr, /FIRST SCRIPT MISMATCH/)
      if (mode === 'flow-failure') {
        assert.match(outcome.stderr, /Scenario execution and fatal cleanup failed/)
        assert.match(outcome.stderr, /controlled-independent-preFlow-failure/)
      }
    }
  } catch (error) {
    failure = { error }
  } finally {
    const groups = foreignStarted ? [{ group: foreign.pid, pids: [foreign.pid] }] : []
    try {
      if (fs.existsSync(receiptPath)) {
        const owned = JSON.parse(fs.readFileSync(receiptPath, 'utf8'))
        groups.push({ group: owned.root, pids: [owned.root] }, { group: owned.guardian, pids: [owned.guardian, owned.target] })
      }
    } catch (error) { cleanupErrors.push(error) }
    for (const { group, pids } of groups) {
      try {
        const members = processRows().filter(row => row.group === group)
        if (members.length === 0) continue
        assert.ok(members.some(row => pids.includes(row.pid)), 'Fixture cleanup requires its exact original process witness')
        await terminateTree(group)
      } catch (error) { cleanupErrors.push(error) }
    }
    try {
      assert.deepEqual(processRows().filter(row => groups.some(owned => owned.group === row.group)), [], 'Every known fixture group is physically empty after cleanup')
    } catch (error) { cleanupErrors.push(error) }
    try { fs.rmSync(directory, { recursive: true, force: true }) } catch (error) { cleanupErrors.push(error) }
  }
  if (failure && cleanupErrors.length) throw new AggregateError([failure.error, ...cleanupErrors], 'Host cleanup proof and fixture reclamation failed', { cause: failure.error })
  if (failure) throw failure.error
  if (cleanupErrors.length) throw new AggregateError(cleanupErrors, 'Host cleanup fixture reclamation failed', { cause: cleanupErrors[0] })
}

export const hostCleanupCases = [
  { name: 'WHAT[verification-system-006] forced Host stop joins one actual guardian reclamation and preserves a foreign group', fn: () => hostCleanupCase('join') },
  { name: 'WHAT[verification-system-005] first fatal mismatch drains its actual guardian before process exit', fn: () => hostCleanupCase('fatal') },
  { name: 'WHAT[verification-system-005] an independent preFlow error survives the real fatal cleanup failure boundary', fn: () => hostCleanupCase('flow-failure') },
  { name: 'WHAT[verification-system-005] fatal mismatch preserves both native inspection failures without claiming unknown groups', fn: () => hostCleanupCase('failure') },
]
