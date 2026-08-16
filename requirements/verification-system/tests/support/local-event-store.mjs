import { mkdtempSync, mkdirSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { randomUUID } from 'node:crypto'

const EventStore = await import('../../../../dist/Persistence/EventStore/Surface.js')

/** Test-only production-shape EventStore: temp git common-dir + one WriterId NDJSON.
 * EventStoreSurface owns append locking, payload closure, Current integration, and replay.
 */
export const createLocalEventStore = ({ commonDir, writerId } = {}) => {
  let ownedBase = null
  let gitCommonDir = commonDir

  if (!gitCommonDir) {
    ownedBase = mkdtempSync(join(tmpdir(), 'wxs-local-store-'))
    gitCommonDir = join(ownedBase, '.git')
  }

  mkdirSync(gitCommonDir, { recursive: true })
  const store = EventStore.EventStoreSurface_create(gitCommonDir, writerId ?? randomUUID().replaceAll('-', ''))

  return {
    commonDir: gitCommonDir,
    store,
    close: () => {
      EventStore.EventStoreSurface_dispose(store)
      if (ownedBase) rmSync(ownedBase, { recursive: true, force: true })
    },
  }
}
