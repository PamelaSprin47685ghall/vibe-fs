import { mkdirSync, mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import * as eventStore from '../../../../dist/Persistence/EventStore/Surface.js'

export const event = (id, parents = [], payload = {}) => ({
  id,
  stream: 'convergence/proof',
  type: 'JobRequested',
  parents,
  payload,
  payloadRefs: [],
})

export async function withStore(run) {
  const root = mkdtempSync(join(tmpdir(), 'wxs-convergence-'))
  const commonDir = join(root, '.git')
  mkdirSync(commonDir)
  const store = eventStore.create(commonDir, 'writer-proof')
  try {
    return await run(store)
  } finally {
    eventStore.dispose(store)
    rmSync(root, { recursive: true, force: true })
  }
}
