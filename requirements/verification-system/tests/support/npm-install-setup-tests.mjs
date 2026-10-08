import assert from 'node:assert/strict'
import childProcess, { execFileSync, spawn } from 'node:child_process'
import fs from 'node:fs'
import { syncBuiltinESMExports } from 'node:module'
import path from 'node:path'
import { Readable } from 'node:stream'
import test from 'node:test'
import { fileURLToPath } from 'node:url'
import { installVerificationDependencies } from '../../../../scripts/lib/verification-npm-candidate.mjs'
import { createNpmInstallFixture } from './npm-install-fixture.mjs'

const selfPath = fileURLToPath(import.meta.url)
const monitorPath = fileURLToPath(new URL('../../../../scripts/lib/verification-tool-monitor.mjs', import.meta.url))

function nativeRows() {
  return execFileSync('/bin/ps', ['-eo', 'pid=,ppid=,pgid=,stat='], { encoding: 'utf8', timeout: 1000 })
    .trim().split('\n').map(line => {
      const fields = /^\s*(\d+)\s+(\d+)\s+(\d+)\s+(\S+)\s*$/.exec(line)
      assert.ok(fields, `Invalid native process row: ${line}`)
      return { pid: Number(fields[1]), parentPid: Number(fields[2]), pgid: Number(fields[3]), state: fields[4] }
    }).filter(row => !/^[ZX]/.test(row.state))
}

function awaitHeldRequest(marker) {
  execFileSync(process.execPath, ['--input-type=module', '-e', `
import fs from 'node:fs'
import path from 'node:path'
const marker = process.argv[1]
let settled = false
const watcher = fs.watch(path.dirname(marker), check)
const deadline = setTimeout(() => { watcher.close(); throw new Error('Real npm ci did not reach the held tarball') }, 3000)
function check() {
  if (settled || !fs.existsSync(marker)) return
  settled = true
  clearTimeout(deadline)
  watcher.close()
}
check()
`, marker], { timeout: 4000 })
}

