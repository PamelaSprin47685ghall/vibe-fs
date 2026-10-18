import { resolve } from 'node:path'
import { performance } from 'node:perf_hooks'
import { isFileCompletionEvent, isFileWrapper, testEntryFile } from './test-run-state.mjs'

export const MAX_PHYSICAL_WAIT_MS = 30000

export function validatePhysicalWait(operation, limitMs) {
  if (typeof operation !== 'string' || operation.length === 0 || operation.length > 300) {
    throw new TypeError('Physical wait requires an operation of 1 to 300 characters')
  }
  if (!Number.isSafeInteger(limitMs) || limitMs <= 0 || limitMs > MAX_PHYSICAL_WAIT_MS) {
    throw new TypeError(`Physical wait requires an integer bound from 1 to ${MAX_PHYSICAL_WAIT_MS}ms`)
  }
}

const sameInvocation = (left, right) =>
  ['name', 'file', 'line', 'column', 'nesting'].every(key => left[key] === right[key]) &&
  (left.testId === undefined || right.testId === undefined || left.testId === right.testId)

export class NativeTestSupervision {
  constructor({ files, silenceMs, now = () => performance.now(), requireAllocation = false }) {
    if (!Number.isFinite(silenceMs) || silenceMs <= 0) throw new TypeError('A positive silence window is required')
    this.now = now
    this.silenceMs = silenceMs
    this.requireAllocation = requireAllocation
    this.lastProgressAt = now()
    this.progressCount = 0
    this.lastProgress = 'start'
    this.backgroundCount = 0
    this.lastBackground = null
    this.entries = new Map()
    this.failure = null
    this.stopped = false
    for (const name of files) {
      const file = resolve(name)
      if (this.entries.has(file)) throw new Error(`Duplicate planned entry ${file}`)
      this.entries.set(file, { file, phase: 'queued', completed: false, lastVerdict: null,
        lastProgressAt: this.lastProgressAt, nativeSequence: 0, invocations: [],
        usedTokens: new Set(), pending: null, allowance: null })
    }
  }

  nextDeadline() {
    if (this.stopped) return null
    const active = [...this.entries.values()].filter(entry => entry.phase === 'active')
    if (active.length === 0) return { at: this.lastProgressAt + this.silenceMs, file: null, kind: 'lifecycle' }
    return active.map(entry => entry.allowance
      ? { at: entry.allowance.deadline, file: entry.file, kind: 'physical', operation: entry.allowance.operation }
      : { at: entry.lastProgressAt + this.silenceMs, file: entry.file, kind: 'silence' })
      .reduce((first, next) => next.at < first.at ? next : first)
  }

  check(now = this.now()) {
    if (this.failure) throw this.failure
    if (this.stopped) return
    const deadline = this.nextDeadline()
    if (now >= deadline.at) {
      this.failure = Object.assign(new Error(`Native test ${deadline.kind} deadline expired`),
        { code: 'verification-silence', deadline })
      throw this.failure
    }
  }

  stop() {
    this.stopped = true
  }

  entry(file) {
    if (typeof file !== 'string' || file.length === 0) throw new Error('File lifecycle event requires entryFile')
    const entry = this.entries.get(resolve(file))
    if (!entry) throw new Error(`File lifecycle event names unplanned entry ${file}`)
    return entry
  }

  allocate(file, runId) {
    this.check()
    const entry = this.entry(file)
    if (entry.phase !== 'queued' || entry.runId || typeof runId !== 'string') {
      throw new Error('File execution instance was already allocated or is invalid')
    }
    entry.runId = runId
  }

  observe(event) {
    const now = this.now()
    this.check(now)
    if (this.stopped) return []
    const type = event?.type
    if (type !== 'test:complete') {
      this.backgroundCount += 1
      this.lastBackground = type ?? 'unknown event'
    }
    const relevant = ['runner:file-start', 'runner:file-drained', 'test:start', 'test:dequeue', 'test:complete', 'test:pass', 'test:fail']
    if (!relevant.includes(type)) return []
    const entry = this.entry(testEntryFile(event))
    const expected = type === 'runner:file-start' ? 'queued' : 'active'
    if (entry.phase !== expected) throw new Error(`File lifecycle event ${type} received while ${entry.file} is ${entry.phase}`)
    if (type === 'runner:file-start') {
      if ((this.requireAllocation || entry.runId) && (!entry.runId || entry.runId !== event.data.runId)) {
        throw new Error('File start does not match its allocated execution instance')
      }
      entry.phase = 'active'
      entry.runId ??= event.data.runId
      entry.lastProgressAt = now
    } else if (type === 'runner:file-drained') {
      if (entry.allowance) throw new Error('A file drained with an open physical wait')
      entry.phase = 'drained'
    } else if (type === 'test:start') entry.lastStart = { ...event.data }
    else if (type === 'test:dequeue' || type === 'test:complete') return this.native(entry, event, now)
    return []
  }

