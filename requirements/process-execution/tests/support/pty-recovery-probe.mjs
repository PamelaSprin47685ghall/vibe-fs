import assert from 'node:assert/strict'
import { fork } from 'node:child_process'
import * as fs from 'node:fs'
import net from 'node:net'
import { syncBuiltinESMExports } from 'node:module'
import { randomUUID } from 'node:crypto'
import { join } from 'node:path'
import { tmpdir } from 'node:os'
import { fileURLToPath, pathToFileURL } from 'node:url'
import { setTimeout as delay } from 'node:timers/promises'

const surfaceUrl = process.env.WXS_PACKAGE_BASE
  ? pathToFileURL(join(process.env.WXS_PACKAGE_BASE, 'dist/Process/Surface.js')).href
  : new URL('../../../../dist/Process/Surface.js', import.meta.url).href
const physical = await import(surfaceUrl)
const script = fileURLToPath(import.meta.url)
const read = path => JSON.parse(fs.readFileSync(path, 'utf8'))
const until = async (predicate, label, milliseconds = 8000) => {
  const deadline = Date.now() + milliseconds
  while (!predicate()) {
    assert.ok(Date.now() < deadline, label)
    await delay(10)
  }
}

const refused = (request, secret, action) => new Promise((resolve, reject) => {
  const socket = net.createConnection(request.endpoint)
  socket.setEncoding('utf8')
  const timer = setTimeout(() => socket.destroy(new Error('PTY refusal was not observed')), 5000)
  let pending = ''
  let rejection
  socket.on('connect', () => socket.write(`${JSON.stringify({ kind: 'attach', mode: 'recover', key: request.key, incarnation: request.incarnation, secret })}\n`))
  socket.on('data', data => {
    pending += data
    while (pending.includes('\n')) {
      const delimiter = pending.indexOf('\n')
      const frame = JSON.parse(pending.slice(0, delimiter))
      pending = pending.slice(delimiter + 1)
      if (frame.kind === 'attached' && action) {
        const frames = [{ kind: action, sequence: 1, text: 'must-not-replay\n', signal: 'SIGUSR1' }]
        if (action === 'write') frames.push({ kind: 'signal', sequence: 2, signal: 'SIGUSR1' })
        socket.write(frames.map(value => `${JSON.stringify(value)}\n`).join(''))
      }
      if (frame.kind === 'rejected') {
        rejection = frame.reason
        socket.end()
      }
    }
  })
  socket.on('error', reject)
  socket.on('close', () => {
    clearTimeout(timer)
    if (rejection) resolve(rejection)
    else reject(new Error('PTY connection closed without its rejection'))
  })
})

