import assert from 'node:assert/strict'
import { createHash } from 'node:crypto'
import { appendFileSync, writeFileSync, renameSync, readFileSync, existsSync, mkdirSync, readdirSync } from 'node:fs'
import { execFileSync } from 'node:child_process'
import { setTimeout as delay } from 'node:timers/promises'
import { fileURLToPath } from 'node:url'
import { ProcessSession } from 'wanxiangli/process'
import { SupervisedProcess } from 'wanxiangli/supervision'
import { Wait } from 'wanxiangli/wait'

const [lane, directory] = process.argv.slice(2)
const worker = fileURLToPath(new URL('./worker.mjs', import.meta.url))
const duration = 2 * 60 * 60 * 1000
const beginning = performance.now()
const sessions = []
const ownedPids = new Set()
const registry = Wait.createRegistry(16)
const observer = Wait.observerCapability(registry)
const owner = Wait.owner('soak', { lane })
const status = { lane, pid: process.pid, source: '3db7b439283779f6037244d57cf05e6ec3e7ab31', state: 'Starting', phase: null, bytes: 0, completedPhases: [], startedAt: new Date().toISOString() }
let current = null
let rejectFailure
const failure = new Promise((_, reject) => { rejectFailure = reject })
const fail = error => rejectFailure(error)
process.on('SIGTERM', () => fail(Error('Coordinator requested cancellation')))
process.on('SIGINT', () => fail(Error('Operator requested cancellation')))
process.on('unhandledRejection', fail)
const save = () => {
  status.updatedAt = new Date().toISOString()
  status.elapsedMs = performance.now() - beginning
  status.activeWaits = Wait.snapshot(registry).active.length
  status.ownedPids = [...ownedPids]
  status.currentState = current?.inspect() ?? null
  status.memory = process.memoryUsage()
  status.activeResources = process.getActiveResourcesInfo?.() ?? null
  writeFileSync(directory + '/status.next', JSON.stringify(status))
  renameSync(directory + '/status.next', directory + '/status.json')
}
const note = (event, details = {}) => {
  appendFileSync(directory + '/events.jsonl', JSON.stringify({ at: new Date().toISOString(), elapsedMs: performance.now() - beginning, event, ...details }) + '\n')
  save()
}
const bounded = async (task, milliseconds, label) => {
  let timer
  try {
    return await Promise.race([task, failure, new Promise((_, reject) => { timer = setTimeout(() => reject(Error(label)), milliseconds) })])
  } finally { clearTimeout(timer) }
}
const until = async (observe, label, milliseconds = 10000) => {
  const end = performance.now() + milliseconds
  while (performance.now() < end) {
    const value = observe()
    if (value) return value
    await bounded(delay(20), 500, label)
  }
  throw Error(label)
}
const live = pid => {
  try {
    const state = execFileSync('/bin/ps', ['-p', String(pid), '-o', 'stat='], { encoding: 'utf8', timeout: 3000 }).trim()
    return state !== '' && !state.startsWith('Z')
  } catch (error) {
    if (error.status === 1) return false
    throw error
  }
}
const sample = () => {
  const pids = [process.pid, ...ownedPids].filter(pid => pid === process.pid || live(pid))
  const ps = execFileSync('/bin/ps', ['-p', pids.join(','), '-o', 'pid=,ppid=,pgid=,rss=,time=,stat=,comm='], { encoding: 'utf8', timeout: 5000 })
  const descriptors = execFileSync('/usr/sbin/lsof', ['-a', '-p', pids.join(','), '-F', 'pf'], { encoding: 'utf8', timeout: 10000 })
  appendFileSync(directory + '/resources.jsonl', JSON.stringify({ at: new Date().toISOString(), elapsedMs: performance.now() - beginning, phase: status.phase, pids, ps, descriptors, memory: process.memoryUsage(), activeResources: process.getActiveResourcesInfo?.() ?? null }) + '\n')
  if (current) {
    const state = current.inspect()
    assert.equal(state.failure, null)
    assert.ok(state.stdout.retained <= 4096 && state.stderr.retained <= 4096)
  }
  assert.ok(status.bytes <= 16 * 1024 * 1024, 'Per-lane output safety envelope exceeded')
  save()
}
const options = (args, tailBytes = 4096) => ({ executable: process.execPath, args, cwd: directory, env: { SOAK_LANE: lane }, tailBytes, maxWriteBytes: 2048 })
const collect = (session, phase) => {
  const lengths = { stdout: 0, stderr: 0 }
  const hashes = { stdout: createHash('sha256'), stderr: createHash('sha256') }
  const tails = { stdout: Buffer.alloc(0), stderr: Buffer.alloc(0) }
  const task = (async () => {
    for (;;) {
      const part = await session.takeOutput()
      if (part.kind === 'Closed') return { state: part.state, lengths, hashes, tails }
      assert.equal(part.kind, 'Chunk', `${phase}: ${JSON.stringify(part)}`)
      assert.equal(part.from, lengths[part.channel])
      assert.ok(part.bytes.length > 0 && part.bytes.length <= 65536)
      lengths[part.channel] += part.bytes.length
      assert.equal(part.to, lengths[part.channel])
      hashes[part.channel].update(part.bytes)
      tails[part.channel] = Buffer.concat([tails[part.channel], Buffer.from(part.bytes)]).subarray(-4096)
      status.bytes += part.bytes.length
      status.lastOutputAt = new Date().toISOString()
    }
  })()
  task.catch(fail)
  return task
}
const expectedOutput = (channel, frames, descendant) => {
  const hash = createHash('sha256')
  let bytes = 0
  const add = value => { hash.update(value); bytes += value.length }
  add(Buffer.from(`\uFEFFSTREAM:${channel}\r\n`))
  const unit = Buffer.from('\uFEFF中🧭\r\n')
  const block = Buffer.alloc(3 * 1024 * 1024)
  for (let offset = 0; offset < block.length; offset += unit.length) unit.copy(block, offset)
  add(block)
  for (let frame = 0; frame < frames; frame += 1) add(Buffer.from(`\uFEFF${channel}|${String(frame).padStart(6, '0')}|中🧭\r\n`))
  if (descendant) add(Buffer.from(channel === 'stdout' ? 'DESCENDANT_READY\r\n' : 'DESCENDANT_TERM\r\n'))
  return { bytes, hash: hash.digest('hex') }
}
const tailText = bytes => {
  for (let start = 0; start <= Math.min(3, bytes.length); start += 1) {
    try { return new TextDecoder('utf-8', { fatal: true, ignoreBOM: true }).decode(bytes.subarray(start)) } catch {}
  }
  throw Error('Final tail has no valid UTF8 suffix')
}