  native(entry, event, now) {
    const sequence = event.data.nativeSequence
    if (!Number.isSafeInteger(sequence) || sequence <= entry.nativeSequence) {
      throw new Error('Native test sequence is missing, repeated or out of order')
    }
    entry.nativeSequence = sequence
    if (event.type === 'test:dequeue') {
      if (isFileWrapper(event)) return []
      if (entry.allowance) throw new Error('Another native invocation started during an open physical wait')
      const invocation = { ...event.data, occurrence: sequence, ambiguous: false }
      for (const active of entry.invocations.filter(value => sameInvocation(value, invocation))) {
        active.ambiguous = true
        invocation.ambiguous = true
      }
      entry.invocations.push(invocation)
    } else {
      if (isFileWrapper(event)) {
        if (entry.allowance) throw new Error('A native file completed with an open physical wait')
        if (isFileCompletionEvent(event)) entry.completed = true
      } else {
        const index = entry.invocations.findIndex(value => sameInvocation(value, event.data))
        if (index >= 0) {
          const [ended] = entry.invocations.splice(index, 1)
          if (entry.allowance?.occurrence === ended.occurrence) {
            if (!entry.allowance.ended) throw new Error('A finite invocation completed without its end acknowledgement')
            entry.allowance = null
          }
        } else if (entry.allowance) throw new Error('A finite invocation completed without its native identity')
      }
      entry.lastVerdict = `test:complete:${event.data.name}`
      this.progressCount += 1
      this.lastProgress = entry.lastVerdict
      entry.lastProgressAt = now
      this.lastProgressAt = entry.lastProgressAt
    }
    return this.bindPending(entry, now)
  }

  reply(request, ok, reason) {
    return { entryFile: request.entryFile, runId: request.runId, requestId: request.requestId,
      contextToken: request.contextToken, phase: request.phase, ok, ...(reason ? { reason } : {}) }
  }

  control(request) {
    const now = this.now()
    this.check(now)
    if (this.stopped) return [this.reply(request, false, 'Supervision has stopped')]
    const entry = this.entry(request.entryFile)
    if (entry.phase !== 'active' || typeof entry.runId !== 'string' || entry.runId !== request.runId) {
      return [this.reply(request, false, 'The file execution instance is not active')]
    }
    if (request.phase === 'cancel') {
      if (entry.pending?.requestId === request.requestId) entry.pending = null
      return []
    }
    if (request.phase === 'end') {
      const wait = entry.allowance
      if (!wait || wait.contextToken !== request.contextToken || wait.operation !== request.operation || wait.ended) {
        return [this.reply(request, false, 'No matching open physical operation')]
      }
      wait.ended = true
      return [this.reply(request, true)]
    }
    if (request.phase !== 'begin') return [this.reply(request, false, 'Unknown physical wait phase')]
    try { validatePhysicalWait(request.operation, request.limitMs) }
    catch (error) { return [this.reply(request, false, error.message)] }
    if (typeof request.contextToken !== 'string' || !/^[a-f0-9]{32}$/.test(request.contextToken)) {
      return [this.reply(request, false, 'Invalid registered context token')]
    }
    if (entry.usedTokens.has(request.contextToken) || entry.pending || (entry.allowance && !entry.allowance.ended)) {
      return [this.reply(request, false, 'Physical wait was already declared for this invocation')]
    }
    entry.usedTokens.add(request.contextToken)
    entry.pending = request
    return this.bindPending(entry, now)
  }

  bindPending(entry, now) {
    const request = entry.pending
    if (!request || entry.allowance) return []
    const matching = entry.invocations.filter(value => value.name === request.name)
    if (matching.length === 0) return []
    entry.pending = null
    const invocation = matching[0]
    if (matching.length !== 1 || entry.invocations.length !== 1 || invocation.ambiguous ||
        invocation.nesting !== 0 || request.fullName !== request.name) {
      return [this.reply(request, false, 'No unique ordinary top-level native invocation')]
    }
    if (invocation.spent) return [this.reply(request, false, 'Physical wait was already consumed')]
    invocation.spent = true
    entry.allowance = { contextToken: request.contextToken, operation: request.operation,
      occurrence: invocation.occurrence, deadline: now + request.limitMs, ended: false }
    return [this.reply(request, true)]
  }

  incomplete() {
    return [...this.entries.values()].filter(entry => !entry.completed).map(entry => entry.file)
  }

  progress() {
    return { count: this.progressCount, last: this.lastProgress,
      backgroundCount: this.backgroundCount, lastBackground: this.lastBackground }
  }

  currentWait(file) {
    const entry = this.entry(file)
    return { invocations: entry.invocations.map(({ name, occurrence, nesting }) => ({ name, occurrence, nesting })),
      physical: entry.allowance ? { operation: entry.allowance.operation,
        occurrence: entry.allowance.occurrence, deadline: entry.allowance.deadline, ended: entry.allowance.ended } : null }
  }

  snapshot() {
    const result = { queued: [], active: [], drained: [] }
    for (const entry of this.entries.values()) {
      if (entry.phase === 'active') result.active.push({ file: entry.file, lastVerdict: entry.lastVerdict,
        ...(entry.lastStart ? { lastStart: { ...entry.lastStart } } : {}) })
      else result[entry.phase].push(entry.file)
    }
    return result
  }
}
