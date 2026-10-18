import { randomBytes } from 'node:crypto'
import { createServer } from 'node:net'
import { chmodSync } from 'node:fs'
import { resolve } from 'node:path'

export async function createFiniteWaitChannel({ entryFile, onRequest, onError }) {
  const runId = randomBytes(16).toString('hex')
  const capability = randomBytes(32).toString('hex')
  const path = `/tmp/wxs-finite-${runId}.sock`
  const sockets = new Set()
  const pending = new Map()
  let sequence = 0
  let closing = null
  const server = createServer(socket => {
    sockets.add(socket)
    let buffer = Buffer.alloc(0)
    let request = null
    socket.on('error', () => {})
    socket.on('close', () => {
      sockets.delete(socket)
      if (request && pending.delete(request.requestId)) {
        try { onRequest({ ...request, phase: 'cancel' }) } catch (error) { onError(error) }
      }
    })
    socket.on('data', bytes => {
      if (request) return socket.destroy()
      buffer = Buffer.concat([buffer, bytes])
      if (buffer.length > 4096) return socket.destroy()
      const end = buffer.indexOf(10)
      if (end < 0) return
      let frame
      try { frame = JSON.parse(buffer.subarray(0, end).toString('utf8')) } catch { return socket.destroy() }
      if (!frame || typeof frame !== 'object' || Array.isArray(frame) ||
          frame.capability !== capability || !['begin', 'end'].includes(frame.phase)) return socket.destroy()
      sequence += 1
      request = { entryFile: resolve(entryFile), runId, requestId: sequence,
        contextToken: frame.contextToken, phase: frame.phase, name: frame.name,
        fullName: frame.fullName, operation: frame.operation, limitMs: frame.limitMs }
      pending.set(sequence, socket)
      try { onRequest(request) } catch (error) { onError(error) }
    })
  })
  server.on('error', onError)
  await new Promise((accept, reject) => {
    server.once('error', reject)
    server.listen(path, () => {
      server.removeListener('error', reject)
      accept()
    })
  })
  try { chmodSync(path, 0o600) }
  catch (error) {
    for (const socket of sockets) socket.destroy()
    await new Promise(accept => server.close(accept))
    throw error
  }
  return {
    runId,
    descriptor: { path, capability, entryFile: resolve(entryFile) },
    reply(value) {
      const socket = pending.get(value.requestId)
      if (!socket) return
      pending.delete(value.requestId)
      socket.end(JSON.stringify(value) + '\n')
    },
    close() {
      if (closing) return closing
      pending.clear()
      for (const socket of sockets) socket.destroy()
      closing = new Promise((accept, reject) => server.close(error => error ? reject(error) : accept()))
      return closing
    },
  }
}
