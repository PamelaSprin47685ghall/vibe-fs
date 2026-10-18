import assert from 'node:assert/strict'
import fs from 'node:fs'
import path from 'node:path'
import { pathToFileURL } from 'node:url'
import { syncBuiltinESMExports } from 'node:module'
import { createHash, randomUUID } from 'node:crypto'

const [packageRoot, directory, scenario, receiptPath] = process.argv.slice(2)
assert.ok(path.isAbsolute(packageRoot))
assert.ok(path.isAbsolute(directory))
assert.ok(['closed', 'before-physical', 'after-release', 'valid'].includes(scenario))
assert.equal(process.env.NODE_TEST_CONTEXT, undefined)
assert.equal(process.env.WANXIANGSHU_NO_FATAL_EXIT, undefined)
const imported = relative => import(pathToFileURL(path.join(packageRoot, 'dist', relative)).href)
const owner = 'budget-writer-owner-' + scenario
const failedRun = 'budget-failed-' + randomUUID()
const reason = 'BUDGET_NATIVE_FAILURE_' + randomUUID()
const eventsDirectory = path.join(directory, 'wanxiangshu', 'events')
const lock = path.join(directory, 'wanxiangshu.lock')
const cause = Object.assign(new Error('BUDGET_NATIVE_' + scenario + '_RECEIPT_LOST'), { code: 'BUDGET_NATIVE_RECEIPT_LOST' })
const originals = {
  appendFileSync: fs.appendFileSync, openSync: fs.openSync,
  fsyncSync: fs.fsyncSync, closeSync: fs.closeSync, rmSync: fs.rmSync,
}
const ownFile = value => typeof value === 'string' && path.dirname(path.resolve(value)) === eventsDirectory && value.endsWith('.ndjson')
const descriptors = new Set()
let armed = false
let targetAppended = false
let targetSynced = false
let runtime
let sync
const counts = { targetCalls: 0, targetNativeWrites: 0, targetFsync: 0, releasesAfterTarget: 0, injected: 0 }
const timeline = []
const result = {
  pid: process.pid, directory, scenario, failedRun, reason, counts, timeline,
  cause: { message: cause.message, name: cause.name, code: cause.code },
}

function files() {
  return fs.readdirSync(eventsDirectory).filter(name => name.endsWith('.ndjson')).sort().map(name => {
    const bytes = fs.readFileSync(path.join(eventsDirectory, name))
    return {
      name, bytes: bytes.toString('base64'), sha256: createHash('sha256').update(bytes).digest('hex'),
      facts: bytes.length === 0 ? [] : bytes.toString('utf8').trimEnd().split('\n').map(JSON.parse),
    }
  })
}

function installFault() {
  fs.appendFileSync = function (...args) {
    const text = typeof args[1] === 'string' ? args[1] : Buffer.from(args[1]).toString('utf8')
    const target = armed && ownFile(args[0]) && text.includes('FailureRecorded') && text.includes(failedRun) && text.includes(reason)
    if (target) {
      counts.targetCalls += 1
      timeline.push({ event: 'target-append-entered', path: args[0], requested: text })
      if (scenario === 'before-physical' && counts.injected === 0) {
        counts.injected += 1
        timeline.push({ event: 'before-native-append-threw' })
        throw cause
      }
    }
    const value = Reflect.apply(originals.appendFileSync, this, args)
    if (target) {
      counts.targetNativeWrites += 1
      targetAppended = true
      timeline.push({ event: 'target-native-append-completed' })
    }
    return value
  }
  fs.openSync = function (...args) {
    const descriptor = Reflect.apply(originals.openSync, this, args)
    if (ownFile(args[0])) descriptors.add(descriptor)
    return descriptor
  }
  fs.fsyncSync = function (...args) {
    const value = Reflect.apply(originals.fsyncSync, this, args)
    if (armed && targetAppended && descriptors.has(args[0])) {
      counts.targetFsync += 1
      targetSynced = true
      timeline.push({ event: 'target-native-fsync-completed' })
    }
    return value
  }
  fs.closeSync = function (...args) {
    const value = Reflect.apply(originals.closeSync, this, args)
    descriptors.delete(args[0])
    return value
  }
  fs.rmSync = function (...args) {
    const ownedLock = armed && typeof args[0] === 'string' && path.resolve(args[0]) === lock
    if (ownedLock) assert.equal(JSON.parse(fs.readFileSync(path.join(lock, 'owner.json'), 'utf8')).pid, process.pid)
    const value = Reflect.apply(originals.rmSync, this, args)
    if (ownedLock && targetAppended && targetSynced) {
      counts.releasesAfterTarget += 1
      assert.equal(fs.existsSync(lock), false)
      timeline.push({ event: 'owned-lock-released-after-native-write-and-fsync' })
      if (scenario === 'after-release' && counts.injected === 0) {
        counts.injected += 1
        timeline.push({ event: 'after-native-release-threw' })
        throw cause
      }
    }
    return value
  }
  syncBuiltinESMExports()
}