async function installerScenario(config) {
  const originalSpawn = childProcess.spawn
  const originalSetEncoding = Readable.prototype.setEncoding
  const originalRemove = fs.rmSync
  const reason = config.nullReason ? null : new Error('Forwarded real npm ci stream setup failed')
  const records = []
  const finish = new Promise(resolve => process.once('message', message => {
    assert.equal(message.type, 'finish')
    resolve()
  }))
  let ci
  let ownership
  let disposal
  let failure
  const cleanupErrors = []
  try {
    childProcess.spawn = function (executable, argv, options) {
      const child = Reflect.apply(originalSpawn, this, [executable, argv, options])
      if (executable === process.execPath && argv[0] === monitorPath) {
        const record = { child, argv, stdout: '', terminal: null, closed: null }
        record.drained = new Promise(resolve => child.once('close', (code, signal) => {
          record.closed = { code, signal }
          resolve()
        }))
        child.stdout.on('data', bytes => { record.stdout += bytes.toString() })
        child.on('message', message => {
          if (message.type === 'verification-tool-terminal') record.terminal = message
        })
        records.push(record)
        if (argv[2] === config.options.npmCli && argv[3] === 'ci') ci = record
      }
      return child
    }
    syncBuiltinESMExports()
    fs.rmSync = function (target, options) {
      if (target === ownership?.installationRoot) {
        disposal = {
          terminal: ci.terminal, monitorClosed: ci.closed,
          groups: nativeRows().filter(row => [ownership.monitor.pgid, ownership.tool.pgid].includes(row.pgid)),
        }
      }
      return Reflect.apply(originalRemove, this, [target, options])
    }
    Readable.prototype.setEncoding = function (...args) {
      const result = Reflect.apply(originalSetEncoding, this, args)
      if (!ci || this !== ci.child.stdout) return result
      Readable.prototype.setEncoding = originalSetEncoding
      awaitHeldRequest(config.heldMarker)
      const rows = nativeRows()
      const monitor = rows.find(row => row.pid === ci.child.pid && row.parentPid === process.pid)
      const children = rows.filter(row => row.parentPid === ci.child.pid)
      assert.ok(monitor, 'The actual ci monitor must exist before the setup fault')
      assert.equal(children.length, 1, 'The held ci is the actual monitor child')
      const tool = children[0]
      assert.equal(monitor.pgid, monitor.pid)
      assert.equal(tool.pgid, tool.pid)
      const installationRoot = path.dirname(path.dirname(ci.argv[1]))
      assert.equal(fs.existsSync(installationRoot), true)
      ownership = { monitor, tool, installationRoot }
      fs.writeFileSync(config.ownershipMarker, JSON.stringify(ownership))
      throw reason
    }
    let rejected = { received: false }
    try {
      await installVerificationDependencies(config.options)
    } catch (error) {
      rejected = { received: true, error }
    }
    assert.equal(rejected.received, true)
    assert.equal(rejected.error, reason, 'The original setup Error or null remains the public rejection')
    assert.ok(ownership, 'The injection follows real ci creation and its held registry request')
    assert.equal(records.length, 3)
    assert.deepEqual(JSON.parse(records[0].stdout), { platform: process.platform, arch: process.arch })
    assert.equal(records[1].stdout.trim(), config.options.expectedNpmVersion)
    for (const record of records.slice(0, 2)) {
      assert.deepEqual(record.terminal, { type: 'verification-tool-terminal', version: 1, exitCode: 0, signal: null, failures: [] })
      assert.deepEqual(record.closed, { code: 0, signal: null })
    }
    const rows = nativeRows()
    await new Promise((resolve, reject) => process.send({
      type: 'caught', ownership,
      groups: rows.filter(row => [ownership.monitor.pgid, ownership.tool.pgid].includes(row.pgid)),
      terminal: ci.terminal, monitorClosed: ci.closed, disposal,
      installationExists: fs.existsSync(ownership.installationRoot),
      candidates: fs.readdirSync(config.options.parentDirectory),
      reasonPreserved: rejected.error === reason,
    }, error => error ? reject(error) : resolve()))
    await finish
  } catch (error) {
    failure = { error }
  } finally {
    childProcess.spawn = originalSpawn
    syncBuiltinESMExports()
    Readable.prototype.setEncoding = originalSetEncoding
    fs.rmSync = originalRemove
    for (const record of records) {
      try {
        if (record.child.exitCode === null && record.child.signalCode === null) record.child.stdin.destroy()
        await record.drained
      } catch (error) {
        cleanupErrors.push(error)
      }
    }
    if (ownership) {
      try {
        assert.deepEqual(nativeRows().filter(row => [ownership.monitor.pgid, ownership.tool.pgid].includes(row.pgid)), [], 'Final fixture cleanup drains the actual captured ci and monitor groups')
      } catch (error) {
        cleanupErrors.push(error)
      }
    }
    if (process.connected) process.disconnect()
  }
  if (cleanupErrors.length) throw new AggregateError([...(failure ? [failure.error] : []), ...cleanupErrors], 'Npm setup fixture cleanup failed', { cause: failure?.error })
  if (failure) throw failure.error
}

