import * as journal from '../../../../dist/Persistence/Journal/Surface.js'
import { ToolRuntimeScopeSurface } from '../../../../dist/OpenCode/Tools/ToolRuntimeScopeSurface.js'
import * as routing from '../../../../dist/OpenCode/Host/ModelRoutingSurface.js'
import os from 'node:os'
import { mock } from 'node:test'
import { syncBuiltinESMExports } from 'node:module'
import { join } from 'node:path'
import { existsSync } from 'node:fs'
import { DatabaseSync } from 'node:sqlite'
import { loseOwnershipCommit } from './ownership-commit.mjs'

const [workspace, child, physical] = process.argv.slice(2)
mock.method(os, 'homedir', () => join(workspace, `routing-${process.pid}`))
syncBuiltinESMExports()
await routing.initialize()
const opened = await journal.JournalSurface_acquireSharedForWorkspace(workspace, process.pid, '2026-10-16T00:00:00Z')
if (!opened.ok) throw new Error(JSON.stringify(opened.error))
await ToolRuntimeScopeSurface.startPtyExecution(opened.journal, child, physical, 'pty-admission-provider')
const probe = ToolRuntimeScopeSurface.ptyAdmissionProbe(opened.journal, workspace, child, false)
const prepare = DatabaseSync.prototype.prepare
const execute = DatabaseSync.prototype.exec
const ownershipConnections = new WeakSet()
const revisions = new WeakMap()
let barrier
DatabaseSync.prototype.prepare = function (sql) {
  const statement = prepare.call(this, sql)
  if (!sql.startsWith('SELECT revision, body, checksum FROM journal_entries')) return statement
  const database = this
  const all = statement.all
  statement.all = function (stream) {
    const rows = all.call(this, stream)
    if (stream === `ownership:fork-pty:${child}`) {
      ownershipConnections.add(database)
      revisions.set(database, rows.length)
    }
    return rows
  }
  return statement
}
DatabaseSync.prototype.exec = function (sql) {
  if (barrier && ownershipConnections.has(this) && /^BEGIN IMMEDIATE\b/.test(sql.trim())) {
    const current = barrier
    barrier = undefined
    process.send({ barrier: current.name, revision: revisions.get(this) })
    const waitArray = new Int32Array(new SharedArrayBuffer(4))
    const deadline = Date.now() + 10000
    while (!existsSync(current.release)) {
      if (Date.now() > deadline) throw new Error(`Barrier timed out: ${current.name}`)
      Atomics.wait(waitArray, 0, 0, 10)
    }
  }
  return execute.call(this, sql)
}
process.send({ ready: true, pid: process.pid })
process.on('message', async ({ id, action, command, pause, lose }) => {
  try {
    if (pause) barrier = pause
    const perform = async () => action === 'open' ? await probe.open(command)
      : action === 'openLater' ? await probe.openLater(command)
      : action === 'openUnknown' ? await probe.openUnknown(command)
      : action === 'openCaptured' ? await probe.openCaptured(command)
      : action === 'stop' ? await probe.stop()
      : action === 'complete' ? probe.complete(command)
      : action === 'blockers' ? probe.blockers()
      : action === 'spawns' ? probe.spawns()
      : action === 'detachChild' ? await probe.detachChild()
      : action === 'dispose' ? await probe.dispose()
      : (() => { throw new Error(`Unknown action: ${action}`) })()
    const value = lose
      ? await loseOwnershipCommit(DatabaseSync, lose.command, lose.afterCommit, perform)
      : await perform()
    process.send({ id, value: value ?? null })
  } catch (error) {
    process.send({ id, error: error.stack })
  }
})
process.on('disconnect', () => {
  journal.JournalSurface_dispose(opened.journal)
  process.exit(0)
})