async function phase(name, supervised) {
  const phaseDirectory = directory + '/' + name
  mkdirSync(phaseDirectory)
  status.phase = name
  const start = performance.now()
  const session = supervised
    ? new SupervisedProcess(options([worker, 'paced', phaseDirectory, 'descendant']))
    : new ProcessSession(options([worker, 'paced', phaseDirectory, 'eof']), 'Capture')
  current = session
  sessions.push(session)
  const started = await bounded(session.started(), 10000, 'Target startup did not settle')
  assert.equal(started.kind, 'Started')
  ownedPids.add(started.pid)
  if (supervised) ownedPids.add(session.inspect().supervisor.pid)
  note('phase-started', { name, target: started.pid, supervised })
  await until(() => existsSync(phaseDirectory + '/writer.ready'), 'Writer ready witness absent')
  await bounded(delay(2000), 4000, 'Paused capture challenge exceeded its bound')
  assert.equal(existsSync(phaseDirectory + '/writer.burst-done'), false, 'Paused physical burst completed without backpressure')
  const short = await session.wait(25)
  assert.equal(short.kind, 'TimedOut')
  assert.equal(short.state.phase, 'Running')
  note('physical-backpressure-observed', { name, pausedMs: 2000, burstBytesPerChannel: 3 * 1024 * 1024 })
  const collected = collect(session, name)
  const descriptor = { waitKind: 'physical-process-close', owner, subject: { phase: name }, producer: Wait.externalProducer('process', { pid: String(started.pid) }), escapes: [Wait.escape('processLifetime')], source: 'independent-physical-soak' }
  const closed = Wait.awaitTask(observer, descriptor, session.wait(duration + 90000))
  closed.catch(fail)
  const inputHash = createHash('sha256')
  let inputBytes = 0
  let sequence = 0
  while (performance.now() - start < duration) {
    assert.equal(session.inspect().exit, null, 'Target exited before its owned EOF')
    const input = Buffer.from(`\uFEFFinput:${lane}:${name}:${sequence}\r\n中🧭`)
    const result = await bounded(session.write(input), 10000, 'Input write did not settle')
    assert.deepEqual(result, { kind: 'Written', bytes: input.length })
    inputHash.update(input)
    inputBytes += input.length
    sequence += 1
    status.inputBytes = inputBytes
    status.inputStages = sequence
    save()
    const remaining = duration - (performance.now() - start)
    if (remaining > 0) await bounded(delay(Math.min(60000, remaining)), 65000, 'Phase pacing timer exceeded its bound')
  }
  session.endInput()
  const witness = await until(() => existsSync(phaseDirectory + '/witness.json') && JSON.parse(readFileSync(phaseDirectory + '/witness.json', 'utf8')), 'Actual input EOF witness absent')
  assert.equal(witness.inputBytes, inputBytes)
  assert.equal(witness.inputHash, inputHash.digest('hex'))
  if (supervised) {
    ownedPids.add(witness.descendant)
    await until(() => session.inspect().exit?.code === 23, 'Original target exit was not observed')
    assert.equal(session.inspect().phase, 'Draining')
    assert.equal(session.inspect().supervisor.phase, 'Running')
    assert.equal(live(witness.descendant), true)
    assert.equal(session.signal('SIGTERM').kind, 'Requested')
    await until(() => existsSync(phaseDirectory + '/term-seen'), 'Descendant did not receive group TERM')
    assert.equal((await session.wait(25)).kind, 'TimedOut')
    assert.equal(session.signal('SIGKILL').kind, 'Requested')
  }
  const output = await bounded(collected, 15000, 'Capture did not finish after final EOF/cancellation')
  const waitResult = await bounded(closed, 15000, 'Owned close wait did not settle')
  assert.equal(waitResult.kind, 'Closed')
  assert.equal(output.state.failure, null)
  assert.deepEqual(output.state.exit, { code: supervised ? 23 : 0, signal: null })
  for (const channel of ['stdout', 'stderr']) {
    const expected = expectedOutput(channel, witness.frames, supervised)
    assert.equal(output.lengths[channel], expected.bytes)
    assert.equal(output.hashes[channel].digest('hex'), expected.hash)
    const tail = session.read(channel, 0)
    assert.deepEqual(Buffer.from(tail.bytes), output.tails[channel])
    assert.equal(tail.text, tailText(output.tails[channel]))
  }
  await until(() => !live(started.pid), 'Original target remains live after Closed')
  if (supervised) {
    await until(() => !live(witness.descendant), 'Descendant remains live after Closed')
    await until(() => !live(output.state.supervisor.pid), 'Supervisor remains live after Closed')
    assert.equal(output.state.capture, 'Closed')
  }
  assert.equal(Wait.snapshot(registry).active.length, 0)
  status.completedPhases.push({ name, elapsedMs: performance.now() - start, frames: witness.frames, bytes: output.lengths, inputBytes, inputStages: sequence, exit: output.state.exit })
  note('phase-completed', status.completedPhases.at(-1))
  current = null
}

