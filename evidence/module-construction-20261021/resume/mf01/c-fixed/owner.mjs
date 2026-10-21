// Candidate A only. Scratch fs is real; production authority/ledger are absent.
import * as fs from 'node:fs'
import * as path from 'node:path'
import { randomUUID } from 'node:crypto'
import { execFileSync } from 'node:child_process'
import { fileURLToPath } from 'node:url'
import { ProcessSession } from './dependencies/ProcessSession.mjs'
import { frame, FrameReader, errorData } from './wire.mjs'
import { hostApis } from './host-apis.mjs'
import { HostDeadline } from './host-deadline.mjs'

const deferred = () => {
  let resolve, reject
  const promise = new Promise((yes, no) => { resolve = yes; reject = no })
  return { promise, resolve, reject }
}
export async function bounded(promise, milliseconds, label) {
  let timer
  try {
    return await Promise.race([promise, new Promise((_, reject) => { timer = setTimeout(() => reject(new Error(label)), milliseconds) })])
  } finally { clearTimeout(timer) }
}
export function identity(pid) {
  const line = execFileSync('/bin/ps', ['-o', 'pid=,ppid=,pgid=,stat=,lstart=', '-p', String(pid)], { encoding: 'utf8', timeout: 1000 }).trim()
  const fields = line.split(/\s+/)
  return { pid: Number(fields[0]), ppid: Number(fields[1]), pgid: Number(fields[2]), status: fields[3], birth: fields.slice(4).join(' ') }
}
export function absent(pid) {
  try { process.kill(pid, 0); return false }
  catch (error) { if (error.code === 'ESRCH') return true; throw error }
}

