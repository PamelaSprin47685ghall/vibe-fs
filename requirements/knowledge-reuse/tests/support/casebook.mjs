import assert from 'node:assert/strict'
import { mkdtempSync, mkdirSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import * as eventStore from '../../../../dist/Persistence/EventStore/Surface.js'
import * as casebook from '../../../../dist/Repository/Knowledge/Casebook/Surface.js'
import * as fetchSurface from '../../../../dist/Repository/Knowledge/Casebook/FetchSurface.js'
import * as index from '../../../../dist/Repository/Knowledge/Casebook/IndexSurface.js'
import { parse } from 'smol-toml'
export { casebook, eventStore, index, parse }
export const sandbox = ({ enabled = true } = {}) => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-casebook-review-'))
  if (enabled) mkdirSync(join(dir, '.wanxiang', 'casebook'), { recursive: true })
  const store = eventStore.create(dir, 'review-writer')
  const tool = fetchSurface.contract({ tool: { schema: { string: () => ({}) } } }, dir, store)
  return {
    dir, store,
    fetch: shelfmark => tool.execute({ shelfmark }, { sessionID: 'reader', agent: 'engineer' }),
    close: () => { eventStore.dispose(store); rmSync(dir, { recursive: true, force: true }) },
  }
}
export const createCase = async (local, identity = 'case-1', text = 'version-B') => {
  writeFileSync(join(local.dir, 'subject.txt'), text)
  const baseline = await casebook.freezeCompletionState(local.store, local.dir, ['subject.txt'])
  assert.equal(typeof baseline, 'string')
  const result = await casebook.finalizeEngineerCase(local.store, identity, 'trace-1', 'Question?', 'Answer B', ['subject.txt'], baseline)
  assert.equal(result.kind, 'finalized')
  return { baseline, identity, shelfmark: index.shelfmarkFor(identity, 'Question?') }
}
export const deferred = () => {
  let resolve
  const promise = new Promise(done => { resolve = done })
  return { promise, resolve }
}
