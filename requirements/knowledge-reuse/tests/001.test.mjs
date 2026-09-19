import assert from 'node:assert/strict'
import test from 'node:test'
import { mkdtempSync, rmSync, mkdirSync, writeFileSync, readFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { create as createEventStore, dispose as disposeEventStore } from '../../../dist/Persistence/EventStore/Surface.js'
import * as casebook from '../../../dist/Repository/Knowledge/Casebook/Surface.js'

const fileRead = (path, contentHash) => ({ kind: 'file-read', path, contentHash })

const globResult = (pattern, paths) => ({ kind: 'glob-result', pattern, paths })

const createCasebookEventStore = () => {
  const commonDir = mkdtempSync(join(tmpdir(), 'wxs-casebook-store-'))
  const store = createEventStore(commonDir, 'casebook-test-writer')
  return {
    store,
    close: () => {
      disposeEventStore(store)
      rmSync(commonDir, { recursive: true, force: true })
    },
  }
}

const unwrap = async (operation) => {
  const result = await operation
  assert.equal(result.ok, true, `expected successful Casebook operation, got ${JSON.stringify(result.error)}`)
  return result.value
}

const caseRec = (sessionId, q, a, observations) => ({
  sessionId,
  q,
  a,
  observations,
  lastAccessOrder: 0,
})

const findCase = async (store, sessionId) => {
  const result = await casebook.fetchCase(store, 10, sessionId)
  assert.equal(result.ok, true, JSON.stringify(result.error))
  return result.value
}

test('WHAT[knowledge-reuse-001] CASE001_casebook_is_best_effort_semantic_cache_with_readonly_observation_replay', async () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-cb001-'))
  const local = createCasebookEventStore()
  try {
    const filePath = join(dir, 'sample.txt')
    writeFileSync(filePath, 'constant content', 'utf8')
    const hash = casebook.contentHash('constant content')

    // 1. Case saves QA paired with replayable typed observations
    const sampleCase = caseRec('session-qa-1', 'What is in sample.txt?', 'constant content', [fileRead('sample.txt', hash)])
    await unwrap(casebook.archive(local.store, sampleCase))

    // 2. Fetch retrieves stored case from Current projection
    const fetched = await findCase(local.store, 'session-qa-1')
    assert.equal(fetched.q, 'What is in sample.txt?')
    assert.equal(fetched.a, 'constant content')
    assert.equal(fetched.observations.length, 1)

    // 3. Evaluation/replay is strictly read-only: does not modify target workspace
    const beforeContent = readFileSync(filePath, 'utf8')
    const needsRefresh = (await casebook.needsRefresh(local.store, 10, 'session-qa-1', dir)).value
    assert.equal(needsRefresh, false, 'matching workspace observations should not need refresh')
    const afterContent = readFileSync(filePath, 'utf8')
    assert.equal(beforeContent, afterContent, 'replay against workspace must be strictly read-only')

    // 4. Best-effort cache semantics: non-existent case safely returns null without truth failure
    const nonExistent = await findCase(local.store, 'unknown-session')
    assert.equal(nonExistent, null, 'unknown case returns null, does not claim codebase truth')
  } finally {
    local.close()
    rmSync(dir, { recursive: true, force: true })
  }
})
