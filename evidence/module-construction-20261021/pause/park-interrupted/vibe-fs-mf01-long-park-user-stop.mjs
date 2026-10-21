import assert from 'node:assert/strict'
import { readFileSync, writeFileSync } from 'node:fs'
import { spawnSync } from 'node:child_process'
import { setTimeout as sleep } from 'node:timers/promises'

const base = '/private/tmp/vibe-fs-mf01-long-park'
const receipt = JSON.parse(readFileSync(base + '-user-stop.json', 'utf8'))
const state = receipt.originalStoppedState
const gone = pid => assert.throws(() => process.kill(pid, 0), { code: 'ESRCH' })
const save = () => writeFileSync(base + '-user-stop.json', JSON.stringify(receipt, null, 2))
const ps = (pid, fields) => {
  const result = spawnSync('/bin/ps', ['-p', String(pid), '-o', fields], { encoding: 'utf8', timeout: 1500, killSignal: 'SIGKILL' })
  if (result.pid) gone(result.pid)
  assert.ifError(result.error)
  assert.equal(result.status, 0, result.stderr)
  return result.stdout.trim()
}
const identity = ps(state.targetPid, 'pid=,ppid=,pgid=,stat=,lstart=')
const match = identity.match(/^(\d+)\s+(\d+)\s+(\d+)\s+(\S+)\s+(.+)$/)
assert.ok(match, identity)
assert.deepEqual({ pid: Number(match[1]), ppid: Number(match[2]), pgid: Number(match[3]), birth: match[5] }, state.identity)
assert.match(match[4], /T/)
const ownerCommand = ps(state.harnessPid, 'command=')
assert.equal(ownerCommand, '/private/tmp/vibe-fs-sm07-node22.h1udgm/node-v22.13.0-darwin-arm64/bin/node /private/tmp/vibe-fs-mf01-long-park.mjs')
receipt.lastStoppedIdentity = identity
receipt.ownerCommand = ownerCommand
receipt.continuedAt = new Date().toISOString()
receipt.stoppedWallMs = Date.parse(receipt.continuedAt) - Date.parse(state.stoppedAt)
receipt.oneHourCompleted = false
save()
process.kill(state.targetPid, 'SIGCONT')
const deadline = performance.now() + 5000
let resumed
while (performance.now() < deadline) {
  const events = readFileSync(base + '-samples.jsonl', 'utf8').trim().split('\n').map(line => JSON.parse(line))
  resumed = events.find(event => event.kind === 'child-frame' && event.frame.kind === 'resumed')
  if (resumed) break
  await sleep(20)
}
assert.ok(resumed, 'original owner did not observe resumed continuation receipt')
assert.deepEqual(resumed.frame, { kind: 'resumed', pid: state.targetPid, parentPid: state.harnessPid, nonce: state.nonce, keep: 42, messageCount: 1, messages: [{ id: state.nonce, value: 'queued-once-汉🔥' }] })
for (let attempt = 0; attempt < 50; attempt++) {
  try { gone(state.targetPid); break } catch (error) { if (attempt === 49) throw error; await sleep(20) }
}
receipt.applicationReceipt = resumed
receipt.targetGoneBeforeOwnerCancel = true
receipt.ownerCancelledAt = new Date().toISOString()
save()
assert.equal(ps(state.harnessPid, 'command='), ownerCommand)
process.kill(state.harnessPid, 'SIGTERM')
for (let attempt = 0; attempt < 100; attempt++) {
  try { gone(state.harnessPid); break } catch (error) { if (attempt === 99) throw error; await sleep(20) }
}
gone(state.targetPid)
receipt.harnessGone = true
receipt.targetGone = true
receipt.completedAt = new Date().toISOString()
receipt.ownerFinalState = JSON.parse(readFileSync(base + '-state.json', 'utf8'))
assert.deepEqual(receipt.ownerFinalState.nativeClosed.exit, { code: 0, signal: null })
assert.equal(receipt.ownerFinalState.nativeClosed.failure, null)
assert.equal(receipt.ownerFinalState.cleanup.closed, 'Closed')
assert.equal(receipt.ownerFinalState.cleanup.targetGone, true)
save()
console.log(JSON.stringify({ stoppedWallMs: receipt.stoppedWallMs, continuedAt: receipt.continuedAt, closure: resumed.frame.keep, messageCount: resumed.frame.messageCount, targetGone: true, harnessGone: true, nativeExit: receipt.ownerFinalState.nativeClosed.exit, oneHourCompleted: false }))
