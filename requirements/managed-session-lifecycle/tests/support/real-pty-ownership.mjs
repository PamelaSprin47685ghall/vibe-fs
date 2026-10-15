import assert from 'node:assert/strict'
import { Database } from 'bun:sqlite'
import { existsSync, readFileSync } from 'node:fs'
import { join } from 'node:path'
import { setTimeout as delay } from 'node:timers/promises'
import { realOwnershipProbe, realSharedOwnershipProbe } from '../../../../dist/Execution/Delegation/Fork/Host/HostForkPtySurface.js'
import { loseOwnershipCommit } from './ownership-commit.mjs'

const [directory, mode] = process.argv.slice(2)
const quote = value => `'${value.replaceAll("'", "'\\''")}'`
const alive = pid => {
  try {
    process.kill(pid, 0)
    return true
  } catch (error) {
    if (error.code === 'ESRCH') return false
    throw error
  }
}
const waitFor = async (predicate, label) => {
  const deadline = Date.now() + 5000
  while (!predicate()) {
    assert.ok(Date.now() < deadline, `timed out waiting for ${label}`)
    await delay(5)
  }
}
const pids = []
const descendants = []
const start = async (owner, label) => {
  const marker = join(directory, `${label}-pid`)
  const program = `const fs = require('node:fs'); fs.writeFileSync(${JSON.stringify(`${marker}.tmp`)}, String(process.pid)); fs.renameSync(${JSON.stringify(`${marker}.tmp`)}, ${JSON.stringify(marker)}); setInterval(() => {}, 1000)`
  const opened = await owner.fork(`exec ${quote(process.execPath)} -e ${quote(program)}`)
  assert.equal(opened.ok, true, opened.error)
  await waitFor(() => existsSync(marker), `${label} PTY readiness`)
  const pid = Number(readFileSync(marker, 'utf8'))
  assert.ok(Number.isInteger(pid) && pid > 1)
  pids.push(pid)
  assert.equal(alive(pid), true)
  return { id: opened.id, pid }
}

