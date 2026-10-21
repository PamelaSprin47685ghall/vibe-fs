import assert from 'node:assert/strict'
import { spawnSync } from 'node:child_process'
import { randomUUID, createHash } from 'node:crypto'
import { appendFileSync, readFileSync, writeFileSync } from 'node:fs'
import { setTimeout as sleep } from 'node:timers/promises'
import { ProcessSession } from '/Users/yuanxi/.codex/worktrees/upstream-plan-20261020/vibe-fs/wanxiangli/dist/process/ProcessSession.js'

const durationMs = 3600000
const intervalMs = 60000
const nonce = randomUUID()
const statePath = '/private/tmp/vibe-fs-mf01-long-park-state.json'
const samplePath = '/private/tmp/vibe-fs-mf01-long-park-samples.jsonl'
const ownedPidPath = '/private/tmp/vibe-fs-mf01-long-park-owned.jsonl'
const childPath = '/private/tmp/vibe-fs-mf01-long-park-child.mjs'
const frames = []
const controller = new AbortController()
const state = { scope: 'one Node22 POSIX ProcessSession pause primitive, not sandbox/Host/deadline acceptance', nonce, harnessPid: process.pid, targetPid: null, phase: 'Starting', startedAt: new Date().toISOString(), durationMs, intervalMs }
const persist = () => writeFileSync(statePath, JSON.stringify(state, null, 2))
const record = event => { const value = { at: new Date().toISOString(), ...event }; appendFileSync(samplePath, JSON.stringify(value) + '\n'); console.log(JSON.stringify(value)) }
const gone = pid => assert.throws(() => process.kill(pid, 0), { code: 'ESRCH' })
const observer = (executable, args) => {
  const result = spawnSync(executable, args, { encoding: 'utf8', timeout: 1500, killSignal: 'SIGKILL', maxBuffer: 1048576 })
  if (result.pid) {
    gone(result.pid)
    appendFileSync(ownedPidPath, JSON.stringify({ kind: 'observer', pid: result.pid, executable, args, exited: true, at: new Date().toISOString() }) + '\n')
  }
  assert.ifError(result.error)
  assert.equal(result.status, 0, result.stderr)
  return result.stdout.trim()
}
const inspect = () => {
  const line = observer('/bin/ps', ['-p', String(state.targetPid), '-o', 'pid=,ppid=,pgid=,stat=,time=,rss=,lstart='])
  const match = line.match(/^(\d+)\s+(\d+)\s+(\d+)\s+(\S+)\s+(\S+)\s+(\d+)\s+(.+)$/)
  assert.ok(match, line)
  const [, pid, ppid, pgid, status, cpu, rss, birth] = match
  const fields = observer('/usr/sbin/lsof', ['-nP', '-a', '-p', pid, '-F', 'f']).split('\n')
  const fds = fields.filter(value => /^f\d/.test(value)).map(value => value.slice(1)).sort()
  return { pid: Number(pid), ppid: Number(ppid), pgid: Number(pgid), status, cpu, rssKiB: Number(rss), birth, fdCount: fds.length, fds }
}
const bounded = async (promise, milliseconds, label) => {
  let timer
  try {
    return await Promise.race([promise, new Promise((_, reject) => { timer = setTimeout(() => reject(new Error(label)), milliseconds) })])
  } finally { clearTimeout(timer) }
}
let session
let failure
let outputDone
let baseline
let readyResolve
const ready = new Promise(resolve => { readyResolve = resolve })
let buffered = ''
let stderr = ''
const stop = reason => {
  controller.abort(reason)
  if (session) session.signal('SIGKILL')
}
const interrupted = signal => stop(new Error(`harness received ${signal}`))
const onTerm = () => interrupted('SIGTERM')
const onInt = () => interrupted('SIGINT')
process.once('SIGTERM', onTerm)
process.once('SIGINT', onInt)
const backstop = setTimeout(() => stop(new Error('one-hour primitive overall 3630000ms backstop')), durationMs + 30000)
persist()
record({ kind: 'harness-start', harnessPid: process.pid, nonce, childSourceSha256: createHash('sha256').update(readFileSync(childPath)).digest('hex') })
try {
  session = new ProcessSession({ executable: process.execPath, args: [childPath, nonce], cwd: '/private/tmp', env: {}, tailBytes: 65536, maxWriteBytes: 1024 }, 'Capture')
  outputDone = (async () => {
    while (true) {
      const event = await session.takeOutput()
      if (event.kind === 'Closed') {
        assert.equal(event.state.failure, null)
        assert.deepEqual(event.state.exit, { code: 0, signal: null })
        state.nativeClosed = event.state
        return
      }
      if (event.kind === 'Discarded') return
      assert.equal(event.kind, 'Chunk')
      if (event.channel === 'stderr') { stderr += Buffer.from(event.bytes).toString('utf8'); continue }
      buffered += Buffer.from(event.bytes).toString('utf8')
      while (buffered.includes('\n')) {
        const end = buffered.indexOf('\n')
        const frame = JSON.parse(buffered.slice(0, end))
        buffered = buffered.slice(end + 1)
        frames.push(frame)
        record({ kind: 'child-frame', frame })
        if (frame.kind === 'parked') readyResolve(frame)
      }
    }
  })().catch(error => { failure = error; stop(error) })
  const started = await bounded(session.started(), 3000, 'target start timeout')
  assert.equal(started.kind, 'Started')
  state.targetPid = started.pid
  appendFileSync(ownedPidPath, JSON.stringify({ kind: 'target', pid: started.pid, parentPid: process.pid, nonce, at: new Date().toISOString() }) + '\n')
  const parked = await bounded(ready, 3000, 'child closure ready timeout')
  assert.deepEqual(parked, { kind: 'parked', pid: started.pid, parentPid: process.pid, nonce, keep: 41 })
  const before = inspect()
  assert.equal(before.pid, started.pid)
  assert.equal(before.ppid, process.pid)
  assert.equal(before.pgid, started.pid)
  state.identity = { pid: before.pid, ppid: before.ppid, pgid: before.pgid, birth: before.birth }
  const sameIdentity = sample => assert.deepEqual({ pid: sample.pid, ppid: sample.ppid, pgid: sample.pgid, birth: sample.birth }, state.identity)
  process.kill(started.pid, 'SIGSTOP')
  for (let index = 0; index < 20; index++) {
    const sample = inspect()
    sameIdentity(sample)
    if (sample.status.includes('T')) { baseline = sample; break }
    await sleep(25, null, { signal: controller.signal })
  }
  assert.ok(baseline, 'owned target did not enter stopped state')
  const stoppedMono = performance.now()
  state.phase = 'Stopped'
  state.stoppedAt = new Date().toISOString()
  state.plannedResumeAt = new Date(Date.now() + durationMs).toISOString()
  state.baseline = baseline
  state.sampleCount = 1
  state.rssRangeKiB = { min: baseline.rssKiB, max: baseline.rssKiB }
  persist()
  record({ kind: 'stopped-start', ...state.identity, stoppedAt: state.stoppedAt, plannedResumeAt: state.plannedResumeAt, durationMs, sample: baseline })
  const queued = Buffer.from(JSON.stringify({ id: nonce, value: 'queued-once-汉🔥' }) + '\n')
  assert.ok(queued.length < 1024)
  const written = await bounded(session.write(queued), 1000, 'bounded stopped stdin enqueue timeout')
  assert.deepEqual(written, { kind: 'Written', bytes: queued.length })
  session.endInput()
  state.queued = { bytes: queued.length, at: new Date().toISOString(), firstHopWritten: true, applicationReceipt: false, id: nonce }
  persist()
  record({ kind: 'queued-once', ...state.queued })
  while (performance.now() - stoppedMono < durationMs) {
    await sleep(Math.min(intervalMs, durationMs - (performance.now() - stoppedMono)), null, { signal: controller.signal })
    if (failure) throw failure
    const sample = inspect()
    sameIdentity(sample)
    assert.match(sample.status, /T/)
    assert.equal(sample.cpu, baseline.cpu, 'stopped target CPU time changed')
    assert.deepEqual(sample.fds, baseline.fds, 'stopped target numeric FD set changed')
    assert.equal(frames.length, 1, 'stopped continuation produced an application receipt')
    assert.equal(session.inspect().phase, 'Running')
    state.sampleCount++
    state.elapsedStoppedMs = performance.now() - stoppedMono
    state.rssRangeKiB.min = Math.min(state.rssRangeKiB.min, sample.rssKiB)
    state.rssRangeKiB.max = Math.max(state.rssRangeKiB.max, sample.rssKiB)
    state.lastSample = sample
    persist()
    record({ kind: 'stopped-sample', ordinal: state.sampleCount, elapsedStoppedMs: state.elapsedStoppedMs, sample })
  }
  const last = inspect()
  sameIdentity(last)
  assert.match(last.status, /T/)
  state.elapsedStoppedMs = performance.now() - stoppedMono
  assert.ok(state.elapsedStoppedMs >= durationMs)
  process.kill(started.pid, 'SIGCONT')
  state.phase = 'Resuming'
  state.resumedAt = new Date().toISOString()
  persist()
  record({ kind: 'continued', ...state.identity, elapsedStoppedMs: state.elapsedStoppedMs })
  await bounded(outputDone, 5000, 'resumed continuation/close timeout')
  if (failure) throw failure
  assert.equal(stderr, '')
  assert.equal(buffered, '')
  assert.equal(frames.length, 2)
  assert.deepEqual(frames[1], { kind: 'resumed', pid: started.pid, parentPid: process.pid, nonce, keep: 42, messageCount: 1, messages: [{ id: nonce, value: 'queued-once-汉🔥' }] })
  assert.equal((await session.wait(1000)).kind, 'Closed')
  gone(started.pid)
  state.phase = 'Complete'
  state.completedAt = new Date().toISOString()
  state.continuationPreserved = true
  state.uniqueQueuedMessageReceipt = true
  state.targetGone = true
  state.queued.applicationReceipt = true
  persist()
  record({ kind: 'complete', ...state })
} catch (error) {
  state.phase = 'Failed'
  state.failedAt = new Date().toISOString()
  state.error = { message: error?.message ?? String(error), stack: error?.stack ?? null }
  persist()
  record({ kind: 'failed', ...state.error })
  process.exitCode = 1
} finally {
  clearTimeout(backstop)
  process.removeListener('SIGTERM', onTerm)
  process.removeListener('SIGINT', onInt)
  if (session && session.inspect().phase !== 'Closed') session.signal('SIGKILL')
  session?.discardOutput()
  if (session) {
    const closed = await session.wait(3000)
    state.cleanup = { closed: closed.kind, at: new Date().toISOString() }
    if (closed.kind !== 'Closed') process.exitCode = 1
    if (state.targetPid) {
      try { gone(state.targetPid); state.cleanup.targetGone = true }
      catch (error) { state.cleanup.targetGone = false; state.cleanup.error = error.message; process.exitCode = 1 }
    }
  }
  persist()
  record({ kind: 'harness-cleanup', harnessPid: process.pid, targetPid: state.targetPid, phase: state.phase, cleanup: state.cleanup })
}
