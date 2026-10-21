import assert from 'node:assert/strict'
import files from 'node:fs'
import processes from 'node:child_process'
import { join } from 'node:path'
import { tmpdir } from 'node:os'
import { setTimeout as delay } from 'node:timers/promises'
import { mock } from 'bun:test'

const mode = process.argv[2]
const kind = process.argv[3]
assert.ok(['scope', 'recovery'].includes(mode), 'a known PTY probe mode is required')
assert.ok(['valid', 'missing', 'file', 'relative', 'empty', 'null'].includes(kind), 'a known cwd case is required')
const root = files.mkdtempSync(join(tmpdir(), 'wxs-pty-cwd-'))
const temporary = join(root, 'temporary')
const valid = join(root, 'valid')
const file = join(root, 'ordinary-file')
const recovery = join(root, 'recovery')
const witness = join(root, 'executed')
files.mkdirSync(temporary)
files.mkdirSync(valid)
files.writeFileSync(file, 'not a directory')
process.env.TMPDIR = temporary

const cwd = {
  valid,
  missing: join(root, 'missing'),
  file,
  relative: 'relative-pty-cwd',
  empty: '',
  null: null,
}[kind]
const shouldStart = ['valid', 'empty', 'null'].includes(kind)
const created = []
let custodianSpawns = 0
let terminalSpawns = 0
const makeDirectory = files.mkdtempSync
const launch = processes.spawn
mock.module('node:fs', () => ({
  ...files,
  default: files,
  mkdtempSync(...args) {
    const directory = makeDirectory(...args)
    created.push(directory)
    return directory
  },
}))
mock.module('node:child_process', () => ({
  ...processes,
  default: processes,
  spawn(...args) {
    custodianSpawns += 1
    return launch(...args)
  },
}))
const nativeSpawn = (await import('bun-pty')).spawn
mock.module('bun-pty', () => ({
  spawn(...args) {
    terminalSpawns += 1
    return nativeSpawn(...args)
  },
}))
const runtime = await import('../../../../dist/Process/Surface.js')
const quote = value => `'${value.replaceAll("'", "'\\''")}'`
const alive = pid => {
  try { process.kill(pid, 0); return true } catch (error) {
    if (error.code === 'ESRCH') return false
    throw error
  }
}
const waitFor = async (predicate, label) => {
  const deadline = performance.now() + 5000
  while (!predicate()) {
    assert.ok(performance.now() < deadline, label)
    await delay(5)
  }
}
let terminal
let ended = false
let output = ''
let timer
try {
  let rejection
  let result
  try {
    const command = `pwd; printf executed > ${quote(witness)}; exit 37`
    terminal = mode === 'scope'
      ? await runtime.ptyScopeOpen(command, cwd)
      : runtime.ptyRecoveryOpen(recovery, 'cwd-key', 'cwd-incarnation', command, cwd)
    result = await new Promise((resolve, reject) => {
      terminal.onData(data => { output += data })
      terminal.onFault(reason => reject(new Error(reason)))
      terminal.onExit(exit => { ended = true; resolve(exit) })
      timer = setTimeout(() => reject(new Error('PTY did not close')), 8000)
    })
  } catch (error) {
    rejection = String(error?.message ?? error)
  } finally {
    clearTimeout(timer)
  }
  if (ended && mode === 'recovery') {
    terminal.dispose()
    const owner = JSON.parse(files.readFileSync(join(recovery, 'available'), 'utf8')).owner
    await waitFor(() => !alive(owner), 'the real custodian must exit after its scope')
  }
  const observed = {
    mode, kind, rejection: rejection ?? null, executed: files.existsSync(witness),
    terminalSpawns, custodianSpawns, temporaryDirectories: created.length,
    recoveryDirectory: files.existsSync(recovery), output: output.trim(), result: result ?? null,
    scopeClosed: mode === 'scope' && ended ? terminal.inspect().phase === 'Closed' : ended,
  }
  console.log(JSON.stringify(observed))
  if (shouldStart) {
    assert.equal(rejection, undefined)
    assert.equal(observed.executed, true)
    assert.equal(observed.output, kind === 'valid' ? files.realpathSync(valid) : process.cwd())
    assert.equal(observed.scopeClosed, true)
    assert.ok(created.length > 0, 'the transparent probe must observe actual physical preparation')
    if (mode === 'scope') {
      assert.equal(terminalSpawns, 1, 'the transparent probe must forward one real PTY spawn')
      assert.deepEqual(result, { code: 37, signal: null })
    } else {
      assert.equal(custodianSpawns, 1, 'the probe must observe the real custodian launch')
      assert.deepEqual(result, { kind: 'Ended', exit: { code: 37, signal: null }, stopped: false })
    }
  } else {
    assert.ok(rejection, 'invalid cwd must be rejected')
    assert.match(rejection, kind === 'missing' ? /ENOENT/ : kind === 'file' ? /not a directory/ : /absolute/)
    assert.equal(observed.executed, false)
    assert.equal(terminalSpawns, 0)
    assert.equal(custodianSpawns, 0)
    assert.equal(created.length, 0)
    assert.equal(observed.recoveryDirectory, false)
  }
} finally {
  if (terminal && !ended) {
    try { terminal.signal('SIGKILL') } catch {}
  }
  if (terminal && mode === 'recovery') terminal.dispose()
  if (!terminal || ended) files.rmSync(root, { recursive: true, force: true })
}
