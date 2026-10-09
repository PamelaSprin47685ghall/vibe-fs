import assert from 'node:assert/strict'
import fs from 'node:fs'
import path from 'node:path'
import { createHash } from 'node:crypto'
import { createRequire, syncBuiltinESMExports } from 'node:module'
import lockfile from 'proper-lockfile'

const [mode, commonDir, writerId, scenario, requestJson] = process.argv.slice(2)
const request = JSON.parse(requestJson)
assert.ok(['measure', 'cold'].includes(mode))
assert.ok(['mkdir-eacces', 'mkdir-eio', 'busy-success', 'busy-eacces'].includes(scenario))
const target = path.join(commonDir, 'wanxiangshu')
const lockPath = `${target}.lock`
const sourceFile = path.join(target, 'events', `${request.sourceWriterId}.ndjson`)
const writerFile = path.join(target, 'events', `${writerId}.ndjson`)
const digest = text => createHash('sha256').update(text, 'utf8').digest('hex')
const oldDigest = digest(request.oldBody)
const incomingDigest = digest(request.incomingBody)
const oldFile = path.join(target, 'payloads', oldDigest)
const incomingFile = path.join(target, 'payloads', incomingDigest)
const oldRef = 'blobs/' + oldDigest
const incomingRef = 'blobs/' + incomingDigest
const journalSurface = await import('../../../../dist/Persistence/Journal/Surface.js')
const eventStore = await import('../../../../dist/Persistence/EventStore/Surface.js')
const mustOk = result => {
  assert.equal(result.ok, true, JSON.stringify(result.error))
  return result
}
const open = async writer => mustOk(await journalSurface.JournalSurface_bootWithWriterId(
  commonDir, writer, 'payload-g1-' + writer, process.pid, new Date().toISOString())).journal
const sourceFacts = () => fs.readFileSync(sourceFile, 'utf8').trimEnd().split('\n').map(JSON.parse).map(event => ({
  id: event.event_id, stream: event.stream_id, type: event.event_type, parents: event.parents,
  payload: event.payload, payloadRefs: event.payload_refs,
}))
const views = (handle, facts) => facts.map(fact => ({ event: eventStore.read(handle, fact.id),
  head: eventStore.head(handle, fact.stream), heads: eventStore.heads(handle, fact.stream) }))
const physical = () => ({ sourceBytes: fs.readFileSync(sourceFile, 'base64'),
  oldBytes: fs.readFileSync(oldFile, 'base64'), incomingBytes: fs.existsSync(incomingFile)
    ? fs.readFileSync(incomingFile, 'base64') : null,
  payloadFiles: fs.readdirSync(path.dirname(oldFile)).sort(),
  eventFiles: fs.readdirSync(path.dirname(sourceFile)).sort(), writerCreated: fs.existsSync(writerFile),
  lockReleased: !fs.existsSync(lockPath) })

async function cold() {
  assert.notEqual(writerId, request.sourceWriterId)
  const journal = await open(writerId)
  const handle = eventStore.create(commonDir, writerId + '-views')
  let operationFailure
  try {
    const facts = sourceFacts()
    const output = { pid: process.pid, current: journalSurface.JournalSurface_snapshot(journal),
      oldRead: await journalSurface.JournalSurface_readPayload(journal, oldRef),
      incomingRead: await journalSurface.JournalSurface_readPayload(journal, incomingRef),
      views: views(handle, facts), facts, physical: physical() }
    assert.equal(output.physical.writerCreated, false)
    assert.equal(output.physical.lockReleased, true)
    assert.equal(fs.existsSync(path.join(target, 'events', `${writerId}-views.ndjson`)), false)
    return output
  } catch (error) {
    operationFailure = { error }
    throw error
  } finally {
    const cleanupFailures = []
    try {
      journalSurface.JournalSurface_dispose(journal)
    } catch (error) {
      cleanupFailures.push(error)
    }
    try {
      eventStore.dispose(handle)
    } catch (error) {
      cleanupFailures.push(error)
    }
    if (cleanupFailures.length > 0) {
      throw new AggregateError(operationFailure === undefined ? cleanupFailures : [operationFailure.error, ...cleanupFailures],
        'payload cold fixture cleanup failed', { cause: operationFailure?.error })
    }
  }
}

