import assert from 'node:assert/strict'
import { existsSync, readFileSync } from 'node:fs'
import { join } from 'node:path'
import { setTimeout as delay } from 'node:timers/promises'
import { realOwnershipProbe } from '../../../../dist/Execution/Delegation/Fork/Host/HostForkPtySurface.js'

const directory = process.argv[2]
const pidFile = join(directory, 'pid')
const readyFile = join(directory, 'ready')
const termFile = join(directory, 'term')
const quote = value => `'${value.replaceAll("'", "'\\''")}'`
const waitFor = async (predicate, label) => {
  const deadline = Date.now() + 5000
  while (!predicate()) {
    assert.ok(Date.now() < deadline, `timed out waiting for ${label}`)
    await delay(5)
  }
}
const alive = pid => {
  try {
    process.kill(pid, 0)
    return true
  } catch (error) {
    if (error.code === 'ESRCH') return false
    throw error
  }
}

const owner = realOwnershipProbe(undefined, 'real-pty-drain')
let pid
try {
  const command = `termFile=${quote(termFile)}; trap 'printf TERM > "$termFile"' TERM; printf '%s' "$$" > ${quote(pidFile)}; printf ready > ${quote(readyFile)}; while :; do :; done`
  const opened = await owner.fork(command)
  assert.equal(opened.ok, true)
  await waitFor(() => existsSync(readyFile), 'real PTY readiness')
  pid = Number(readFileSync(pidFile, 'utf8'))
  assert.ok(Number.isInteger(pid) && pid > 1)
  assert.equal(alive(pid), true)
  const started = performance.now()
  let finished = false
  const closing = owner.closeAll(350).then(() => { finished = true })
  await waitFor(() => existsSync(termFile), 'the shell TERM handler')
  assert.equal(alive(pid), true, 'TERM handler must leave the process alive')
  assert.equal(finished, false, 'close must wait after requesting TERM')
  assert.equal(owner.owned(opened.id), true, 'TERM must retain ownership until exit')
  await closing
  const elapsed = performance.now() - started
  assert.ok(elapsed >= 300, `KILL must wait for the grace window, got ${elapsed} ms`)
  await waitFor(() => !alive(pid), 'the real PTY process exit after KILL')
  assert.equal(owner.exists(opened.id), false)
  assert.deepEqual(owner.outstanding(), [])
  assert.equal(owner.completions(), 1)
  console.log(JSON.stringify({ termObserved: true, pid, elapsed, processExited: true, blockers: owner.outstanding() }))
} finally {
  if (pid && alive(pid)) {
    try { process.kill(-pid, 'SIGKILL') } catch { process.kill(pid, 'SIGKILL') }
  }
}
