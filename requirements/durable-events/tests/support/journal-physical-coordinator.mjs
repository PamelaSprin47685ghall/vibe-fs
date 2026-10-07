import assert from 'node:assert/strict'
import { spawn } from 'node:child_process'
import fs from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

const [commonDir, writerId, scenario] = process.argv.slice(2)
const root = path.dirname(commonDir)
const variant = scenario.startsWith('business-') ? scenario.slice('business-'.length) : scenario
const fatal = variant.startsWith('malformed')
const childPath = fileURLToPath(new URL('./journal-physical-child.mjs', import.meta.url))
const env = { ...process.env }
delete env.NODE_TEST_CONTEXT
delete env.NODE_OPTIONS
delete env.WANXIANGSHU_NO_FATAL_EXIT
const reportFile = path.join(root, 'journal-fatal-report.ndjson')
const fd = fs.openSync(reportFile, 'wx')
const native = spawn(process.execPath, [childPath, 'measure', commonDir, writerId, scenario, '{}'],
  { cwd: root, env, stdio: ['ignore', 'ignore', fd] })
fs.closeSync(fd)
let failure
let closed = false
let cleanupRequested = false
let terminal
native.once('error', error => { failure = error })
native.once('exit', (exitCode, signal) => { terminal = { exitCode, signal } })
const close = new Promise(resolve => native.once('close', (exitCode, signal) => {
  closed = true
  resolve({ exitCode, signal })
}))
try {
  await close
  if (failure) throw failure
  assert.equal(cleanupRequested, false)
  assert.deepEqual(terminal, fatal ? { exitCode: null, signal: 'SIGKILL' } : { exitCode: 0, signal: null })
  const reports = fs.readFileSync(reportFile, 'utf8').split('\n').filter(Boolean).map(JSON.parse)
  assert.equal(reports.length, fatal ? 1 : 0)
  const receipts = fs.readFileSync(path.join(root, 'journal-settlements.ndjson'), 'utf8')
    .trimEnd().split('\n').map(JSON.parse)
  assert.ok(receipts.every(receipt => receipt.pid === native.pid && receipt.parentPid === process.pid))
  const returned = fs.existsSync(path.join(root, 'journal-returned.json'))
    ? JSON.parse(fs.readFileSync(path.join(root, 'journal-returned.json'), 'utf8')) : null
  assert.equal(returned === null, fatal)
  fs.writeSync(1, JSON.stringify({ coordinatorPid: process.pid, pid: native.pid, ...terminal,
    cleanupRequested, reports, receipts, returned }) + '\n')
} finally {
  if (!closed) {
    cleanupRequested = true
    native.kill('SIGKILL')
    await close
  }
}