export class GuestOwner {
  static async create(executable, directory, options = {}) {
    const owner = new GuestOwner(directory, options)
    try {
      await owner.open(executable)
      return owner
    } catch (error) {
      let cleanup
      try { cleanup = await owner.dispose() }
      catch (failure) {
        throw Object.assign(new AggregateError([error, failure], 'STARTUP_CLEANUP_UNCONFIRMED', { cause: error }), { cleanupOwner: owner })
      }
      throw Object.assign(new Error('GUEST_START_FAILED', { cause: error }), { cleanup, cleanupOwner: null })
    }
  }
  constructor(directory, options) {
    this.directory = directory
    this.options = options
    this.runId = randomUUID()
    this.phase = 'Starting'
    this.generation = 0
    this.receipts = []
    this.rejected = []
    this.transportFaults = []
    this.accepted = new Set()
    this.replyQueue = []
    this.reader = new FrameReader()
    this.ready = deferred()
    this.result = deferred()
    this.closed = deferred()
    this.closeUnknown = deferred()
    this.parkWait = deferred()
    this.resumed = null
    this.inputChain = Promise.resolve()
    this.syncChain = Promise.resolve()
    this.deadline = new HostDeadline(() => {
      this.watchdogFired = true
      this.closeReason = 'DEADLINE'
      this.kill()
    })
    this.watchdogFired = false
    this.lastDeadline = null
    this.stderr = []
    this.apis = hostApis(path.join(directory, 'scratch'), options)
    this.fifoPath = path.join(directory, 'sync-reply')
    this.replyFd = null
    this.fifoIdentity = null
    this.session = null
    this.offerWaiters = []
    this.offerQueue = []
    this.offers = new WeakSet()
    this.references = new WeakSet()
    this.revokedReferences = new WeakSet()
    this.commandSequence = 0
    this.callablePending = new Map()
    this.businessSettled = false
  }
  arm(deadline) {
    this.resetDeadline(deadline)
    this.deadline.activate()
  }
  resetDeadline(epoch) {
    if (['Closing', 'ClosingUnknown', 'Closed', 'ProgramSettled'].includes(this.phase)) throw new Error('DEADLINE_OWNER_REVOKED')
    this.deadline.reset(epoch)
    this.lastDeadline = this.deadline.current
  }
  borrowDeadline(milliseconds) {
    if (!['Active', 'Abandoning'].includes(this.phase)) throw new Error('DEADLINE_OWNER_REVOKED')
    return this.deadline.borrow(milliseconds)
  }
  async open(executable) {
    fs.mkdirSync(path.join(this.directory, 'scratch'), { recursive: true })
    execFileSync('/usr/bin/mkfifo', [this.fifoPath])
    const created = fs.lstatSync(this.fifoPath)
    this.fifoIdentity = { dev: created.dev, ino: created.ino }
    this.replyFd = fs.openSync(this.fifoPath, fs.constants.O_RDWR | fs.constants.O_NONBLOCK)
    const guest = fileURLToPath(new URL('./guest.mjs', import.meta.url))
    const options = { executable, args: [guest, this.runId, this.fifoPath, JSON.stringify({ ignoreTerm: Boolean(this.options.ignoreTerm), noReady: Boolean(this.options.noReady) })], cwd: this.directory, env: {}, tailBytes: 65536, maxWriteBytes: 65536 }
    this.session = this.options.sessionFactory ? this.options.sessionFactory(options) : new ProcessSession(options, 'Capture')
    this.pump = this.drain()
    const started = await bounded(this.session.started(), 5000, 'START_TIMEOUT')
    if (started.kind !== 'Started') throw new Error(JSON.stringify(started))
    this.pid = started.pid
    this.nativeIdentity = identity(this.pid)
    if (this.nativeIdentity.ppid !== process.pid || this.nativeIdentity.pgid !== this.pid) throw new Error('OWNER_IDENTITY')
    await bounded(this.ready.promise, this.options.readyTimeoutMs ?? 5000, 'READY_TIMEOUT')
    if (this.readyFrame.pid !== this.pid || this.readyFrame.termIgnoring !== Boolean(this.options.ignoreTerm)) throw new Error('READY_IDENTITY')
    this.phase = 'Active'
  }
  async start(source, budgetMs = 3000) {
    if (this.phase !== 'Active' || this.started) throw new Error('START_PHASE')
    this.started = true
    this.arm(Date.now() + budgetMs)
    await this.send({ kind: 'start', source })
  }
  send(message) {
    const bytes = frame({ ...message, runId: this.runId })
    const send = async () => {
      for (let offset = 0; offset < bytes.length; offset += 16384) {
        const result = await this.session.write(bytes.subarray(offset, offset + 16384))
        if (result.kind !== 'Written') throw new Error('COMMAND_WRITE_UNKNOWN')
      }
    }
    const result = this.inputChain.then(send)
    this.inputChain = result.catch(error => { this.transportFaults.push(errorData(error)) })
    return result
  }
  nextOffer() {
    if (this.offerQueue.length) return Promise.resolve(this.offerQueue.shift())
    if (this.businessSettled || this.phase !== 'Active') return Promise.reject(new Error('OFFER_OWNER_REVOKED'))
    const wait = deferred()
    this.offerWaiters.push(wait)
    return wait.promise
  }
  acceptReference(value) {
    if (!value || value.runId !== this.runId || !['class', 'instance', 'thunk'].includes(value.kind) || !Number.isSafeInteger(value.id) || value.id <= 0) throw new Error('CALLABLE_REF_INVALID')
    const ref = Object.freeze(value)
    this.references.add(ref)
    return ref
  }
  requireReference(ref, kind) {
    if (!ref || !this.references.has(ref) || this.revokedReferences.has(ref) || ref.runId !== this.runId || (kind && ref.kind !== kind)) throw new Error('CALLABLE_REF_INVALID')
  }
  callable(operation, ref, fields = {}) {
    if (this.phase !== 'Active' || this.businessSettled) throw new Error('CALLABLE_OWNER_REVOKED')
    if (!this.deadline.admit()) throw new Error('DEADLINE_EXPIRED')
    this.requireReference(ref)
    if (operation === 'release' && ref.kind !== 'instance') throw new Error('CALLABLE_RELEASE_KIND')
    if (fields.thunk) this.requireReference(fields.thunk, 'thunk')
    if (operation === 'finishOffer') {
      if (!this.offers.has(ref)) throw new Error('CALLABLE_OFFER_RETIRED')
      this.offers.delete(ref)
      this.revokedReferences.add(ref)
    } else if (operation === 'release') this.revokedReferences.add(ref)
    const commandId = ++this.commandSequence
    const wait = deferred()
    this.callablePending.set(commandId, { wait, operation })
    this.send({ kind: 'callable-command', commandId, generation: this.generation, command: { operation, ref, ...fields } }).catch(error => this.fail(error))
    return wait.promise
  }
  settleBusiness(outcome) {
    if (this.businessSettled || !['Active', 'Abandoning'].includes(this.phase)) {
      this.rejected.push({ kind: outcome.kind, reason: 'BUSINESS_REVOKED' })
      return
    }
    this.businessSettled = true
    this.phase = 'ProgramSettled'
    this.result.resolve(this.abandonRequested ? { kind: 'Abandoned' } : outcome)
    this.revokeCommands('PROGRAM_SETTLED')
  }
  revokeCommands(reason) {
    for (const { wait } of this.callablePending.values()) wait.reject(new Error(reason))
    this.callablePending.clear()
    for (const wait of this.offerWaiters.splice(0)) wait.reject(new Error(reason))
  }
  writeSyncReply(message) {
    const bytes = frame({ ...message, runId: this.runId })
    const write = async () => {
      for (let offset = 0; offset < bytes.length;) {
        if (this.phase === 'Closing' || this.phase === 'Closed') throw new Error('REPLY_REVOKED')
        try {
          const count = await new Promise((resolve, reject) => fs.write(this.replyFd, bytes, offset, Math.min(16384, bytes.length - offset), null, (error, written) => error ? reject(error) : resolve(written)))
          if (count === 0) throw new Error('ZERO_REPLY_WRITE')
          offset += count
        } catch (error) {
          if (error.code !== 'EAGAIN' && error.code !== 'EWOULDBLOCK') throw error
          await new Promise(resolve => setTimeout(resolve, 1))
        }
      }
    }
    const result = this.syncChain.then(write)
    this.syncChain = result.catch(error => { this.transportFaults.push(errorData(error)) })
    return result
  }
  dispatch(message) {
    if (message.runId !== this.runId) { this.rejected.push({ kind: message.kind, reason: 'RUN_FENCE' }); return }
    this.receipts.push({ kind: message.kind, generation: message.generation ?? null, requestId: message.requestId ?? null })
    if (message.kind === 'ready') { this.readyFrame = message; this.ready.resolve(); return }
    if (!this.deadline.admit()) { this.rejected.push({ kind: message.kind, reason: 'DEADLINE_OR_OWNER_REVOKED' }); return }
    if (message.kind === 'result') { this.settleBusiness({ kind: 'Result', value: message.value }); return }
    if (message.kind === 'failure') { this.settleBusiness({ kind: 'GuestFailure', error: message.error }); return }
    if (message.kind === 'abandoned') { this.settleBusiness({ kind: 'Abandoned' }); return }
    if (message.kind === 'callable-offer') {
      if (this.phase !== 'Active' || this.businessSettled || message.generation !== this.generation) { this.rejected.push({ kind: message.kind, reason: 'OWNER_REVOKED' }); return }
      const ref = this.acceptReference(message.ref)
      if (ref.kind !== 'class') throw new Error('CALLABLE_OFFER_KIND')
      this.offers.add(ref)
      const waiter = this.offerWaiters.shift()
      if (waiter) waiter.resolve(ref)
      else this.offerQueue.push(ref)
      return
    }
    if (message.kind === 'callable-reply') {
      const pending = this.callablePending.get(message.commandId)
      if (!pending || !['Active', 'Abandoning'].includes(this.phase)) { this.rejected.push({ kind: message.kind, reason: 'COMMAND_RETIRED' }); return }
      if (this.options.dropCallableReplyFor === pending.operation && !this.droppedCallableReply) { this.droppedCallableReply = message; return }
      this.callablePending.delete(message.commandId)
      if (!message.ok) { pending.wait.reject(Object.assign(new Error(message.error.message), message.error)); return }
      if (pending.operation === 'construct') message.value = this.acceptReference(message.value)
      if (pending.operation === 'sanity') message.value = { nextDone: this.acceptReference(message.value.nextDone), watchVeto: this.acceptReference(message.value.watchVeto) }
      pending.wait.resolve(message.value)
      return
    }
    if (message.kind === 'park') {
      if (this.phase !== 'Active' || message.generation !== this.generation + 1) throw new Error('PARK_FENCE')
      this.generation = message.generation
      this.phase = 'ParkRequested'
      this.pendingPark = { generation: message.generation, payload: message.payload }
      this.parkWait.resolve(this.pendingPark)
      return
    }
    if (message.kind === 'control-ack') {
      if (this.phase !== 'Resuming' || message.generation !== this.generation) throw new Error('CONTROL_ACK_FENCE')
      this.phase = this.abandonRequested ? 'Abandoning' : 'Active'
      this.send({ kind: 'activate', generation: this.generation }).catch(error => this.fail(error))
      for (const reply of this.replyQueue.splice(0)) this.deliver(reply).catch(error => this.fail(error))
      return
    }
    if (message.kind === 'activated') {
      if (!['Active', 'Abandoning'].includes(this.phase)) { this.rejected.push({ kind: 'activated', reason: 'OWNER_REVOKED' }); return }
      if (!this.resumed || message.generation !== this.generation) throw new Error('ACTIVATION_FENCE')
      this.resumed.resolve({ generation: this.generation })
      return
    }
    if (message.kind !== 'request') throw new Error('UNKNOWN_MESSAGE')
    if (!['Active', 'Abandoning'].includes(this.phase) || message.generation !== this.generation) {
      this.rejected.push({ requestId: message.requestId, reason: 'PHASE_OR_GENERATION' })
      this.deliver({ message, ok: false, error: { name: 'Error', code: 'OWNER_REVOKED', message: 'OWNER_REVOKED' } }).catch(error => this.fail(error))
      return
    }
    const task = (async () => {
      let reply
      try { reply = { message, ok: true, value: await this.apis.invoke(message.operation, message.args) } }
      catch (error) { reply = { message, ok: false, error: errorData(error) } }
      if (this.options.dropReplyFor === message.operation) { this.droppedReply = reply; return }
      if (['ParkRequested', 'Parked', 'Resuming'].includes(this.phase)) this.replyQueue.push(reply)
      else if (['Active', 'Abandoning'].includes(this.phase) && this.deadline.admit()) await this.deliver(reply)
    })()
    this.accepted.add(task)
    task.catch(error => this.fail(error)).finally(() => this.accepted.delete(task))
  }
  deliver(reply) {
    const message = { kind: 'reply', requestId: reply.message.requestId, ok: reply.ok, value: reply.value, error: reply.error }
    return reply.message.synchronous ? this.writeSyncReply(message) : this.send(message)
  }
  fail(error) { this.transportFaults.push(errorData(error)); this.kill() }
  observeClosed(state) {
    this.phase = 'Closed'
    this.deadline.close()
    this.result.resolve(this.abandonRequested ? { kind: 'Abandoned', programSettled: false, state } : { kind: this.watchdogFired ? 'TimedOut' : 'Closed', state })
    this.closed.resolve(state)
    this.revokeCommands('NATIVE_CLOSED')
  }
  async retainUntilClosed() {
    while (this.phase === 'ClosingUnknown') {
      await new Promise(resolve => setTimeout(resolve, 25))
      try {
        const end = await this.session.wait(5000)
        if (end.kind === 'Closed') { this.observeClosed(end.state); return }
      } catch (error) { this.transportFaults.push(errorData(error)) }
    }
  }
  async drain() {
    try {
      while (true) {
        const output = await this.session.takeOutput()
        if (output.kind === 'Chunk') {
          if (output.channel === 'stderr') this.stderr.push(Buffer.from(output.bytes))
          else for (const message of this.reader.push(output.bytes)) this.dispatch(message)
        } else if (output.kind === 'Closed') {
          try { this.reader.finish() } catch (error) { this.transportFaults.push(errorData(error)) }
          this.observeClosed(output.state)
          return
        } else throw new Error('CAPTURE_' + output.kind)
      }
    } catch (error) {
      this.transportFaults.push(errorData(error))
      this.kill()
      this.session.discardOutput()
      this.deadline.close()
      let end
      try { end = await this.session.wait(5000) }
      catch (failure) { this.transportFaults.push(errorData(failure)); end = { kind: 'Unknown' } }
      if (end.kind === 'Closed') this.observeClosed(end.state)
      else {
        this.phase = 'ClosingUnknown'
        this.result.resolve({ kind: 'CloseUnknown', cause: errorData(error) })
        this.closeUnknown.resolve({ owner: this, cause: errorData(error) })
        this.retainedObserver = this.retainUntilClosed()
      }
    }
  }
  sameIdentity() {
    const current = identity(this.pid)
    for (const key of ['pid', 'ppid', 'pgid', 'birth']) if (current[key] !== this.nativeIdentity[key]) throw new Error('NATIVE_IDENTITY_CHANGED')
    return current
  }
  async park() {
    const request = await bounded(this.parkWait.promise, 5000, 'PARK_REQUEST_TIMEOUT')
    if (this.phase !== 'ParkRequested') throw new Error('PARK_PHASE')
    this.sameIdentity()
    process.kill(-this.pid, 'SIGSTOP')
    for (let attempt = 0; attempt < 100; attempt++) {
      const observed = this.sameIdentity()
      if (observed.status.includes('T')) {
        if (!this.deadline.admit()) throw new Error('PARK_DEADLINE_EXPIRED')
        this.phase = 'Parked'
        this.deadline.suspend()
        this.stoppedIdentity = observed
        this.currentSite = Object.freeze({ ...request, runId: this.runId, observed })
        return this.currentSite
      }
      await new Promise(resolve => setTimeout(resolve, 5))
    }
    throw new Error('STOP_NOT_OBSERVED')
  }
  async resume(site, value, { budgetMs = 3000, abandon = false } = {}) {
    if (this.phase !== 'Parked' || site !== this.currentSite || site.runId !== this.runId || site.generation !== this.generation) throw new Error('HANDLE_NOT_PARKED')
    this.sameIdentity()
    this.currentSite = null
    this.phase = 'Resuming'
    this.abandonRequested = abandon
    this.resumed = deferred()
    this.parkWait = deferred()
    const control = { kind: 'resume', generation: this.generation, value, abandon }
    if (abandon) {
      this.deadline.enterCleanup(Date.now() + budgetMs)
      this.lastDeadline = this.deadline.current
      this.deadline.activate()
    } else this.arm(Date.now() + budgetMs)
    process.kill(-this.pid, 'SIGCONT')
    this.send(control).catch(error => this.fail(error))
    const resumed = await bounded(this.resumed.promise, budgetMs + 500, 'RESUME_ACK_TIMEOUT')
    return resumed
  }
  kill() {
    if (!this.session || this.phase === 'Closed') return
    if (this.phase !== 'ClosingUnknown') this.phase = 'Closing'
    this.deadline.close()
    this.revokeCommands('OWNER_REVOKED')
    this.session.signal('SIGKILL')
  }
  releaseReply() {
    if (this.replyFd !== null) { fs.closeSync(this.replyFd); this.replyFd = null }
    if (this.fifoIdentity && fs.existsSync(this.fifoPath)) {
      const current = fs.lstatSync(this.fifoPath)
      if (current.dev !== this.fifoIdentity.dev || current.ino !== this.fifoIdentity.ino) throw new Error('FIFO_IDENTITY_CHANGED')
      fs.unlinkSync(this.fifoPath)
    }
    this.fifoIdentity = null
  }
  async dispose() {
    this.deadline.close()
    if (!this.session) {
      this.releaseReply()
      return { kind: 'NotStarted' }
    }
    if (this.phase === 'ClosingUnknown') throw Object.assign(new Error('CLOSE_UNCONFIRMED'), { cleanupOwner: this })
    if (this.phase !== 'Closed') this.kill()
    let state
    try { state = await bounded(this.closed.promise, 6000, 'CLOSE_UNCONFIRMED') }
    catch (error) { throw Object.assign(error, { cleanupOwner: this }) }
    await Promise.allSettled([...this.accepted, this.inputChain, this.syncChain])
    this.releaseReply()
    if (this.pid && !absent(this.pid)) throw new Error('OWNED_PROCESS_STILL_LIVE')
    return state
  }
}