let ticker
let exitCode = 1
try {
  save()
  sample()
  ticker = setInterval(() => { try { sample() } catch (error) { fail(error) } }, 30000)
  const keeper = new ProcessSession(options([worker, 'keeper', directory], 4096), 'Capture')
  sessions.push(keeper)
  const keeperStart = await bounded(keeper.started(), 10000, 'Owned keeper startup failed')
  assert.equal(keeperStart.kind, 'Started')
  ownedPids.add(keeperStart.pid)
  keeper.endInput()
  const keeperOutput = []
  const keeperResult = (async () => {
    for (;;) {
      const part = await keeper.takeOutput()
      if (part.kind === 'Closed') {
        assert.equal(part.state.failure, null)
        assert.deepEqual(part.state.exit, { code: 0, signal: null })
        const receipt = JSON.parse(Buffer.concat(keeperOutput).toString())
        assert.equal(receipt.exitWon, false)
        assert.ok(receipt.elapsedMs >= 4 * 60 * 60 * 1000 - 100)
        status.ownedKeeper = receipt
        note('owned-keeper-completed', receipt)
        return
      }
      assert.equal(part.kind, 'Chunk')
      assert.equal(part.channel, 'stdout')
      keeperOutput.push(Buffer.from(part.bytes))
      assert.ok(keeperOutput.reduce((sum, bytes) => sum + bytes.length, 0) <= 1024)
    }
  })()
  keeperResult.catch(fail)
  status.state = 'Running'
  await Promise.race([(async () => {
    await phase('process', false)
    await phase('supervision', true)
    await bounded(keeperResult, 90000, 'Owned keepalive target did not finish')
  })(), failure])
  sample()
  const residual = readdirSync(directory + '/tmp').filter(name => name.startsWith('wanxiangli-supervision-'))
  assert.deepEqual(residual, [])
  status.state = 'Passed'
  exitCode = 0
} catch (error) {
  status.state = 'Failed'
  status.error = { message: error.message, stack: error.stack }
  note('failed', status.error)
} finally {
  clearInterval(ticker)
  for (const session of sessions) {
    try {
      if (session.inspect().phase !== 'Closed') {
        session.signal('SIGKILL')
        session.discardOutput()
        await Promise.race([session.wait(5000), delay(5500)])
      }
    } catch (error) { note('cleanup-error', { message: error.message }) }
  }
  for (const pid of ownedPids) {
    try { if (live(pid)) process.kill(pid, 'SIGKILL') } catch (error) { note('cleanup-pid-error', { pid, message: error.message }) }
  }
  status.endedAt = new Date().toISOString()
  status.exitCode = exitCode
  save()
  writeFileSync(directory + '/exit', String(exitCode))
  process.exit(exitCode)
}