let first
let second
try {
  if (mode.startsWith('shared-')) {
    const pair = realSharedOwnershipProbe(directory, 'shared-real-owner')
    first = pair.first
    second = pair.second
    const own = await start(first, 'first')
    const other = await start(second, 'second')
    await first[mode === 'shared-cancel' ? 'cancel' : 'detach']()
    await waitFor(() => !alive(own.pid), 'the first owner process exit')
    assert.equal(first.owned(own.id), false)
    assert.equal(alive(other.pid), true)
    assert.equal(second.exists(other.id), true)
    assert.equal(second.owned(other.id), true)
    assert.deepEqual(second.outstanding(), [other.id])
    assert.equal(second.completions(), 0)
    assert.equal(second.exitListeners(), 1)
    await second.detach()
    await waitFor(() => !alive(other.pid), 'the second owner process exit')
    assert.deepEqual(second.outstanding(), [])
    assert.equal(second.exitListeners(), 0)
    console.log(JSON.stringify({ mode, firstExited: true, otherSurvived: true, blockers: [] }))
  } else if (mode === 'leader-exit') {
    first = realOwnershipProbe(directory, 'leader-exit-owner')
    const parentMarker = join(directory, 'first-pid')
    const childMarker = join(directory, 'second-pid')
    const child = `process.on('SIGHUP', () => {}); process.on('SIGTERM', () => {}); const fs = require('node:fs'); fs.writeFileSync(${JSON.stringify(`${childMarker}.tmp`)}, String(process.pid)); fs.renameSync(${JSON.stringify(`${childMarker}.tmp`)}, ${JSON.stringify(childMarker)}); setInterval(() => {}, 1000)`
    const parent = `const fs = require('node:fs'); fs.writeFileSync(${JSON.stringify(`${parentMarker}.tmp`)}, String(process.pid)); fs.renameSync(${JSON.stringify(`${parentMarker}.tmp`)}, ${JSON.stringify(parentMarker)}); require('node:child_process').spawn(process.execPath, ['-e', ${JSON.stringify(child)}], {stdio: 'ignore'}); setInterval(() => {if (fs.existsSync(${JSON.stringify(childMarker)})) process.exit(0)}, 5)`
    const opened = await first.fork(`exec ${quote(process.execPath)} -e ${quote(parent)}`)
    assert.equal(opened.ok, true, opened.error)
    await waitFor(() => existsSync(parentMarker), 'the leader readiness')
    const parentPid = Number(readFileSync(parentMarker, 'utf8'))
    pids.push(parentPid)
    await waitFor(() => existsSync(childMarker), 'the descendant readiness')
    const childPid = Number(readFileSync(childMarker, 'utf8'))
    descendants.push(childPid)
    await waitFor(() => !alive(parentPid), 'the leader exit')
    await waitFor(() => !first.exists(opened.id), 'the leader exit observation')
    await first.closeAll(100)
    await first.detach()
    console.log(JSON.stringify({
      mode, leaderExited: true, descendantAlive: alive(childPid),
      owned: first.owned(opened.id), blockers: first.outstanding(),
    }))
  } else if (mode.startsWith('cancel-')) {
    const [, action, boundary] = mode.split('-')
    first = realOwnershipProbe(directory, 'cancel-real-owner')
    const own = await start(first, 'first')
    const other = await start(first, 'second')
    const close = () => action === 'close'
      ? first.close([own.id, 100])
      : action === 'closeAll' ? first.closeAll(100) : first[action]()
    const { injected } = await loseOwnershipCommit(Database, 'Cancel', boundary === 'after', () =>
      assert.rejects(close(), /CancellationRequested was not acknowledged: Unknown/))
    const closed = action === 'close' ? [own] : [own, other]
    for (const opened of closed) {
      await waitFor(() => !alive(opened.pid), 'the cancelled PTY process exit')
      assert.equal(first.exists(opened.id), false)
      assert.equal(first.owned(opened.id), false)
    }
    assert.deepEqual(first.outstanding(), action === 'close' ? [other.id] : [])
    assert.equal(first.exitListeners(), action === 'detach' || action === 'cancel' ? 0 : 1)
    if (action === 'detach' || action === 'cancel') {
      const next = await first.fork('exit 0')
      assert.equal(next.ok, false, 'a terminated runtime must not accept another physical command')
      assert.deepEqual(first.outstanding(), [])
    }
    if (action === 'close') {
      assert.equal(alive(other.pid), true)
      assert.equal(first.exists(other.id), true)
      assert.equal(first.owned(other.id), true)
      await first.close([other.id, 100])
      await waitFor(() => !alive(other.pid), 'the remaining PTY process exit')
    }
    second = realOwnershipProbe(directory, 'cancel-real-owner')
    assert.deepEqual(second.outstanding(), [])
    await second.detach()
    console.log(JSON.stringify({ mode, injected, failureReported: true, processExited: true, blockers: [] }))
  } else {
    first = realOwnershipProbe(directory, 'ended-real-owner')
    const opened = await start(first, 'first')
    const { injected } = await loseOwnershipCommit(Database, 'Ended', mode === 'ended-after', () => first.detach())
    await waitFor(() => !alive(opened.pid), 'the real PTY process exit')
    assert.equal(first.exists(opened.id), false)
    assert.equal(first.owned(opened.id), false)
    assert.deepEqual(first.outstanding(), [])
    assert.equal(first.exitListeners(), 0)
    second = realOwnershipProbe(directory, 'ended-real-owner')
    assert.deepEqual(second.outstanding(), [])
    await second.detach()
    console.log(JSON.stringify({ mode, injected, processExited: true, blockers: [] }))
  }
} finally {
  for (const pid of descendants) {
    if (alive(pid)) process.kill(pid, 'SIGKILL')
    await waitFor(() => !alive(pid), 'the test descendant cleanup')
  }
  for (const pid of pids) {
    if (alive(pid)) {
      try { process.kill(-pid, 'SIGKILL') } catch (error) {
        if (error.code !== 'ESRCH') throw error
      }
    }
  }
}
