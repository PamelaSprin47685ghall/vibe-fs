import { beforeEach, afterEach } from 'node:test'
import { randomBytes } from 'node:crypto'
import { connect } from 'node:net'
import { resolve } from 'node:path'
import { performance } from 'node:perf_hooks'
import { isMainThread } from 'node:worker_threads'
import { validatePhysicalWait } from './native-test-supervision.mjs'

const contexts = new WeakMap()
const REPLY_MS = 1000
let descriptor = null

export function installFinitePhysicalWait(value) {
  if (!isMainThread || typeof process.argv[1] !== 'string' || resolve(process.argv[1]) !== value.entryFile) return false
  if (descriptor !== null) throw new Error('Physical wait transport was already installed')
  descriptor = Object.freeze({ ...value })
  beforeEach(context => {
    const entry = { token: randomBytes(16).toString('hex'), name: context.name,
      fullName: context.fullName, spent: false, sealed: context.signal.aborted }
    contexts.set(context, entry)
    context.signal.addEventListener('abort', () => { entry.sealed = true }, { once: true })
  })
  afterEach(context => {
    const entry = contexts.get(context)
    if (entry) entry.sealed = true
  })
  return true
}

function request(context, entry, phase, operation, limitMs) {
  const sentAt = performance.now()
  return new Promise((accept, reject) => {
    const socket = connect(descriptor.path)
    let buffer = Buffer.alloc(0)
    let settled = false
    const finish = (failure, reply) => {
      if (settled) return
      settled = true
      clearTimeout(timer)
      context.signal.removeEventListener('abort', aborted)
      socket.destroy()
      if (failure) reject(failure)
      else accept(reply)
    }
    const aborted = () => finish(new Error('Physical wait context closed before its reply'))
    const timer = setTimeout(() => finish(new Error(`Physical wait reply deadline expired (${phase}: ${operation})`)), REPLY_MS)
    context.signal.addEventListener('abort', aborted, { once: true })
    socket.on('error', error => finish(error))
    socket.on('close', () => finish(new Error('Physical wait connection closed without a reply')))
    socket.on('connect', () => {
      if (context.signal.aborted || entry.sealed) return aborted()
      socket.write(JSON.stringify({ capability: descriptor.capability, contextToken: entry.token,
        name: entry.name, fullName: entry.fullName, phase, operation, limitMs }) + '\n')
    })
    socket.on('data', bytes => {
      buffer = Buffer.concat([buffer, bytes])
      if (buffer.length > 4096) return finish(new Error('Physical wait reply exceeded its byte boundary'))
      const end = buffer.indexOf(10)
      if (end < 0) return
      if (performance.now() - sentAt >= REPLY_MS) return finish(new Error('Physical wait reply arrived after its deadline'))
      try {
        const reply = JSON.parse(buffer.subarray(0, end).toString('utf8'))
        if (!reply || typeof reply !== 'object' || typeof reply.ok !== 'boolean') throw new Error('Invalid physical wait reply')
        if (reply.contextToken !== entry.token || reply.phase !== phase ||
            reply.runId !== descriptor.runId || reply.entryFile !== descriptor.entryFile) {
          throw new Error('Physical wait reply identity mismatch')
        }
        if (!reply.ok) throw new Error(reply.reason ?? 'Physical wait was refused')
        finish(null, reply)
      } catch (error) { finish(error) }
    })
  })
}

export async function withFinitePhysicalWait(context, { operation, limitMs }, action) {
  validatePhysicalWait(operation, limitMs)
  context.signal.throwIfAborted()
  if (descriptor === null) return action()
  const entry = contexts.get(context)
  if (!entry || entry.sealed) throw new Error('Physical wait requires the live registered TestContext')
  if (entry.spent) throw new Error('Physical wait was already declared for this invocation')
  entry.spent = true
  await request(context, entry, 'begin', operation, limitMs)
  if (entry.sealed || context.signal.aborted) throw new Error('Physical wait context closed after its grant')
  let result
  let failure
  try { result = await action() } catch (error) { failure = { error } }
  try { await request(context, entry, 'end', operation) }
  catch (error) {
    if (failure) throw new AggregateError([failure.error, error], 'Physical probe and end acknowledgement failed', { cause: failure.error })
    throw error
  }
  if (failure) throw failure.error
  return result
}
