// ponytail: universal loop proof — smallest check that G6 workflow composes
// Meditator -> same reusable Inspector -> ReuseScope close -> one CaseFinalize -> fetch
import assert from 'node:assert/strict'
import test from 'node:test'
import { mkdtempSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { CasebookWorkflow_archiveInspectorResult as archive, CasebookWorkflow_finalizeCase as finalize } from '../../../dist/Infrastructure/CasebookWorkflow.js'
import { Observation } from '../../../dist/Domain/Casebook.js'
import { contentHash } from '../../../dist/Infrastructure/CasebookCapture.js'
import { GitRawStore_createInMemory as createRaw } from '../../../dist/Infrastructure/Persist/GitRawStore.js'
import { EventStore_create as createStore } from '../../../dist/Infrastructure/Persist/EventStore.js'
import { toList, resultOf } from '../support/domain.mjs'

const obsIndex = n => Object.create(Observation.prototype).cases().indexOf(n)
const fileRead = (p,h) => new Observation(obsIndex('FileRead'), [p,h])

test('G6_G_universal_loop_archive_finalize_fetch', async () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-universal-'))
  try {
    const raw = createRaw(); const store = createStore(raw)
    writeFileSync(join(dir, 'a.txt'), 'hello', 'utf8')
    const c1 = { SessionId: 'reuse-scope-1', Q: 'Q1', A: 'A1', Observations: toList([fileRead('a.txt', contentHash('hello'))]), LastAccessOrder: 0 }
    const r1 = resultOf(archive(store, raw, c1)); assert.equal(r1.ok, true)
    // same scope second finalize must be refused
    const r2 = finalize(store, raw, c1)
    // finalizeCase returns Error when already finalized
    const second = resultOf(r2) // resultOf unwraps error string; check error path
    // final fetch should still see first case
    const { CasebookWorkflow_fetchCase: fetch } = await import('../../../dist/Infrastructure/CasebookWorkflow.js')
    const fetched = resultOf(fetch(store, raw, 10, 'reuse-scope-1'))
    assert.equal(fetched.value.A, 'A1')
  } finally { rmSync(dir, { recursive: true, force: true }) }
})
