import assert from 'node:assert/strict'
import fs from 'node:fs'
import path from 'node:path'
import { pathToFileURL } from 'node:url'

const [product, directory, receiptPath] = process.argv.slice(2)
assert.ok(path.isAbsolute(product))
assert.ok(path.isAbsolute(directory))
const imported = relative => import(pathToFileURL(path.join(product, 'dist', relative)).href)
const sync = await imported('Execution/Delegation/SyncDelegate/Surface.js')
const dispatch = await imported('Interaction/Dispatch/DispatchSurface.js')
const failure = await imported('Participant/Provider/Attempt/Fallback/ProviderFailureSurface.js')
const owner = 'concurrent-recovery-owner'
const providerRun = 'concurrent-recovery-failed-run'
const reason = 'concurrent-recovery-confirmed-failure'
const result = { pid: process.pid, scope: 'same live Journal, direct SyncDelegate HandleTurn; ordinary session scheduler bypassed' }
let runtime

async function bounded(pending, operation) {
  let timer
  try {
    return await Promise.race([pending, new Promise((_, reject) => {
      timer = setTimeout(() => reject(new Error(operation + ' did not settle')), 8000)
    })])
  } finally {
    clearTimeout(timer)
  }
}

try {
  runtime = await sync.createForProviderRecovery(directory, [{ sessionId: owner, agent: 'manager' }])
  const execution = sync.startObserved(runtime, owner, 'Concurrent recovery dispatch')
  await bounded(sync.awaitPromptCount(runtime, owner, 'Engineer', 1), 'initial send')
  assert.equal(sync.returnPromptOutcome(runtime, owner, 'Engineer', 0, dispatch.admittedWithReceipt('initial-receipt')), true)
  const initial = await sync.confirmManagedPromptPhysical(runtime, owner, 'Engineer', 0, 'initial-physical')
  assert.equal(initial.ok, true)
  await bounded(sync.observedAdmission(execution), 'initial admission')
  assert.equal(sync.observeProviderFailureStop(runtime, initial.sessionId, providerRun), true)

  const observe = () => sync.observeExactProviderFailure(runtime, initial.sessionId, initial.physicalUserMessageId,
    initial.authorityRootUserMessageId, providerRun, reason)
  const first = observe()
  const second = observe()
  first.catch(() => {})
  second.catch(() => {})
  await sync.awaitPromptCount(runtime, owner, 'Engineer', 2)
  assert.equal(sync.returnPromptOutcome(runtime, owner, 'Engineer', 1, dispatch.admittedWithReceipt('recovery-receipt')), true)
  result.handled = await bounded(Promise.all([first, second]), 'both recovery observations')
  assert.deepEqual(result.handled, [true, true])
  result.sendDelta = sync.promptCount(runtime, owner, 'Engineer') - 1
  assert.equal(result.sendDelta, 1)
  result.state = await sync.withJournal(runtime, async journal => failure.snapshot(journal, initial.sessionId))
  assert.equal(result.state.failures, 1)

  const events = path.join(directory, 'wanxiangshu', 'events')
  result.failureFacts = fs.readdirSync(events).filter(name => name.endsWith('.ndjson')).flatMap(name =>
    fs.readFileSync(path.join(events, name), 'utf8').trimEnd().split('\n').filter(Boolean).map(JSON.parse))
    .filter(fact => JSON.stringify(fact).includes(reason))
  await bounded(observe(), 'late duplicate observation')
  assert.equal(sync.promptCount(runtime, owner, 'Engineer') - 1, 1)
  result.accepted = true
} catch (error) {
  result.accepted = false
  result.error = { name: error.name, message: error.message, stack: error.stack }
  process.exitCode = 1
} finally {
  if (runtime) {
    try {
      await bounded(sync.closeRecovery(runtime), 'owned recovery close')
      result.closed = true
    } catch (error) {
      result.cleanupError = error.message
      process.exitCode = 1
    }
  }
  fs.writeFileSync(receiptPath, JSON.stringify(result, null, 2) + '\n')
}
