import assert from 'node:assert/strict'
import fs from 'node:fs'
import net from 'node:net'
import { createHash, createHmac, randomUUID } from 'node:crypto'
import { execFileSync, spawnSync } from 'node:child_process'
import { setTimeout as delay } from 'node:timers/promises'
import { fileURLToPath } from 'node:url'
import { ptyRecoveryOpen, ptyRecoveryDrain } from './dist/Process/Surface.js'

const folder = '/private/tmp/vibe-fs-pty-soak-gen100-20261021'
const physical = folder + '/physical'
const recovery = physical + '/recovery'
const target = fileURLToPath(new URL('./target.mjs', import.meta.url))
const minimum = 120 * 60 * 1000
const beginning = performance.now()
const key = randomUUID()
const incarnation = randomUUID()
const owned = new Set()
const status = { state: 'Starting', pid: process.pid, generation: 100, startedAt: new Date().toISOString(), phases: [], requests: 0, outputBytes: 0 }
const hash = createHash('sha256')
const expectedHash = createHash('sha256')
const inputHash = createHash('sha256')
const lines = []
let buffered = ''
let terminal
let request
let custodian
let scopeDirectory
let scopeStarted
let scopeRequest
let finishing = false
let timer
let stopped = false
let rejectFailure
const failure = new Promise((_, reject) => { rejectFailure = reject })
const fail = error => rejectFailure(error)
process.on('SIGTERM', () => fail(Error('External cancellation requested')))
process.on('SIGINT', () => fail(Error('Operator cancellation requested')))
process.on('unhandledRejection', fail)
const json = path => JSON.parse(fs.readFileSync(path, 'utf8'))
const save = () => {
  status.updatedAt = new Date().toISOString()
  status.elapsedMs = performance.now() - beginning
  status.ownedPids = [...owned]
  status.client = terminal?.inspect() ?? null
  fs.writeFileSync(folder + '/status.next', JSON.stringify(status))
  fs.renameSync(folder + '/status.next', folder + '/status.json')
}
const note = (event, value = {}) => {
  fs.appendFileSync(folder + '/events.jsonl', JSON.stringify({ at: new Date().toISOString(), elapsedMs: performance.now() - beginning, event, ...value }) + '\n')
  save()
}
const bound = async (pending, ms, label) => {
  let deadline
  try {
    return await Promise.race([pending, failure, new Promise((_, reject) => { deadline = setTimeout(() => reject(Error('HARNESS_BOUND: ' + label)), ms) })])
  } finally { clearTimeout(deadline) }
}
const until = async (observe, label, ms = 15000) => {
  const end = performance.now() + ms
  while (performance.now() < end) {
    const result = await observe()
    if (result) return result
    await bound(delay(25), 1000, label)
  }
  throw Error('HARNESS_BOUND: ' + label)
}
const live = pid => {
  try {
    const state = execFileSync('/bin/ps', ['-p', String(pid), '-o', 'stat='], { encoding: 'utf8', timeout: 5000 }).trim()
    return state !== '' && !state.startsWith('Z')
  } catch (error) { if (error.status === 1) return false; throw error }
}
const descendants = parent => {
  const sample = spawnSync('/usr/bin/pgrep', ['-P', String(parent)], { encoding: 'utf8', timeout: 5000 })
  assert.ok(sample.status === 0 || sample.status === 1)
  const children = sample.stdout.trim() ? sample.stdout.trim().split(/\s+/).map(Number).filter(pid => pid !== sample.pid) : []
  for (const pid of children) owned.add(pid)
  return children
}
const sample = () => {
  if (custodian && live(custodian)) {
    for (const child of descendants(custodian)) descendants(child)
  }
  const pids = [process.pid, ...owned].filter(pid => pid === process.pid || live(pid))
  const ps = execFileSync('/bin/ps', ['-p', pids.join(','), '-o', 'pid=,ppid=,pgid=,rss=,time=,stat=,comm='], { encoding: 'utf8', timeout: 5000 })
  const descriptors = execFileSync('/usr/sbin/lsof', ['-a', '-p', pids.join(','), '-F', 'pf'], { encoding: 'utf8', timeout: 10000 })
  fs.appendFileSync(folder + '/resources.jsonl', JSON.stringify({ at: new Date().toISOString(), elapsedMs: performance.now() - beginning, pids, ps, descriptors, memory: process.memoryUsage() }) + '\n')
  assert.ok(status.outputBytes < 128 * 1024, 'PTY output exceeded experiment safety envelope')
  if (!finishing && terminal) {
    const state = terminal.inspect()
    assert.equal(state.failure, null)
    assert.equal(state.completion, null)
  }
  save()
}
const reachable = () => new Promise((resolve, reject) => {
  const socket = net.createConnection(request.endpoint)
  socket.setEncoding('utf8')
  let data = ''
  let reason
  const timeout = setTimeout(() => socket.destroy(Error('Socket refusal did not arrive')), 10000)
  socket.on('connect', () => socket.write(JSON.stringify({ kind: 'attach', mode: 'recover', key, incarnation, secret: 'invalid-soak-secret' }) + '\n'))
  socket.on('data', part => {
    data += part
    if (!data.includes('\n')) return
    const frame = JSON.parse(data.slice(0, data.indexOf('\n')))
    assert.equal(frame.kind, 'rejected')
    reason = frame.reason
    socket.end()
  })
  socket.on('error', reject)
  socket.on('close', () => { clearTimeout(timeout); reason ? resolve(reason) : reject(Error('No explicit socket refusal')) })
})
const expectLine = async line => {
  await until(() => lines.length > 0, 'Expected target output line')
  assert.equal(lines.shift(), line)
  expectedHash.update(line + '\n')
}
let exitCode = 1
try {
  save()
  note('artifacts-selected', { manifest: '../gen100-build-manifest.json', runtime: process.versions.bun, installedBunPty: import.meta.resolve('bun-pty'), installedProcess: import.meta.resolve('wanxiangli/process') })
  const quote = value => "'" + value.replaceAll("'", "'\\''") + "'"
  const command = `stty icanon isig -echo -onlcr eof '^D'; exec ${quote(process.execPath)} ${quote(target)} target ${quote(physical)}`
  terminal = ptyRecoveryOpen(recovery, key, incarnation, command, physical)
  terminal.onFault(reason => { if (!finishing) fail(Error(reason)); else note('creator-detached-fault', { reason }) })
  terminal.onNotStarted(reason => fail(Error('PTY did not start: ' + reason)))
  terminal.onExit(result => { if (!finishing) fail(Error('PTY ended during active phase: ' + JSON.stringify(result))) })
  terminal.onData(data => {
    hash.update(data, 'utf8')
    status.outputBytes += Buffer.byteLength(data)
    fs.appendFileSync(folder + '/output.jsonl', JSON.stringify({ at: new Date().toISOString(), text: data }) + '\n')
    buffered += data
    while (buffered.includes('\n')) {
      const delimiter = buffered.indexOf('\n')
      lines.push(buffered.slice(0, delimiter))
      buffered = buffered.slice(delimiter + 1)
    }
  })
  await until(() => terminal.inspect().started, 'Recovery creator startup')
  request = json(recovery + '/request')
  custodian = json(recovery + '/available').owner
  owned.add(custodian)
  scopeDirectory = await until(() => fs.readdirSync(folder + '/tmp').filter(name => name.startsWith('wanxiangshu-pty-')).map(name => folder + '/tmp/' + name).find(path => fs.existsSync(path + '/started')), 'Own PTY scope receipt')
  scopeStarted = json(scopeDirectory + '/started')
  scopeRequest = json(scopeDirectory + '/request')
  owned.add(scopeStarted.owner)
  owned.add(scopeStarted.target)
  status.custodian = custodian
  status.scope = { directory: scopeDirectory, incarnation: scopeStarted.incarnation, owner: scopeStarted.owner, target: scopeStarted.target }
  await expectLine(`READY|${scopeStarted.target}|tty\r`)
  sample()
  assert.equal([...owned].filter(pid => live(pid)).length, 4, 'Expected custodian, scope owner, target and one anchor')
  status.state = 'Running'
  const activeStart = performance.now()
  status.activeStartedAt = new Date().toISOString()
  timer = setInterval(() => { try { sample() } catch (error) { fail(error) } }, 30000)
  note('long-pty-started', { minimumRealMs: minimum, ownedPids: [...owned] })
  while (performance.now() - activeStart < minimum) {
    const sequence = status.requests
    const value = `\uFEFFPTY input ${sequence}: 中🧭\r\nvalue remains data`
    const payload = Buffer.from(value, 'utf8')
    const digest = createHash('sha256').update(payload).digest('hex')
    const line = JSON.stringify({ sequence, value }) + '\n'
    terminal.write(line)
    inputHash.update(line, 'utf8')
    await expectLine(`ACK|${sequence}|${payload.length}|${digest}|${payload.toString('base64')}\r`)
    status.requests += 1
    if (sequence % 5 === 0) {
      assert.match(await bound(reachable(), 12000, 'Live control endpoint refusal'), /live control connection/)
      status.socketChecks = (status.socketChecks ?? 0) + 1
    }
    save()
    const remaining = minimum - (performance.now() - activeStart)
    if (remaining > 0) await bound(delay(Math.min(60000, remaining)), 65000, 'Long PTY pacing')
  }
  status.activeElapsedMs = performance.now() - activeStart
  assert.ok(status.activeElapsedMs >= minimum)
  finishing = true
  status.state = 'Closing'
  note('long-pty-interval-ended', { requests: status.requests, activeElapsedMs: status.activeElapsedMs })
  terminal.write('\x04')
  const eof = await until(() => fs.existsSync(physical + '/target-eof.json') && json(physical + '/target-eof.json'), 'Real canonical terminal EOF')
  owned.add(eof.descendant)
  assert.equal(eof.count, status.requests)
  assert.equal(eof.inputHash, inputHash.digest('hex'))
  await expectLine(`EOF|${status.requests}|${eof.inputHash}\r`)
  await expectLine(`DESC_READY|${eof.descendant}\r`)
  await until(() => fs.existsSync(scopeDirectory + '/target-exit'), 'Exact target exit receipt')
  const targetExit = json(scopeDirectory + '/target-exit')
  assert.equal(targetExit.code, 23)
  assert.equal(targetExit.signal, null)
  assert.equal(fs.existsSync(scopeDirectory + '/scope-ended'), false)
  assert.equal(terminal.inspect().completion, null)
  assert.equal(live(eof.descendant), true)
  fs.copyFileSync(scopeDirectory + '/target-exit', folder + '/observed-target-exit.json')
  assert.equal(lines.length, 0)
  assert.equal(buffered, '')
  assert.equal(hash.digest('hex'), expectedHash.digest('hex'))
  status.realEof = eof
  note('target-exited-scope-retained', { targetExit, descendant: eof.descendant })
  terminal.dispose()
  await bound(delay(100), 1000, 'Creator detach')
  process.kill(custodian, 'SIGSTOP')
  stopped = true
  const independentResume = setTimeout(() => { try { process.kill(custodian, 'SIGCONT') } catch {} }, 12000)
  try {
    let rejection
    const unknownStart = performance.now()
    try { await bound(ptyRecoveryDrain(recovery, key, incarnation, 100), 9000, 'Unknown recovery settlement') } catch (error) { rejection = error }
    assert.ok(rejection)
    assert.doesNotMatch(rejection.message, /HARNESS_BOUND/)
    assert.match(rejection.message, /timed out|unknown|acknowledgement/)
    assert.equal(fs.existsSync(recovery + '/terminal'), false)
    assert.equal(live(custodian), true)
    assert.equal(live(eof.descendant), true)
    status.unknown = { reason: rejection.message, elapsedMs: performance.now() - unknownStart, terminalAbsent: true }
    note('paused-custodian-recovery-remained-unknown', status.unknown)
  } finally {
    clearTimeout(independentResume)
    process.kill(custodian, 'SIGCONT')
    stopped = false
  }
  await until(async () => {
    const reason = await bound(reachable(), 12000, 'Resumed endpoint refusal')
    if (/credentials do not match/.test(reason)) return true
    assert.match(reason, /live control connection/)
    return false
  }, 'Stale recovery controller detach', 15000)
  const recovered = await bound(ptyRecoveryDrain(recovery, key, incarnation, 1000), 15000, 'Final TERM KILL recovery')
  assert.equal(recovered.kind, 'Ended')
  assert.equal(recovered.incarnation, incarnation)
  assert.equal(recovered.stopped, true)
  assert.deepEqual(recovered.exit, { code: 23, signal: null })
  assert.equal(fs.existsSync(physical + '/descendant-signals.jsonl'), true)
  const stored = json(recovery + '/terminal')
  assert.equal(stored.proof, createHmac('sha256', request.secret).update(JSON.stringify(stored.receipt)).digest('hex'))
  assert.equal(stored.receipt.key, key)
  assert.equal(stored.receipt.incarnation, incarnation)
  assert.deepEqual(stored.receipt.result.exit, { code: 23, signal: null })
  await until(() => [...owned].every(pid => !live(pid)), 'All recorded physical PTY members gone', 20000)
  await until(() => !fs.existsSync(request.endpoint), 'Control endpoint removed')
  status.recovered = recovered
  status.terminalProofVerified = true
  status.state = 'Passed'
  note('long-pty-completed', { recovered, ownedPids: [...owned] })
  sample()
  exitCode = 0
} catch (error) {
  status.state = 'Failed'
  status.error = { message: error.message, stack: error.stack }
  note('failed', status.error)
} finally {
  clearInterval(timer)
  try {
    if (fs.existsSync(recovery + '/available')) owned.add(json(recovery + '/available').owner)
    for (const name of fs.readdirSync(folder + '/tmp').filter(name => name.startsWith('wanxiangshu-pty-'))) {
      const path = folder + '/tmp/' + name
      if (fs.existsSync(path + '/request')) {
        const observed = json(path + '/request')
        if (Number.isInteger(observed.owner) && observed.owner > 1) owned.add(observed.owner)
      }
      if (fs.existsSync(path + '/started')) {
        const observed = json(path + '/started')
        owned.add(observed.owner)
        owned.add(observed.target)
      }
      if (fs.existsSync(path + '/target-ready')) owned.add(json(path + '/target-ready').pid)
    }
    for (const pid of [...owned]) if (live(pid)) descendants(pid)
  } catch (error) { note('cleanup-discovery-error', { message: error.message }) }
  save()
  if (stopped && custodian) { try { process.kill(custodian, 'SIGCONT') } catch {} }
  if (exitCode !== 0) {
    try { terminal?.dispose() } catch {}
    try { if (request) await Promise.race([ptyRecoveryDrain(recovery, key, incarnation, 100), delay(7000)]) } catch (error) { note('cleanup-recovery-error', { message: error.message }) }
    for (const pid of owned) { try { if (live(pid)) process.kill(pid, 'SIGCONT') } catch {} }
    for (const pid of [...owned].reverse()) { try { if (live(pid)) process.kill(pid, 'SIGKILL') } catch {} }
  }
  status.endedAt = new Date().toISOString()
  status.exitCode = exitCode
  save()
  fs.writeFileSync(folder + '/exit', String(exitCode))
  process.exit(exitCode)
}