async function measure() {
  assert.notEqual(writerId, request.sourceWriterId)
  assert.notEqual(oldDigest, incomingDigest)
  const require = createRequire(import.meta.url)
  const retry = require(require.resolve('retry', { paths: [path.dirname(require.resolve('proper-lockfile'))] }))
  const originalOperation = retry.operation
  const originals = { mkdirSync: fs.mkdirSync, appendFileSync: fs.appendFileSync,
    writeFileSync: fs.writeFileSync, openSync: fs.openSync, fsyncSync: fs.fsyncSync,
    closeSync: fs.closeSync, rmSync: fs.rmSync }
  const injected = Object.assign(new Error(`Controlled one-hop payload ${scenario} mkdir failure`), {
    code: scenario === 'mkdir-eio' ? 'EIO' : 'EACCES', syscall: 'mkdir', path: lockPath,
  })
  const observed = { mkdirAttempts: 0, legalAfterFailure: 0, injected: 0, busy: 0,
    payloadWrite: 0, payloadFsync: 0, payloadClose: 0, release: 0, append: 0 }
  const descriptors = new Set()
  const busy = scenario.startsWith('busy-')
  let recording = false
  let failNextMkdir = !busy
  let failedMkdir = false
  let releaseHeld
  let releaseFinished = !busy
  let busyBeforeRelease = null
  let payloadBeforeRelease = false
  let settleBusy
  const busyObserved = new Promise(resolve => { settleBusy = resolve })
  const owns = (value, ownedPath) => typeof value === 'string' && path.resolve(value) === ownedPath
  let seedJournal
  let journal
  let handle
  let output
  let operationFailure
  const cleanupFailures = []
  try {
    seedJournal = await open(request.sourceWriterId)
    const oldReceipt = mustOk(await journalSurface.JournalSurface_writePayload(seedJournal, request.oldBody))
    assert.equal(oldReceipt.blobRef, oldRef)
    assert.equal(oldReceipt.blobDigest, oldDigest)
    mustOk(await journalSurface.JournalSurface_appendAgent(seedJournal,
      { kind: 'Session', session: request.sessionId }, null,
      { family: 'Companion', case: 'TerminalOutputCaptured', payload: { SessionId: request.sessionId,
        TextRef: oldRef, TextDigest: oldDigest, ProviderRun: 'payload-g1-seed-run' } }))
    assert.equal(journalSurface.JournalSurface_hasSession(seedJournal, request.sessionId), true)
    journalSurface.JournalSurface_dispose(seedJournal)
    seedJournal = undefined
    journal = await open(writerId)
    handle = eventStore.create(commonDir, writerId + '-views')
    const beforeCurrent = journalSurface.JournalSurface_snapshot(journal)
    const before = physical()
    const facts = sourceFacts()
    const beforeViews = views(handle, facts)
    assert.ok(beforeCurrent.sessions.includes(request.sessionId))
    assert.deepEqual(beforeCurrent.sessionProjections[request.sessionId].xTrace,
      { openingPresent: false, partCount: 0, latestTerminalPresent: true })
    assert.deepEqual(beforeViews, facts.map(fact => ({ event: fact, head: fact.id, heads: [fact.id] })))
    assert.equal(facts.length, 2)
    assert.deepEqual(facts.at(-1).payloadRefs, [oldDigest])
    assert.equal(before.incomingBytes, null)
    assert.deepEqual(before.payloadFiles, [oldDigest])
    assert.deepEqual(before.eventFiles, [`${request.sourceWriterId}.ndjson`])
    assert.equal(before.writerCreated, false)
    assert.ok(Buffer.from(before.oldBytes, 'base64').length > 0)
    if (busy) {
      releaseHeld = lockfile.lockSync(target, { realpath: false, retries: 0, stale: 5000, update: 1500 })
      assert.equal(fs.existsSync(lockPath), true)
    }
    retry.operation = function (...args) {
      const operation = Reflect.apply(originalOperation, this, args)
      const originalRetry = operation.retry
      operation.retry = function (error) {
        const actualBusy = recording && error?.code === 'ELOCKED' && error.file === target
        const decision = Reflect.apply(originalRetry, this, [error])
        if (actualBusy) {
          observed.busy += 1
          queueMicrotask(settleBusy)
        }
        return decision
      }
      return operation
    }
    fs.mkdirSync = function (...args) {
      if (recording && owns(args[0], lockPath)) {
        observed.mkdirAttempts += 1
        if (failNextMkdir && !failedMkdir) {
          failedMkdir = true
          observed.injected += 1
          throw injected
        }
        if (failedMkdir) observed.legalAfterFailure += 1
      }
      return Reflect.apply(originals.mkdirSync, this, args)
    }
    fs.appendFileSync = function (...args) {
      const result = Reflect.apply(originals.appendFileSync, this, args)
      if (recording && typeof args[0] === 'string'
        && path.dirname(path.resolve(args[0])) === path.dirname(sourceFile)) observed.append += 1
      return result
    }
    fs.writeFileSync = function (...args) {
      const result = Reflect.apply(originals.writeFileSync, this, args)
      if (recording && owns(args[0], incomingFile)) {
        observed.payloadWrite += 1
        if (!releaseFinished) payloadBeforeRelease = true
      }
      return result
    }
    fs.openSync = function (...args) {
      const descriptor = Reflect.apply(originals.openSync, this, args)
      if (recording && owns(args[0], incomingFile) && args[1] === 'r+') descriptors.add(descriptor)
      return descriptor
    }
    fs.fsyncSync = function (...args) {
      const result = Reflect.apply(originals.fsyncSync, this, args)
      if (recording && descriptors.has(args[0])) observed.payloadFsync += 1
      return result
    }
    fs.closeSync = function (...args) {
      const result = Reflect.apply(originals.closeSync, this, args)
      if (descriptors.delete(args[0])) observed.payloadClose += 1
      return result
    }
    fs.rmSync = function (...args) {
      if (recording && owns(args[0], lockPath)) observed.release += 1
      return Reflect.apply(originals.rmSync, this, args)
    }
    syncBuiltinESMExports()
    let settled = false
    recording = true
    const pending = journalSurface.JournalSurface_writePayload(journal, request.incomingBody).then(result => {
      settled = true
      return result
    }, error => {
      settled = true
      throw error
    })
    if (busy) {
      const reached = await Promise.race([busyObserved.then(() => 'busy'), pending.then(() => 'settled')])
      assert.equal(reached, 'busy', 'The original proper-lockfile reports actual ELOCKED before release')
      busyBeforeRelease = { settled, append: observed.append, payloadWrite: observed.payloadWrite,
        payloadFsync: observed.payloadFsync, lockExists: fs.existsSync(lockPath), physical: physical() }
      failNextMkdir = scenario === 'busy-eacces'
      releaseHeld()
      releaseHeld = undefined
      releaseFinished = true
      assert.equal(fs.existsSync(lockPath), false)
    }
    const result = await pending
    recording = false
    assert.equal(descriptors.size, 0)
    assert.equal(fs.existsSync(lockPath), false)
    const afterCurrent = journalSurface.JournalSurface_snapshot(journal)
    assert.deepEqual(afterCurrent, beforeCurrent)
    output = { pid: process.pid, result, observed, oldDigest, incomingDigest, before, physical: physical(),
      beforeCurrent, afterCurrent, facts, beforeViews, views: views(handle, facts), busyBeforeRelease,
      payloadBeforeRelease, oldRead: await journalSurface.JournalSurface_readPayload(journal, oldRef),
      incomingRead: await journalSurface.JournalSurface_readPayload(journal, incomingRef) }
  } catch (error) {
    operationFailure = { error }
  } finally {
    recording = false
    retry.operation = originalOperation
    Object.assign(fs, originals)
    syncBuiltinESMExports()
    try {
      if (releaseHeld !== undefined) releaseHeld()
    } catch (error) {
      cleanupFailures.push(error)
    }
    try {
      if (seedJournal !== undefined) journalSurface.JournalSurface_dispose(seedJournal)
    } catch (error) {
      cleanupFailures.push(error)
    }
    try {
      if (journal !== undefined) journalSurface.JournalSurface_dispose(journal)
    } catch (error) {
      cleanupFailures.push(error)
    }
    try {
      if (handle !== undefined) eventStore.dispose(handle)
    } catch (error) {
      cleanupFailures.push(error)
    }
  }
  if (cleanupFailures.length > 0) {
    throw new AggregateError(operationFailure === undefined ? cleanupFailures : [operationFailure.error, ...cleanupFailures],
      'payload lock fixture cleanup failed', { cause: operationFailure?.error })
  }
  if (operationFailure !== undefined) throw operationFailure.error
  return output
}

process.stdout.write(JSON.stringify(mode === 'measure' ? await measure() : await cold(),
  (_key, value) => typeof value === 'bigint' ? value.toString() : value))
