import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import * as sync from '../../../../dist/Execution/Delegation/SyncDelegate/Surface.js'

export async function withSyncRuntime(owner, run) {
  const directory = mkdtempSync(join(tmpdir(), 'wxs-delegation-sync-'))
  const runtime = await sync.create(directory, [{ sessionId: owner, agent: 'manager' }])
  try {
    return await run(runtime)
  } finally {
    sync.dispose(runtime)
    rmSync(directory, { recursive: true, force: true })
  }
}