if (process.argv[2] === 'host') {
  const [, , , directory, key, incarnation, command, mode, cwd] = process.argv
  if (mode === 'not-started') {
    const create = net.createConnection
    net.createConnection = (...args) => {
      const socket = create(...args)
      const on = socket.on
      socket.on = function (name, listener) {
        if (name === 'connect') return this
        return on.call(this, name, listener)
      }
      return socket
    }
    syncBuiltinESMExports()
  }
  const terminal = physical.ptyRecoveryOpen(directory, key, incarnation, command, cwd)
  terminal.onData(data => process.send({ data }))
  terminal.onFault(() => {})
  terminal.onExit(() => {})
  process.on('message', ({ action, args }) => {
    terminal[action](...args)
    process.send({ applied: action })
  })
  process.send({ opened: true })
  setInterval(() => {}, 1000)
} else {
  const root = fs.mkdtempSync(join(tmpdir(), 'wxs-r6c-physical-'))
  const cwd = join(root, 'workspace')
  try {
    fs.mkdirSync(cwd)
    for (const runtime of process.env.WXS_COMPILED_HOST ? ['compiled'] : ['node', 'bun']) {
      for (const mode of ['running', 'exited', 'creator', ...(runtime === 'node' ? ['not-started'] : [])]) {
        const directory = join(root, `${runtime}-${mode}`)
        const target = join(directory, 'target')
        const launches = join(directory, 'launches')
        const input = join(directory, 'input')
        const userSignal = join(directory, 'user-signal')
        const dimensions = join(directory, 'dimensions')
        const environment = join(directory, 'environment')
        const key = randomUUID()
        const incarnation = randomUUID()
        const command = `printf 'launched\\n' >> '${launches}'; printf '%s' "$$" > '${target}.tmp'; mv '${target}.tmp' '${target}'; ${mode === 'exited' ? 'sleep 0.3; exit 23' : mode === 'creator' ? `IFS= read -r value; stty size > '${dimensions}'; printf '%s' "$BUN_BE_BUN" > '${environment}'; printf '%s' "$value" > '${input}.tmp'; mv '${input}.tmp' '${input}'; printf 'creator-output:%s\\n' "$value"; exec sleep 60` : `trap 'printf x > "${userSignal}"' USR1; read value; printf '%s' "$value" > '${input}'; exec sleep 60`}`
        const host = fork(script, ['host', directory, key, incarnation, command, mode, cwd], {
          execPath: runtime === 'compiled' ? process.env.WXS_COMPILED_HOST : runtime,
          stdio: ['ignore', 'ignore', 'pipe', 'ipc'],
          ...(runtime === 'compiled' ? { env: { ...process.env, BUN_BE_BUN: '1' } } : {}),
        })
        let stderr = ''
        let output = ''
        host.stderr.on('data', chunk => { stderr += chunk })
        host.on('message', frame => { if (frame.data) output += frame.data })
        await new Promise((resolve, reject) => {
          host.once('message', resolve)
          host.once('error', reject)
          host.once('exit', code => reject(new Error(`Host exited ${code}: ${stderr}`)))
        })
        let killed = false
        try {
          await until(() => fs.existsSync(join(directory, 'available')), 'the physical owner must become available')
          if (mode !== 'not-started') {
            await until(() => fs.existsSync(target), 'the real target must start')
            assert.throws(() => physical.ptyRecoveryOpen(directory, key, randomUUID(), 'must-not-launch', cwd), /EEXIST/)
            await assert.rejects(() => physical.ptyRecoveryDrain(directory, key, 'old-incarnation', 20), /incarnation mismatch/)
            await assert.rejects(() => physical.ptyRecoveryDrain(directory, 'wrong-key', incarnation, 20), /key mismatch/)
            if (mode === 'running') await assert.rejects(() => physical.ptyRecoveryDrain(directory, key, incarnation, 20), /live control connection/)
          }
          if (mode === 'creator') {
            const control = (action, args) => new Promise(resolve => {
              const applied = frame => {
                if (frame.applied !== action) return
                host.off('message', applied)
                resolve()
              }
              host.on('message', applied)
              host.send({ action, args })
            })
            await control('resize', [113, 47])
            await control('write', ['created-once\n'])
            await until(() => fs.existsSync(input), 'the creator must retain real input access')
            assert.equal(fs.readFileSync(input, 'utf8'), 'created-once')
            assert.equal(fs.readFileSync(dimensions, 'utf8').trim(), '47 113')
            assert.equal(fs.readFileSync(environment, 'utf8'), runtime === 'compiled' ? '1' : process.env.BUN_BE_BUN ?? '')
            await until(() => output.includes('creator-output:created-once'), 'real PTY output must reach the creator')
          }
          const exited = new Promise(resolve => host.once('exit', resolve))
          host.kill('SIGKILL')
          await exited
          killed = true
          if (mode === 'exited' || mode === 'not-started') await until(() => fs.existsSync(join(directory, 'terminal')), 'the exact owner must seal its outcome')
          if (mode === 'running') {
            const request = read(join(directory, 'request'))
            assert.match(await refused(request, 'wrong-secret'), /credentials do not match/)
            assert.match(await refused(request, request.secret, 'write'), /cannot replay input/)
            assert.match(await refused(request, request.secret, 'signal'), /termination signals only/)
            assert.equal(fs.existsSync(input), false)
            assert.equal(fs.existsSync(userSignal), false)
          }
          if (mode === 'exited') {
            const terminal = fs.readFileSync(join(directory, 'terminal'), 'utf8')
            const corrupted = JSON.parse(terminal)
            corrupted.proof = '0'.repeat(64)
            fs.writeFileSync(join(directory, 'terminal'), JSON.stringify(corrupted))
            await assert.rejects(() => physical.ptyRecoveryDrain(directory, key, incarnation, 20), /proof mismatch/)
            fs.writeFileSync(join(directory, 'terminal'), terminal)
          }
          const result = await physical.ptyRecoveryDrain(directory, key, incarnation, 20)
          assert.equal(result.incarnation, incarnation)
          if (mode === 'not-started') {
            assert.equal(result.kind, 'NotStarted')
            assert.equal(fs.existsSync(launches), false)
          } else {
            assert.equal(result.kind, 'Ended')
            assert.equal(fs.readFileSync(launches, 'utf8'), 'launched\n')
            if (mode === 'exited') assert.deepEqual(result.exit, { code: 23, signal: null })
          }
          const owner = read(join(directory, 'available')).owner
          await until(() => {
            try { process.kill(owner, 0); return false } catch (error) {
              if (error.code !== 'ESRCH') throw error
              return true
            }
          }, 'the exact custodian must exit after sealing its outcome')
          console.log(JSON.stringify({ runtime, mode, commands: mode === 'not-started' ? 0 : 1, ownerExited: true, result }))
        } finally {
          if (!killed) {
            const exited = new Promise(resolve => host.once('exit', resolve))
            host.kill('SIGKILL')
            await exited
          }
          try { await physical.ptyRecoveryDrain(directory, key, incarnation, 20) } catch {}
        }
      }
    }
  } finally { fs.rmSync(root, { recursive: true, force: true }) }
}
