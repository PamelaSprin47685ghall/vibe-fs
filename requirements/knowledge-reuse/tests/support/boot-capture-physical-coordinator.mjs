import assert from 'node:assert/strict'
import { spawn } from 'node:child_process'
import fs from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

const [directory, scenario] = process.argv.slice(2)
const fatal = scenario.startsWith('malformed')
const env = { ...process.env, WANXIANGSHU_PROVIDER_LANGUAGE: 'en' }
delete env.NODE_TEST_CONTEXT
delete env.NODE_OPTIONS
delete env.WANXIANGSHU_NO_FATAL_EXIT
const fatalFile = path.join(directory, 'fatal-report.ndjson')
const fatalFd = fs.openSync(fatalFile, 'wx')
const child = spawn(process.execPath, [fileURLToPath(new URL('./boot-capture-physical-child.mjs', import.meta.url)),
  'measure', directory, scenario, '{}'], { cwd: directory, env, stdio: ['ignore', 'pipe', fatalFd] })
fs.closeSync(fatalFd)
let closed = false
let failure
let stdout = ''
let cleanupRequested = false
child.once('error', error => { failure = error })
child.stdout.on('data', chunk => { stdout += chunk.toString('utf8') })
child.stdout.once('error', error => { failure ??= error })
const close = new Promise(resolve => child.once('close', (exitCode, signal) => {
  closed = true
  resolve({ exitCode, signal })
}))
try {
  const terminal = await close
  if (failure) throw failure
  assert.equal(stdout, '', 'the original finalization path does not emit a normal answer')
  assert.equal(cleanupRequested, false)
  assert.deepEqual(terminal, fatal ? { exitCode: null, signal: 'SIGKILL' } : { exitCode: 0, signal: null })
  const reports = fs.readFileSync(fatalFile, 'utf8').split('\n').filter(line => line.length > 0).map(JSON.parse)
  assert.equal(reports.length, fatal ? 1 : 0)
  const measured = JSON.parse(fs.readFileSync(path.join(directory, 'settlement.json'), 'utf8'))
  assert.equal(measured.pid, child.pid)
  assert.equal(measured.parentPid, process.pid)
  const returned = fs.existsSync(path.join(directory, 'returned.json'))
    ? JSON.parse(fs.readFileSync(path.join(directory, 'returned.json'), 'utf8')) : null
  assert.equal(returned === null, fatal)
  fs.writeSync(1, JSON.stringify({ coordinatorPid: process.pid, pid: child.pid,
    ...terminal, cleanupRequested, reports, measured, returned }) + '\n')
} finally {
  if (!closed) {
    cleanupRequested = true
    child.kill('SIGKILL')
    await close
  }
}
