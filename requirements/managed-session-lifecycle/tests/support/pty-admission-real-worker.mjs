import * as journal from '../../../../dist/Persistence/Journal/Surface.js'
import { ToolRuntimeScopeSurface } from '../../../../dist/OpenCode/Tools/ToolRuntimeScopeSurface.js'
import { loseOwnershipCommit, crashOwnershipCommit } from './ownership-commit.mjs'

const sqlite = await import(typeof globalThis.Bun === 'undefined' ? 'node:sqlite' : 'bun:sqlite')
const Database = sqlite.DatabaseSync ?? sqlite.Database

const [workspace, child] = process.argv.slice(2)
const opened = await journal.JournalSurface_acquireSharedForWorkspace(workspace, process.pid, '2026-10-16T00:00:00Z')
if (!opened.ok) throw new Error(JSON.stringify(opened.error))
const probe = ToolRuntimeScopeSurface.ptyAdmissionProbe(opened.journal, workspace, child, true, await journal.JournalSurface_initializeStateRoot(workspace))
process.send({ ready: true, pid: process.pid })
process.on('message', async ({ id, action, command, lose, crash }) => {
  try {
    const perform = async () => action === 'open' ? await probe.open(command)
      : action === 'stop' ? await probe.stop()
      : action === 'close' ? await probe.close(command)
      : action === 'blockers' ? probe.blockers()
      : (() => { throw new Error(`Unknown action: ${action}`) })()
    const value = crash
      ? await crashOwnershipCommit(Database, crash.command, crash.afterCommit, perform)
      : lose
      ? await loseOwnershipCommit(Database, lose.command, lose.afterCommit, perform)
      : await perform()
    process.send({ id, value: value ?? null })
  } catch (error) {
    process.send({ id, error: error?.stack ?? error?.message ?? String(error) })
  }
})
process.on('disconnect', () => {
  journal.JournalSurface_dispose(opened.journal)
  process.exit(0)
})
