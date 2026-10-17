import assert from 'node:assert/strict'
import fs from 'node:fs'
import net from 'node:net'
import { dirname, join } from 'node:path'
import { fileURLToPath, pathToFileURL } from 'node:url'
import { randomUUID, createHash } from 'node:crypto'
import { tmpdir } from 'node:os'
import { setTimeout as delay } from 'node:timers/promises'

const surface = process.env.WXS_PACKAGE_BASE
  ? pathToFileURL(join(process.env.WXS_PACKAGE_BASE, 'dist/Process/Surface.js')).href
  : new URL('../../../../dist/Process/Surface.js', import.meta.url).href
const { ptyRecoveryOpen, ptyRecoveryDrain } = await import(surface)
const mode = process.argv[2]
const root = fs.mkdtempSync(join(tmpdir(), 'wxs-publication-evidence-'))
const directory = join(root, 'physical')
const trace = join(root, 'events.jsonl')
const marker = join(root, 'commands')
const ready = join(root, 'ready')
const key = randomUUID()
const incarnation = randomUUID()
const previousOptions = process.env.BUN_OPTIONS
const previousTrace = process.env.WXS_PUBLICATION_TRACE
const previousMode = process.env.WXS_PUBLICATION_MODE
const preload = fileURLToPath(new URL('./pty-recovery-publication-preload.cjs', import.meta.url))
process.env.BUN_OPTIONS = `${previousOptions ?? ''} --preload=${preload}`.trim()
process.env.WXS_PUBLICATION_TRACE = trace
process.env.WXS_PUBLICATION_MODE = mode
const records = () => fs.existsSync(trace) ? fs.readFileSync(trace, 'utf8').trim().split('\n').map(JSON.parse) : []
const read = name => fs.existsSync(join(directory, name)) ? JSON.parse(fs.readFileSync(join(directory, name), 'utf8')) : null
const alive = pid => {
  try {
    process.kill(pid, 0)
    return true
  } catch (error) {
    if (error.code !== 'ESRCH') throw error
    return false
  }
}
const until = async (condition, message, milliseconds = 5000) => {
  const deadline = Date.now() + milliseconds
  while (!condition()) {
    assert.ok(Date.now() < deadline, message)
    await delay(10)
  }
}
const refuse = (request, secret, identity) => new Promise((resolve, reject) => {
  const socket = net.createConnection(request.endpoint)
  const timer = setTimeout(() => socket.destroy(new Error('the unauthenticated connection must be refused')), 2000)
  let text = ''
  let reason
  socket.setEncoding('utf8')
  socket.on('connect', () => socket.write(`${JSON.stringify({ kind: 'attach', mode: 'recover', key, incarnation: identity, secret })}\n`))
  socket.on('data', chunk => {
    text += chunk
    if (!text.includes('\n')) return
    const frame = JSON.parse(text.slice(0, text.indexOf('\n')))
    assert.equal(frame.kind, 'rejected')
    reason = frame.reason
    socket.end()
  })
  socket.on('error', reject)
  socket.on('close', () => {
    clearTimeout(timer)
    if (reason) resolve(reason)
    else reject(new Error('missing authentication rejection'))
  })
})
let terminal
let owner
let endpoint
let fault
let exits = 0
try {
  const command = mode === 'stopped'
    ? `printf 'once\\n' >> '${marker}'; trap 'exit 23' TERM; printf 'ready' > '${ready}'; while :; do sleep 0.05; done`
    : `printf 'once\\n' >> '${marker}'; printf 'PUBLICATION_OUTPUT'; exit 23`
  terminal = ptyRecoveryOpen(directory, key, incarnation, command, root)
  terminal.onData(() => {})
  terminal.onExit(() => { exits++ })
  terminal.onFault(reason => { fault = reason })
  await until(() => read('available'), 'the exact custodian identity must be available')
  owner = read('available').owner
  const request = read('request')
  endpoint = request.endpoint
  if (mode === 'stopped') {
    await until(() => fs.existsSync(ready), 'the real target must install its TERM handler')
    await terminal.signal('SIGTERM')
  }
  await until(() => records().some(row => row.kind === 'inner-before-remove'), 'capture the genuine inner scope end before cleanup')
  const inner = records().find(row => row.kind === 'inner-before-remove').state
  assert.deepEqual(inner['scope-ended'].targetExit, { code: 23, signal: null })
  await delay(100)
  const pending = fs.readdirSync(directory).filter(name => name.startsWith('terminal.') && name.endsWith('.pending')).map(name => ({ name, receipt: read(name).receipt }))
  const snapshot = { mode, keyHash: createHash('sha256').update(key).digest('hex'), incarnation, inner, pending, outerFault: read('fault'), outerTerminal: read('terminal')?.receipt ?? null, client: terminal.inspect(), fault, exits }
  fs.writeFileSync(join(root, 'at-failure.json'), JSON.stringify(snapshot, null, 2))
  assert.match(fault ?? '', mode.startsWith('directory-sync') ? /directory fsync EIO/ : /terminal rename EIO/)
  assert.equal(exits, 0, 'the original creator must retain its failed observation')
  assert.equal(fs.readFileSync(marker, 'utf8'), 'once\n')
  if (mode.startsWith('directory-sync')) {
    assert.ok(records().some(row => row.kind === 'directory-fsync-failed'))
    if (mode === 'directory-sync-read') assert.ok(records().some(row => row.kind === 'terminal-read-failed'))
    assert.equal(read('terminal').receipt.result.kind, 'Ended')
    const recovered = await ptyRecoveryDrain(directory, key, incarnation, 20)
    assert.deepEqual(recovered.exit, { code: 23, signal: null })
    await until(() => !alive(owner), 'the owner must retire after reporting failed publication of a readable native receipt')
  } else {
    assert.equal(read('terminal'), null)
    assert.ok(pending.length > 0)
    if (mode === 'double-failure') {
      assert.equal(read('fault'), null)
      assert.match(fault, /fault rename ENOSPC/)
    }
    const beforeAuthentication = records().filter(row => row.kind === 'terminal-publication').length
    await refuse(request, 'wrong-secret', incarnation)
    await refuse(request, request.secret, 'wrong-incarnation')
    assert.equal(records().filter(row => row.kind === 'terminal-publication').length, beforeAuthentication)
    if (mode === 'owner-lost') {
      process.kill(owner, 'SIGKILL')
      await until(() => !alive(owner), 'the test custodian must exit before the cold proof')
      await assert.rejects(ptyRecoveryDrain(directory, key, incarnation, 20), /connect|connection|unavailable|ECONNREFUSED/)
      assert.equal(read('terminal'), null, 'a signed pending file must not be treated as a published receipt')
    } else {
      await assert.rejects(ptyRecoveryDrain(directory, key, incarnation, 20), /terminal rename EIO/)
      assert.equal(read('terminal'), null)
      const recovered = await ptyRecoveryDrain(directory, key, incarnation, 20)
      assert.deepEqual(recovered.exit, { code: 23, signal: null })
      assert.equal(recovered.stopped, mode === 'stopped')
      assert.equal(recovered.incarnation, incarnation)
      assert.equal(records().filter(row => row.kind === 'terminal-publication').length, 3)
      await until(() => !alive(owner), 'authenticated recovery must retire the exact completed custodian')
    }
  }
  assert.equal(fs.readFileSync(marker, 'utf8'), 'once\n')
  assert.equal(exits, 0)
  fs.writeFileSync(join(root, 'verified.json'), JSON.stringify({ mode, commands: 1, nativeExit: 23, creatorExits: exits, ownerExited: true }))
  console.log(JSON.stringify({ mode, root, commands: 1, ownerExited: true }))
} finally {
  terminal?.dispose()
  owner ??= read('available')?.owner
  endpoint ??= read('request')?.endpoint
  if (owner && alive(owner)) {
    process.kill(owner, 'SIGKILL')
    await until(() => !alive(owner), 'clean only the test custodian after preserving its native exit')
  }
  if (endpoint) {
    if (fs.existsSync(endpoint)) fs.unlinkSync(endpoint)
    if (fs.existsSync(dirname(endpoint))) fs.rmdirSync(dirname(endpoint))
  }
  fs.rmSync(directory, { recursive: true, force: true })
  for (const [name, value] of [['BUN_OPTIONS', previousOptions], ['WXS_PUBLICATION_TRACE', previousTrace], ['WXS_PUBLICATION_MODE', previousMode]]) {
    if (value === undefined) delete process.env[name]
    else process.env[name] = value
  }
}
