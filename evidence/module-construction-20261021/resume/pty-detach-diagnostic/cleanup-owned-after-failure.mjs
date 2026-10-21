import assert from 'node:assert/strict'
import fs from 'node:fs'
import path from 'node:path'
import { spawnSync } from 'node:child_process'
import { createHash } from 'node:crypto'

const root = '/private/tmp/vibe-fs-pty-detach-diagnostic-20261021'
const status = JSON.parse(fs.readFileSync(root + '/status.json'))
const controller = JSON.parse(fs.readFileSync(root + '/controller.json'))
const request = JSON.parse(fs.readFileSync(root + '/physical/recovery/request'))
const pids = [...new Set([controller.pid, controller.harnessPid, ...status.ownedPids])]
const ps = spawnSync('/bin/ps', ['-p', pids.join(','), '-o', 'pid=,ppid=,pgid=,rss=,stat=,comm='], { encoding: 'utf8', timeout: 5000 })
const fd = spawnSync('/usr/sbin/lsof', ['-a', '-p', pids.join(','), '-F', 'pf'], { encoding: 'utf8', timeout: 10000 })
assert.equal(ps.status, 1)
assert.equal(ps.stdout.trim(), '')
assert.equal(fd.status, 1)
assert.equal(fd.stdout.trim(), '')
const scope = status.scope.directory
assert.ok(scope.startsWith(root + '/tmp/wanxiangshu-pty-'))
assert.ok(path.resolve(request.endpoint).startsWith(root + '/tmp/wxs-pty-'))
const files = fs.readdirSync(scope).sort().map(name => {
  const file = scope + '/' + name
  assert.equal(fs.statSync(file).isFile(), true)
  return { name, bytes: fs.statSync(file).size, sha256: createHash('sha256').update(fs.readFileSync(file)).digest('hex') }
})
const preserved = root + '/retained-scope-before-cleanup'
assert.equal(fs.existsSync(preserved), false)
fs.cpSync(scope, preserved, { recursive: true })
const endpointExisted = fs.existsSync(request.endpoint)
if (endpointExisted) {
  assert.equal(fs.lstatSync(request.endpoint).isSocket(), true)
  fs.unlinkSync(request.endpoint)
}
if (fs.existsSync(path.dirname(request.endpoint))) fs.rmdirSync(path.dirname(request.endpoint))
fs.rmSync(scope, { recursive: true })
const receipt = { at: new Date().toISOString(), outcome: 'Failed original PTY endurance experiment; owned cleanup verified, no retry', originalState: status.state, originalError: status.error, originalExit: JSON.parse(fs.readFileSync(root + '/controller-exit.json')), ownedPids: pids, ps: { exitCode: ps.status, stdout: ps.stdout, stderr: ps.stderr }, lsof: { exitCode: fd.status, stdout: fd.stdout, stderr: fd.stderr }, allRecordedPidsAbsent: true, allRecordedPidFdsAbsent: true, scope: { original: scope, removed: !fs.existsSync(scope), preserved, files }, controlEndpoint: { path: request.endpoint, staleSocketAfterForcedKill: endpointExisted, removed: !fs.existsSync(request.endpoint), parentRemoved: !fs.existsSync(path.dirname(request.endpoint)) }, terminalReceiptExists: fs.existsSync(root + '/physical/recovery/terminal'), retainedDurableRecoveryFiles: fs.readdirSync(root + '/physical/recovery').sort(), remainingTmp: fs.readdirSync(root + '/tmp').sort(), limits: 'No terminal close proof was obtained. Absence of owned processes is independently verified after fallback signals, not inferred from a stop request. Durable files and Bun cache retained as evidence.' }
fs.writeFileSync(root + '/cleanup-receipt.json', JSON.stringify(receipt, null, 2) + '\n')
console.log(JSON.stringify(receipt, null, 2))
