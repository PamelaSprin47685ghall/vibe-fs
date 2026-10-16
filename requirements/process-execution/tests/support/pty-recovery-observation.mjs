import assert from 'node:assert/strict'
import fs from 'node:fs'
import net from 'node:net'
import { syncBuiltinESMExports } from 'node:module'
import { join } from 'node:path'
import { tmpdir } from 'node:os'
import { randomUUID } from 'node:crypto'
import { pathToFileURL } from 'node:url'
import { setTimeout as delay } from 'node:timers/promises'

const surfaceUrl = process.env.WXS_PACKAGE_BASE
  ? pathToFileURL(join(process.env.WXS_PACKAGE_BASE, 'dist/Process/Surface.js')).href
  : new URL('../../../../dist/Process/Surface.js', import.meta.url).href
const { ptyRecoveryOpen, ptyRecoveryDrain } = await import(surfaceUrl)
const scenario = process.argv[2]
const neverStarted = scenario === 'not-started-race'
const receiptRace = neverStarted || scenario === 'ended-race'
const interrupted = scenario === 'truncated'
const expected = 'x'.repeat(scenario === 'paused-large' ? 1048576 : 0) + 'R6C_EXACT_OUTPUT'
const directory = fs.mkdtempSync(join(tmpdir(), 'wxs-r6c-observation-'))
const marker = join(directory, 'launches')
const key = randomUUID()
const incarnation = randomUUID()
const onSocket = net.Socket.prototype.on
const exists = fs.existsSync
const unhandled = []
let intercepted = false
let terminal
let text = ''
let textAtExit = ''
let exits = 0
let fault
let completion
const quote = value => `'${value.replaceAll("'", "'\\''")}'`
const until = async (predicate, label) => {
  const deadline = Date.now() + 8000
  while (!predicate()) {
    assert.ok(Date.now() < deadline, label)
    await delay(5)
  }
}
process.on('unhandledRejection', error => unhandled.push(String(error.message)))
net.Socket.prototype.on = function (event, listener) {
  if (event !== 'connect') return onSocket.call(this, event, listener)
  intercepted = true
  if (neverStarted) return this
  const socket = this
  return onSocket.call(this, event, (...values) => {
    listener(...values)
    if (!receiptRace) {
      socket.pause()
      setTimeout(() => interrupted ? socket.destroy() : socket.resume(), 500)
    }
  })
}
syncBuiltinESMExports()
try {
  const command = `printf 'launched\\n' >> ${quote(marker)}; exec node -e ${quote(`process.stdout.write("x".repeat(${expected.length - 16})+"R6C_EXACT_OUTPUT")`)}`
  terminal = ptyRecoveryOpen(directory, key, incarnation, command, directory)
  terminal.onData(value => { text += value })
  terminal.onFault(reason => { fault = reason })
  terminal.onExit(value => {
    textAtExit = text
    completion = value
    exits++
  })
  await until(() => exists(join(directory, 'terminal')), 'the real owner must seal its native outcome')
  assert.equal(intercepted, true, 'the test must control the real client socket')
  if (receiptRace) {
    let hidden = false
    net.Socket.prototype.on = onSocket
    fs.existsSync = path => {
      if (!hidden && path === join(directory, 'terminal')) {
        hidden = true
        return false
      }
      return exists(path)
    }
    syncBuiltinESMExports()
    const result = await ptyRecoveryDrain(directory, key, incarnation, 20)
    assert.equal(hidden, true)
    assert.equal(result.kind, neverStarted ? 'NotStarted' : 'Ended')
    assert.equal(result.incarnation, incarnation)
    fs.existsSync = exists
    syncBuiltinESMExports()
  } else if (interrupted) {
    await until(() => fault !== undefined || exits > 0, 'the disconnected creator must report its observation')
    assert.match(fault ?? '', /connection|stream/i)
    assert.equal(exits, 0, 'a durable physical receipt must not conceal an incomplete creator stream')
    net.Socket.prototype.on = onSocket
    syncBuiltinESMExports()
    const recovered = await ptyRecoveryDrain(directory, key, incarnation, 20)
    assert.equal(recovered.kind, 'Ended', 'a fresh recovery can still settle the exact native exit')
    assert.deepEqual(recovered.exit, { code: 0, signal: null })
  } else {
    await until(() => exits > 0 || fault !== undefined, 'the creator must receive its terminal stream')
    assert.equal(fault, undefined)
    assert.equal(textAtExit.length, expected.length)
    assert.equal(textAtExit, expected)
    assert.deepEqual(completion.exit, { code: 0, signal: null })
  }
  const custodian = JSON.parse(fs.readFileSync(join(directory, 'available'), 'utf8')).owner
  await until(() => {
    try { process.kill(custodian, 0); return false } catch (error) {
      if (error.code !== 'ESRCH') throw error
      return true
    }
  }, 'the physical custodian must exit after its bounded stream shutdown')
  await delay(50)
  assert.ok(exits <= 1)
  assert.deepEqual(unhandled, [])
  assert.equal(neverStarted ? fs.existsSync(marker) : fs.readFileSync(marker, 'utf8'), neverStarted ? false : 'launched\n')
  console.log(JSON.stringify({ scenario, bytesAtExit: textAtExit.length, exits, fault, commands: neverStarted ? 0 : 1, custodianExited: true }))
} finally {
  net.Socket.prototype.on = onSocket
  fs.existsSync = exists
  syncBuiltinESMExports()
  terminal?.dispose()
  try { await ptyRecoveryDrain(directory, key, incarnation, 20) } catch {}
  fs.rmSync(directory, { recursive: true, force: true })
}