try {
  installFault()
  if (process.versions.bun) {
    const namespace = await import('node:fs')
    const { mock } = await import('bun:test')
    mock.module('node:fs', () => ({ ...namespace, ...fs, default: fs }))
  }
  sync = await imported('Execution/Delegation/SyncDelegate/Surface.js')
  const dispatch = await imported('Interaction/Dispatch/DispatchSurface.js')
  const failure = await imported('Participant/Provider/Attempt/Fallback/ProviderFailureSurface.js')
  runtime = await sync.createForProviderRecovery(directory, [{ sessionId: owner, agent: 'manager' }])
  const execution = sync.startObserved(runtime, owner, 'Physical failure-budget writer proof')
  const admission = sync.observedAdmission(execution)
  const completion = sync.observedCompletion(execution)
  await sync.awaitPromptCount(runtime, owner, 'Engineer', 1)
  assert.equal(sync.returnPromptOutcome(runtime, owner, 'Engineer', 0, dispatch.admittedWithReceipt('initial-host-receipt')), true)
  const initial = await sync.confirmManagedPromptPhysical(runtime, owner, 'Engineer', 0, 'initial-budget-physical')
  assert.equal(initial.ok, true, JSON.stringify(initial))
  result.initial = initial
  result.admission = await admission
  result.before = await sync.withJournal(runtime, async journal => failure.snapshot(journal, initial.sessionId))
  result.beforePrompts = sync.promptCount(runtime, owner, 'Engineer')
  assert.equal(result.beforePrompts, 1)
  assert.equal(sync.observeProviderFailureStop(runtime, initial.sessionId, failedRun), true)
  if (scenario === 'closed') sync.closeJournalWriter(runtime)
  armed = true
  const attempted = sync.observeExactProviderFailure(runtime, initial.sessionId, initial.physicalUserMessageId,
    initial.authorityRootUserMessageId, failedRun, reason)
  if (scenario === 'valid') {
    await sync.awaitPromptCount(runtime, owner, 'Engineer', 2)
    assert.equal(sync.returnPromptOutcome(runtime, owner, 'Engineer', 1, dispatch.admittedWithReceipt('retry-host-receipt')), true)
  }
  result.handled = await attempted
  result.afterPrompts = sync.promptCount(runtime, owner, 'Engineer')
  result.sendDelta = result.afterPrompts - result.beforePrompts
  if (scenario !== 'valid') {
    const callsBeforeRepeat = counts.targetCalls
    result.repeated = { beforePrompts: result.afterPrompts }
    result.repeated.handled = await sync.observeExactProviderFailure(runtime, initial.sessionId,
      initial.physicalUserMessageId, initial.authorityRootUserMessageId, failedRun, reason)
    result.repeated.afterPrompts = sync.promptCount(runtime, owner, 'Engineer')
    result.repeated.sendDelta = result.repeated.afterPrompts - result.repeated.beforePrompts
    result.repeated.additionalAppendCalls = counts.targetCalls - callsBeforeRepeat
    assert.equal(result.repeated.sendDelta, 0)
    assert.equal(result.repeated.additionalAppendCalls, 0)
    result.completion = await completion
    assert.equal(result.completion.ok, false)
    if (scenario === 'closed') assert.match(result.completion.error, /append not attempted .*writer is closing/)
    if (scenario === 'before-physical') assert.match(result.completion.error, /append outcome unknown .* at PhysicalAppend:/)
    if (scenario === 'after-release') assert.match(result.completion.error, /append outcome unknown .* at StoreRelease:/)
    if (scenario !== 'closed') assert.ok(result.completion.error.includes(cause.message))
  }
  result.after = await sync.withJournal(runtime, async journal => failure.snapshot(journal, initial.sessionId))
  result.filesAfter = files()
  result.failureFactCount = result.filesAfter.flatMap(file => file.facts).filter(fact => {
    const encoded = JSON.stringify(fact)
    return encoded.includes('FailureRecorded') && encoded.includes(failedRun) && encoded.includes(reason)
  }).length
  result.lockReleased = !fs.existsSync(lock)
  result.openEventDescriptors = descriptors.size
  assert.equal(result.sendDelta, scenario === 'valid' ? 1 : 0)
  assert.equal(result.failureFactCount, scenario === 'valid' || scenario === 'after-release' ? 1 : 0)
  assert.equal(result.after.failures, result.failureFactCount)
  assert.equal(result.lockReleased, true)
  assert.equal(result.openEventDescriptors, 0)
  assert.equal(counts.targetCalls, scenario === 'closed' ? 0 : 1)
  if (scenario === 'before-physical') assert.equal(counts.targetNativeWrites, 0)
  if (scenario === 'after-release') {
    assert.equal(counts.targetNativeWrites, 1)
    assert.ok(counts.targetFsync >= 1)
    assert.equal(counts.releasesAfterTarget, 1)
  }
  result.accepted = true
} catch (error) {
  result.accepted = false
  result.error = { name: error.name, message: error.message, stack: error.stack }
  process.exitCode = 1
} finally {
  armed = false
  Object.assign(fs, originals)
  syncBuiltinESMExports()
  if (runtime) {
    try {
      await sync.closeRecovery(runtime)
      result.closed = true
    } catch (error) {
      result.cleanupError = { name: error.name, message: error.message }
      process.exitCode = 1
    }
  }
  fs.writeFileSync(receiptPath, JSON.stringify(result) + '\n')
}
