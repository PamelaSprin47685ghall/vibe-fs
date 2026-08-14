import { mkdtempSync, mkdirSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { randomUUID } from 'node:crypto'

const Store = await import('../../../../dist/Persistence/EventStore/Store.js')
const Integrator = await import('../../../../dist/Persistence/EventStore/CanonicalIntegrator.js')

/** Test-only production-shape EventStore: temp git common-dir + one WriterId NDJSON. */
export const createLocalEventStore = ({ commonDir, writerId } = {}) => {
  let ownedBase = null
  let gitCommonDir = commonDir

  if (!gitCommonDir) {
    ownedBase = mkdtempSync(join(tmpdir(), 'wxs-local-store-'))
    gitCommonDir = join(ownedBase, '.git')
  }

  mkdirSync(gitCommonDir, { recursive: true })
  const integrator = Integrator.CanonicalIntegrator_create()
  const store = Store.EventStore_createLocal(gitCommonDir, writerId ?? randomUUID().replaceAll('-', ''), integrator)

  return {
    commonDir: gitCommonDir,
    integrator,
    store,
    close: () => {
      if (ownedBase) rmSync(ownedBase, { recursive: true, force: true })
    },
  }
}