export function registerNpmInstallSetupTests() {
  for (const nullReason of [false, true]) {
    test(`WHAT[verification-system-016] actual held npm ci setup ${nullReason ? 'null' : 'Error'} settles its creation owner before installer disposal`, {
      skip: !['linux', 'darwin'].includes(process.platform),
    }, async () => {
      const fixture = await createNpmInstallFixture()
      const heldMarker = path.join(fixture.root, 'held-request.json')
      const ownershipMarker = path.join(fixture.root, 'ci-owner.json')
      const foreign = spawn(process.execPath, ['--input-type=module', '-e', `
import fs from 'node:fs'
fs.watch(${JSON.stringify(fixture.root)}, () => {})
process.stdout.write('ready\\n')
`], { detached: true, stdio: ['ignore', 'pipe', 'pipe'] })
      const foreignClosed = new Promise((resolve, reject) => {
        foreign.once('close', (code, signal) => resolve({ code, signal }))
        foreign.once('error', reject)
      })
      let child
      let childClosed
      let failure
      let output = ''
      const cleanupErrors = []
      try {
        await new Promise((resolve, reject) => {
          foreign.stdout.once('data', bytes => {
            assert.equal(bytes.toString(), 'ready\n')
            resolve()
          })
          foreign.once('error', reject)
        })
        const foreignIdentity = nativeRows().find(row => row.pid === foreign.pid)
        assert.ok(foreignIdentity)
        assert.equal(foreignIdentity.pgid, foreign.pid)
        fixture.holdLeaf()
        const observedRequest = fixture.leafRequest.then(() => {
          fs.writeFileSync(heldMarker, JSON.stringify(fixture.requests))
        })
        const env = { ...process.env }
        delete env.NODE_TEST_CONTEXT
        child = spawn(process.execPath, [selfPath, '--installer-scenario', JSON.stringify({ options: fixture.options, heldMarker, ownershipMarker, nullReason })], {
          env, stdio: ['ignore', 'pipe', 'pipe', 'ipc'],
        })
        child.stdout.on('data', bytes => { output += bytes.toString() })
        child.stderr.on('data', bytes => { output += bytes.toString() })
        childClosed = new Promise((resolve, reject) => {
          child.once('close', (code, signal) => resolve({ code, signal }))
          child.once('error', reject)
        })
        const caught = await Promise.race([
          new Promise(resolve => child.on('message', message => { if (message.type === 'caught') resolve(message) })),
          childClosed.then(outcome => { throw new Error(`Installer closed before the caught observation: ${JSON.stringify(outcome)}\n${output}`) }),
        ])
        await observedRequest
        assert.ok(fixture.requests.some(request => request.path === `/${fixture.leafName}/-/${fixture.leafName}-1.0.0.tgz`))
        assert.equal(caught.reasonPreserved, true)
        assert.equal(caught.installationExists, false)
        assert.deepEqual(caught.candidates, [])
        assert.ok(nativeRows().some(row => row.pid === foreignIdentity.pid && row.pgid === foreignIdentity.pgid), 'The unrelated actual caller-owned process remains alive')
        assert.deepEqual(caught.groups, [], 'Installer rejection and directory disposal must follow actual ci and monitor group settlement')
        assert.deepEqual(caught.terminal, { type: 'verification-tool-terminal', version: 1, exitCode: null, signal: 'SIGKILL', failures: [] })
        assert.deepEqual(caught.monitorClosed, { code: 0, signal: null })
        assert.deepEqual(caught.disposal, {
          terminal: caught.terminal, monitorClosed: caught.monitorClosed, groups: [],
        }, 'The actual installation directory removal begins only after owner terminal, close and native group settlement')
        await fixture.leafClosed
      } catch (error) {
        failure = { error }
      } finally {
        try {
          if (child?.connected) child.send({ type: 'finish' })
          if (childClosed) assert.deepEqual(await childClosed, { code: 0, signal: null }, output)
        } catch (error) {
          cleanupErrors.push(error)
        }
        try {
          if (foreign.exitCode === null && foreign.signalCode === null) foreign.kill('SIGKILL')
          await foreignClosed
          assert.deepEqual(nativeRows().filter(row => row.pgid === foreign.pid), [])
        } catch (error) {
          cleanupErrors.push(error)
        }
        try { await fixture.dispose() }
        catch (error) { cleanupErrors.push(error) }
      }
      if (cleanupErrors.length) throw new AggregateError([...(failure ? [failure.error] : []), ...cleanupErrors], 'Actual npm setup and fixture cleanup failed', { cause: failure?.error })
      if (failure) throw failure.error
    })
  }
}

if (process.argv[1] === selfPath && process.argv[2] === '--installer-scenario') {
  await installerScenario(JSON.parse(process.argv[3]))
}
