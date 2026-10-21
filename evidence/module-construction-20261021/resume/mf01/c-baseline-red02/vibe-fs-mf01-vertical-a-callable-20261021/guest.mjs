// Trusted isolated guest bootstrap; model execution never runs in the Host.
import * as fs from 'node:fs'
import { frame, FrameReader, errorData } from './wire.mjs'
import { Sandbox } from './dependencies/Sandbox.mjs'
import { callableBase } from './callable-realm.mjs'

const [runId, fifoPath, signalFixture] = process.argv.slice(2)
const fixture = JSON.parse(signalFixture)
const termIgnoring = fixture.ignoreTerm
if (termIgnoring) process.on('SIGTERM', () => {})
const replyFd = fs.openSync(fifoPath, fs.constants.O_RDONLY)
const asyncReader = new FrameReader()
const syncReader = new FrameReader()
const pending = new Map()
let sequence = 0
let generation = 0
let parked = null
let activation = null
let handle = null
let abandoning = false
let ended = false
let callablePort = null
const commands = new Set()
const law = Sandbox()
const send = message => {
  const bytes = frame({ ...message, runId })
  for (let offset = 0; offset < bytes.length;) offset += fs.writeSync(1, bytes, offset, bytes.length - offset)
}
const syncReply = requestId => {
  const scratch = Buffer.alloc(16384)
  while (true) {
    const count = fs.readSync(replyFd, scratch, 0, scratch.length, null)
    if (count === 0) throw new Error('SYNC_REPLY_EOF')
    for (const message of syncReader.push(scratch.subarray(0, count))) {
      if (message.runId !== runId || message.requestId !== requestId || message.kind !== 'reply') throw new Error('SYNC_REPLY_FENCE')
      if (!message.ok) throw Object.assign(new Error(message.error.message), { code: message.error.code })
      return message.value
    }
  }
}
const request = (operation, args, synchronous) => {
  const requestId = ++sequence
  const message = { kind: 'request', generation, requestId, operation, args, synchronous }
  if (synchronous) { send(message); return syncReply(requestId) }
  return new Promise((resolve, reject) => {
    pending.set(requestId, { resolve, reject })
    send(message)
  })
}
const api = {
  runId,
  installCallablePort: port => {
    if (callablePort) throw new Error('CALLABLE_PORT_ALREADY_INSTALLED')
    callablePort = port
  },
  offerCallable: ref => send({ kind: 'callable-offer', generation, ref }),
  write: (...args) => request('write', args, true),
  cat: (...args) => request('cat', args, false),
  rm: (...args) => request('rm', args, true),
  park: payload => {
    if (abandoning) return Promise.resolve({ ok: false, code: 'CANNOT_PARK_WHILE_ABANDONING' })
    if (parked) throw new Error('PARK_ALREADY_PENDING')
    generation += 1
    const promise = new Promise((resolve, reject) => { parked = { resolve, reject, generation } })
    send({ kind: 'park', generation, payload })
    if (handle) handle.pause()
    return promise
  },
}
const fail = error => {
  if (ended) return
  ended = true
  send({ kind: 'failure', error: errorData(error) })
  process.stdin.destroy()
  fs.closeSync(replyFd)
  process.exitCode = 1
}
const onCommand = message => {
  if (message.runId !== runId) throw new Error('COMMAND_RUN_FENCE')
  if (message.kind === 'start') {
    if (handle) throw new Error('ALREADY_STARTED')
    handle = law.run(api, 60000, message.deadline, callableBase, `class Js extends JsProgram { async run() { ${message.source}\n} }`)
    if (parked) handle.pause()
    handle.programPromise.then(json => {
      ended = true
      send(abandoning ? { kind: 'abandoned' } : { kind: 'result', value: JSON.parse(json) })
      process.stdin.destroy()
      fs.closeSync(replyFd)
    }, fail)
  } else if (message.kind === 'callable-command') {
    if (!callablePort || ended || commands.has(message.commandId) || message.generation !== generation) throw new Error('CALLABLE_COMMAND_FENCE')
    commands.add(message.commandId)
    Promise.resolve().then(() => callablePort(message.command)).then(
      value => {
        send({ kind: 'callable-reply', commandId: message.commandId, ok: true, value })
        if (message.command.operation === 'finishOffer') return callablePort({ operation: 'completeOffer', ref: message.command.ref })
      },
      error => send({ kind: 'callable-reply', commandId: message.commandId, ok: false, error: { ...errorData(error), code: error?.__jsFailure?.code ?? error?.code, family: error?.__jsAbandoned ? 'Abandoned' : error?.__jsFailure ? 'JsFailure' : 'Exception' } }),
    ).catch(fail)
  } else if (message.kind === 'reply') {
    const wait = pending.get(message.requestId)
    if (!wait) throw new Error('UNKNOWN_ASYNC_REPLY')
    pending.delete(message.requestId)
    if (message.ok) wait.resolve(message.value)
    else wait.reject(Object.assign(new Error(message.error.message), { code: message.error.code }))
  } else if (message.kind === 'resume') {
    if (!parked || activation || message.generation !== parked.generation) throw new Error('RESUME_GENERATION')
    activation = message
    law.resetDeadline(handle.deadlineRef, message.deadline)
    send({ kind: 'control-ack', generation })
  } else if (message.kind === 'activate') {
    if (!activation || message.generation !== generation) throw new Error('ACTIVATE_GENERATION')
    const action = activation
    activation = null
    const deferred = parked
    parked = null
    abandoning = action.abandon
    handle.resume()
    send({ kind: 'activated', generation })
    if (abandoning) {
      deferred.reject(Object.assign(new Error('ABANDONED'), { __jsAbandoned: true }))
      if (callablePort) Promise.resolve(callablePort({ operation: 'abandonOffers' })).catch(fail)
    }
    else deferred.resolve(action.value)
  } else throw new Error('UNKNOWN_COMMAND')
}
process.stdin.on('data', bytes => {
  try { for (const message of asyncReader.push(bytes)) onCommand(message) }
  catch (error) { fail(error) }
})
process.stdin.on('end', () => { if (!ended) fail(new Error('COMMAND_EOF')) })
if (!fixture.noReady) send({ kind: 'ready', pid: process.pid, termIgnoring })
