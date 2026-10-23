import assert from 'node:assert/strict'
import fs from 'node:fs'
import { spawn } from 'node:child_process'
import { syncBuiltinESMExports } from 'node:module'
import { tmpdir } from 'node:os'
import { basename, isAbsolute, join, resolve } from 'node:path'
import { fileURLToPath, pathToFileURL } from 'node:url'

const packageRoot = process.env.WXS_PACKAGE_BASE ?? fileURLToPath(new URL('../../../../', import.meta.url))
const script = fileURLToPath(import.meta.url)
const load = (name) => import(pathToFileURL(join(packageRoot, 'dist', name)).href)
const writer = 'native-crash-law'
const cap = 512
const id = (n) => n.toString(16).padStart(40, '0')
const directoryOf = (root) => join(root, 'state', 'wanxiangshu', 'events')
const lockOf = (root) => join(root, 'state', 'wanxiangshu.lock')
const legacyOf = (root) => join(directoryOf(root), `${writer}.ndjson`)
const segmentOf = (root, n) => join(directoryOf(root), `${writer}-${String(n).padStart(4, '0')}.ndjson`)
const snapshot = (root) => Object.fromEntries(fs.readdirSync(directoryOf(root)).sort()
  .map((name) => [name, fs.readFileSync(join(directoryOf(root), name)).toString('base64')]))

export const coldReaderWriterId = (pid) => `cold-reader-p${pid}`

export const nativeCrashPoints = Object.freeze([
  'append-first', 'fsync-first', 'append-unicode-second',
  'before-unlink', 'after-unlink', 'resume-copied-fsync',
])

async function history() {
  const { encode } = await load('Persistence/EventStore/CodecSurface.js')
  const events = []
  for (let n = 1; n <= 5; n += 1) {
    const event = {
      id: id(n), stream: 'proof/native-crash', type: 'JobRequested',
      parents: n === 1 ? [] : [id(n - 1)],
      payload: { text: n === 4 ? '汉🔥 CRLF\r\n' : '' }, payloadRefs: [],
    }
    const bytes = n <= 2 ? 256 : 384
    event.payload.text += 'x'.repeat(bytes - Buffer.byteLength(encode(event)))
    assert.equal(Buffer.byteLength(encode(event)), bytes)
    events.push(event)
  }
  return { events, lines: events.map(encode) }
}

async function seed(root) {
  const { events, lines } = await history()
  const store = await load('Persistence/EventStore/Surface.js')
  const handle = store.createWithSegmentCap(join(root, 'state'), writer, cap)
  try {
    assert.deepEqual(await store.append(handle, events.slice(0, 2)), { ok: true, cuts: [] })
  } finally {
    store.dispose(handle)
  }
  assert.equal(fs.readFileSync(segmentOf(root, 0), 'utf8'), lines.slice(0, 2).join(''))
  // Only the initial, disjoint legacy form is a fixture. Copied prefixes must
  // come from the real convergence worker before its parent kills it.
  fs.writeFileSync(legacyOf(root), lines.slice(2).join(''))
  const legacyFd = fs.openSync(legacyOf(root), 'r+')
  try {
    fs.fsyncSync(legacyFd)
  } finally {
    fs.closeSync(legacyFd)
  }
  assert.equal(fs.existsSync(lockOf(root)), false)
  return { pid: process.pid, initial: snapshot(root) }
}

async function cold(root) {
  const { events } = await history()
  const before = snapshot(root)
  const store = await load('Persistence/EventStore/Surface.js')
  const retention = await load('Persistence/EventStore/RetentionSurface.js')
  const writers = retention.writerEventIds(join(root, 'state'))
  assert.equal(writers.length, 1)
  assert.equal(writers[0].writerId, writer)
  const ids = writers[0].eventIds
  assert.deepEqual(ids, events.map((event) => event.id))
  assert.equal(new Set(ids).size, ids.length)
  const handle = store.create(join(root, 'state'), coldReaderWriterId(process.pid))
  try {
    for (const event of events) assert.deepEqual(store.read(handle, event.id), event)
    assert.deepEqual(store.heads(handle, events[0].stream), [events.at(-1).id])
  } finally {
    store.dispose(handle)
  }
  assert.deepEqual(snapshot(root), before, 'cold reading must not materialize the disk')
  return { pid: process.pid, ids, bytes: before }
}

