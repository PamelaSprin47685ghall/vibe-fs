import assert from 'node:assert/strict'
import { Database } from 'bun:sqlite'
import { existsSync, readFileSync } from 'node:fs'
import { join } from 'node:path'
import { setTimeout as delay } from 'node:timers/promises'
import { realOwnershipProbe, realSharedOwnershipProbe } from '../../../../dist/Execution/Delegation/Fork/Host/HostForkPtySurface.js'

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
const start = async (owner, label) => {
  const marker = join(directory, `${label}-pid`)
  const program = `require('node:fs').writeFileSync(${JSON.stringify(marker)}, String(process.pid)); setInterval(() => {}, 1000)`
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
    await second.detach()
    await waitFor(() => !alive(other.pid), 'the second owner process exit')
    assert.deepEqual(second.outstanding(), [])
    console.log(JSON.stringify({ mode, firstExited: true, otherSurvived: true, blockers: [] }))
  } else {
    first = realOwnershipProbe(directory, 'ended-real-owner')
    const opened = await start(first, 'first')
    const prepare = Database.prototype.prepare
    const execute = Database.prototype.exec
    let ending = false
    let injected = false
    Database.prototype.prepare = function (sql) {
      const statement = prepare.call(this, sql)
      if (!sql.startsWith('INSERT INTO journal_entries')) return statement
      const run = statement.run
      statement.run = function (...parameters) {
        ending = JSON.parse(new TextDecoder().decode(parameters[3])).message?.type === 'Ended'
        return run.apply(this, parameters)
      }
      return statement
    }
    Database.prototype.exec = function (sql) {
      if (!injected && ending && /^COMMIT\b/i.test(sql.trim())) {
        injected = true
        if (mode === 'ended-after') execute.call(this, sql)
        throw new Error('Ended commit acknowledgement was lost')
      }
      return execute.call(this, sql)
    }
    try {
      await first.detach()
    } finally {
      Database.prototype.prepare = prepare
      Database.prototype.exec = execute
    }
    assert.equal(injected, true, 'the real Ended transaction must reach COMMIT')
    await waitFor(() => !alive(opened.pid), 'the real PTY process exit')
    assert.equal(first.exists(opened.id), false)
    assert.equal(first.owned(opened.id), false)
    assert.deepEqual(first.outstanding(), [])
    second = realOwnershipProbe(directory, 'ended-real-owner')
    assert.deepEqual(second.outstanding(), [])
    await second.detach()
    console.log(JSON.stringify({ mode, injected, processExited: true, blockers: [] }))
  }
} finally {
  for (const pid of pids) {
    if (alive(pid)) {
      try { process.kill(-pid, 'SIGKILL') } catch (error) {
        if (error.code !== 'ESRCH') throw error
      }
    }
  }
}