async function recover(root) {
  const { lines } = await history()
  const retention = await load('Persistence/EventStore/RetentionSurface.js')
  assert.deepEqual(await retention.reorganizeWithSegmentCap(join(root, 'state'), cap), { ok: true })
  assert.equal(fs.existsSync(legacyOf(root)), false)
  assert.equal(fs.existsSync(lockOf(root)), false, 'the real dead-owner lock must be acquired and released')
  const files = snapshot(root)
  const bytes = Object.values(files).map((value) => Buffer.from(value, 'base64').toString('utf8')).join('')
  assert.equal(bytes, lines.join(''))
  const ids = bytes.trimEnd().split('\n').map((line) => JSON.parse(line).event_id)
  assert.equal(new Set(ids).size, ids.length)
  assert.equal(fs.readFileSync(segmentOf(root, 0), 'utf8'), lines.slice(0, 2).join(''))
  return { pid: process.pid, ids, files, bytes: Buffer.byteLength(bytes) }
}

async function cut(root, point) {
  const originals = {
    appendFileSync: fs.appendFileSync, openSync: fs.openSync, closeSync: fs.closeSync,
    fsyncSync: fs.fsyncSync, unlinkSync: fs.unlinkSync, writeSync: fs.writeSync,
  }
  const descriptors = new Map()
  const trace = []
  let appends = 0
  let syncs = 0
  const owned = (path) => typeof path === 'string' && resolve(path).startsWith(`${directoryOf(root)}/`)
  const isSegment = (path) => owned(path) && /-\d{4}\.ndjson$/.test(path)
  const checkpoint = (event, stop) => {
    trace.push({ sequence: trace.length + 1, ...event })
    if (!stop) return
    const owner = JSON.parse(fs.readFileSync(join(lockOf(root), 'owner.json'), 'utf8'))
    assert.equal(owner.pid, process.pid)
    originals.writeSync(1, `${JSON.stringify({ kind: 'cut-ready', pid: process.pid, point, owner, trace, bytes: snapshot(root) })}\n`)
    Atomics.wait(new Int32Array(new SharedArrayBuffer(4)), 0, 0)
    throw new Error('the cut worker must be killed by its parent')
  }
  fs.appendFileSync = function (...args) {
    const result = Reflect.apply(originals.appendFileSync, this, args)
    if (isSegment(args[0])) {
      appends += 1
      checkpoint({ operation: 'appendFileSync', phase: 'after', path: args[0], appends },
        point === 'append-first' && appends === 1 || point === 'append-unicode-second' && appends === 2)
    }
    return result
  }
  fs.openSync = function (...args) {
    const fd = Reflect.apply(originals.openSync, this, args)
    if (owned(args[0])) descriptors.set(fd, args[0])
    return fd
  }
  fs.closeSync = function (...args) {
    const result = Reflect.apply(originals.closeSync, this, args)
    descriptors.delete(args[0])
    return result
  }
  fs.fsyncSync = function (...args) {
    const result = Reflect.apply(originals.fsyncSync, this, args)
    const path = descriptors.get(args[0])
    if (isSegment(path)) {
      syncs += 1
      checkpoint({ operation: 'fsyncSync', phase: 'after', path, syncs },
        point === 'fsync-first' && syncs === 1 || point === 'resume-copied-fsync' && path === segmentOf(root, 1))
    }
    return result
  }
  fs.unlinkSync = function (...args) {
    const legacy = args[0] === legacyOf(root)
    if (legacy && point === 'before-unlink') checkpoint({ operation: 'unlinkSync', phase: 'before', path: args[0] }, true)
    const result = Reflect.apply(originals.unlinkSync, this, args)
    if (legacy) checkpoint({ operation: 'unlinkSync', phase: 'after', path: args[0] }, point === 'after-unlink')
    return result
  }
  syncBuiltinESMExports()
  const retention = await load('Persistence/EventStore/RetentionSurface.js')
  const result = await retention.reorganizeWithSegmentCap(join(root, 'state'), cap)
  throw new Error(`cut point was not reached: ${point}: ${JSON.stringify({ result, trace })}`)
}

function child(mode, root, point, signal) {
  signal?.throwIfAborted()
  return new Promise((resolveChild, reject) => {
    const env = { ...process.env }
    delete env.NODE_TEST_CONTEXT
    delete env.NODE_OPTIONS
    const worker = spawn(process.execPath, [script, '--worker', mode, root, ...(point ? [point] : [])], {
      stdio: ['ignore', 'pipe', 'pipe'], env,
    })
    let stdout = ''
    let stderr = ''
    let frame
    let failure
    const abort = () => {
      failure = signal.reason
      worker.kill('SIGKILL')
    }
    const timeout = setTimeout(() => {
      failure = new Error(`bounded worker timeout: ${mode}/${point ?? ''}: ${stderr}`)
      worker.kill('SIGKILL')
    }, 15000)
    signal?.addEventListener('abort', abort, { once: true })
    if (signal?.aborted) abort()
    worker.stderr.on('data', (chunk) => { stderr += chunk })
    worker.stdout.on('data', (chunk) => {
      stdout += chunk
      if (mode !== 'cut' || frame || !stdout.includes('\n')) return
      try {
        frame = JSON.parse(stdout.slice(0, stdout.indexOf('\n')))
        assert.equal(frame.kind, 'cut-ready')
        assert.equal(frame.pid, worker.pid)
        assert.equal(frame.owner.pid, worker.pid)
        assert.equal(JSON.parse(fs.readFileSync(join(lockOf(root), 'owner.json'), 'utf8')).pid, worker.pid)
      } catch (error) {
        failure = error
      }
      worker.kill('SIGKILL')
    })
    worker.once('error', (error) => { failure = error })
    worker.once('close', (code, exitSignal) => {
      clearTimeout(timeout)
      signal?.removeEventListener('abort', abort)
      try {
        if (failure) throw failure
        if (mode === 'cut') {
          assert.ok(frame, `no native boundary evidence: ${stderr}`)
          assert.equal(code, null)
          assert.equal(exitSignal, 'SIGKILL')
          assert.throws(() => process.kill(worker.pid, 0), { code: 'ESRCH' })
          assert.deepEqual(snapshot(root), frame.bytes, 'no call may run past the stop boundary')
          resolveChild({ ...frame, code, signal: exitSignal })
        } else {
          assert.equal(code, 0, stderr)
          assert.equal(exitSignal, null)
          resolveChild(JSON.parse(stdout.trim()))
        }
      } catch (error) {
        reject(error)
      }
    })
  })
}

export async function verifyNativeCrashCase(point, { signal } = {}) {
  assert.ok(nativeCrashPoints.includes(point))
  signal?.throwIfAborted()
  const root = fs.mkdtempSync(join(fs.realpathSync(tmpdir()), 'wxs-native-crash-'))
  let failed = false
  try {
    const initial = await child('seed', root, null, signal)
    const crashes = []
    const coldReads = []
    if (point === 'resume-copied-fsync') {
      crashes.push(await child('cut', root, 'append-first', signal))
      coldReads.push(await child('cold', root, null, signal))
    }
    crashes.push(await child('cut', root, point, signal))
    coldReads.push(await child('cold', root, null, signal))
    const recovered = await child('recover', root, null, signal)
    coldReads.push(await child('cold', root, null, signal))
    const repeated = await child('recover', root, null, signal)
    assert.deepEqual(repeated.files, recovered.files)
    const pids = [initial.pid, ...crashes.map((crash) => crash.pid),
      ...coldReads.map((read) => read.pid), recovered.pid, repeated.pid]
    assert.equal(new Set(pids).size, pids.length)
    for (const pid of pids) {
      assert.notEqual(pid, process.pid)
      assert.throws(() => process.kill(pid, 0), { code: 'ESRCH' })
    }
    return { point, seedPid: initial.pid, crashes, coldReads, recoveryPids: [recovered.pid, repeated.pid],
      finalIds: recovered.ids, finalBytes: recovered.bytes, idempotent: true, childrenExited: true }
  } catch (error) {
    failed = true
    error.message += `\nNative crash evidence retained at ${root}`
    throw error
  } finally {
    if (!failed) {
      fs.rmSync(root, { recursive: true, force: true })
      assert.equal(fs.existsSync(root), false)
    }
  }
}

if (process.argv[2] === '--worker') {
  const [, , , mode, root, point] = process.argv
  assert.ok(['seed', 'cold', 'recover', 'cut'].includes(mode))
  assert.ok(isAbsolute(root) && basename(root).startsWith('wxs-native-crash-'))
  if (mode === 'cut') {
    assert.ok(nativeCrashPoints.includes(point))
    await cut(root, point)
  } else {
    console.log(JSON.stringify(await ({ seed, cold, recover })[mode](root)))
  }
}
