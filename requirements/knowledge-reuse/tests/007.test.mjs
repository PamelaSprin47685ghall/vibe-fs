import assert from 'node:assert/strict'
import test from 'node:test'
import { mkdtempSync, rmSync, mkdirSync, writeFileSync, readFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { create as createEventStore, dispose as disposeEventStore, createAppendFailureStore } from '../../../dist/Persistence/EventStore/Surface.js'
import * as casebook from '../../../dist/Repository/Knowledge/Casebook/Surface.js'
import * as lifecycle from '../../../dist/Repository/Knowledge/Casebook/LifecycleSurface.js'

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

test('WHAT[knowledge-reuse-007] CASE007_captured_refreshed_round_trip_through_integrator_Current', async () => {
  const local = createCasebookEventStore()
  try {
    await unwrap(casebook.archive(local.store, caseRec('s1', 'Q1', 'A1', [fileRead('a.txt', 'h1')])))
    await unwrap(casebook.refresh(local.store, 's1', 'Q1b', 'A1b', [fileRead('a.txt', 'h1'), globResult('*.fs', ['x'])]))
    const s1 = await findCase(local.store, 's1')
    assert.equal(s1.a, 'A1b')
    assert.equal(s1.observations.length, 2)
  } finally {
    local.close()
  }
})

test('WHAT[knowledge-reuse-007] access and eviction are visible through the actual Current projection', async () => {
  const local = createCasebookEventStore()
  try {
    await unwrap(casebook.archive(local.store, caseRec('s1', 'Q', 'A', [])))
    const before = (await findCase(local.store, 's1')).lastAccessOrder
    await unwrap(casebook.touchAccess(local.store, 's1'))
    assert.ok((await findCase(local.store, 's1')).lastAccessOrder >= before)
    await unwrap(casebook.evictCase(local.store, 's1'))
    assert.equal(await findCase(local.store, 's1'), null)
  } finally {
    local.close()
  }
})

test('WHAT[knowledge-reuse-007] committed captured, refreshed and evicted facts survive reopening the actual store', async () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-casebook-reopen-'))
  let store = createEventStore(dir, 'writer-before')
  try {
    await unwrap(casebook.archive(store, caseRec('retained', 'Q', 'A', [fileRead('a.txt', 'hash')])))
    await unwrap(casebook.refresh(store, 'retained', 'Q2', 'A2', [fileRead('a.txt', 'hash-2')]))
    await unwrap(casebook.archive(store, caseRec('removed', 'Q', 'A', [])))
    await unwrap(casebook.evictCase(store, 'removed'))
    disposeEventStore(store)
    store = createEventStore(dir, 'writer-after')
    assert.equal((await findCase(store, 'retained')).a, 'A2')
    assert.equal(await findCase(store, 'removed'), null)
  } finally { disposeEventStore(store); rmSync(dir, { recursive: true, force: true }) }
})

const appendFates = [
  { code: 'AppendNotAttempted', suffix: 'NOT_ATTEMPTED', kind: 'notAttempted', finalize: 'notCommitted', phase: 'BeforePhysicalAppend' },
  { code: 'CommitUnknown', suffix: 'COMMIT_UNKNOWN', kind: 'unknown', finalize: 'unknown', phase: 'DurabilityBarrier' },
  { code: 'NoNewWriteReleaseFailed', suffix: 'NO_NEW_WRITE_RELEASE_FAILED', kind: 'noNewWriteReleaseFailed', finalize: 'noNewWriteReleaseFailed', phase: 'StoreRelease' },
]

for (const fate of appendFates) {
  for (const operation of ['Capture', 'Refresh', 'Access', 'Evict']) {
    test(`WHAT[knowledge-reuse-007] typed adapter mapping ${operation} preserves ${fate.code} and the original request without retry`, async () => {
      const local = createCasebookEventStore()
      try {
        if (operation !== 'Capture') await unwrap(casebook.archive(local.store, caseRec('case-identity', 'old-Q', 'old-A', [])))
        const before = await findCase(local.store, 'case-identity')
        const cause = new Error('original append cause')
        const cleanupCause = new Error('original release cause')
        const observed = []
        const failing = createAppendFailureStore(local.store, {
          code: fate.code, phase: fate.phase, cause,
          cleanupFailures: [{ phase: 'StoreRelease', cause: cleanupCause }],
        }, (append) => observed.push(append))
        const result = await ({
          Capture: () => casebook.archive(failing, caseRec('case-identity', 'new-Q', 'new-A', [])),
          Refresh: () => casebook.refresh(failing, 'case-identity', 'new-Q', 'new-A', []),
          Access: () => casebook.touchAccess(failing, 'case-identity'),
          Evict: () => casebook.evictCase(failing, 'case-identity'),
        }[operation])()
        assert.equal(result.ok, false)
        assert.equal(result.code, `CASEBOOK_APPEND_${fate.suffix}`)
        assert.equal(observed.length, 1, 'a failed append is never retried with a fresh event identity')
        const failure = result.persistenceFailure
        assert.equal(failure.operation, operation)
        assert.equal(failure.caseIdentity, 'case-identity')
        assert.equal(failure.kind, fate.kind)
        assert.equal(failure.eventId, observed[0].requested[0].id)
        assert.deepEqual(failure.requestedEventIds, [failure.eventId])
        assert.equal(failure.isOriginalError(observed[0].originalError), true)
        assert.strictEqual(failure.primary.cause, cause)
        if (fate.code !== 'NoNewWriteReleaseFailed') {
          assert.equal(failure.primary.phase, fate.phase)
          assert.strictEqual(failure.cleanupFailures[0].cause, cleanupCause)
        }
        assert.deepEqual(await findCase(local.store, 'case-identity'), before,
          'this controlled mapping fixture does not append; physical Current behavior is proved separately')
      } finally { local.close() }
    })
  }

  test(`WHAT[knowledge-reuse-007] lifecycle ${fate.code} retains finalize identity and exposes its actual settlement fate`, async () => {
    const local = createCasebookEventStore()
    try {
      const cause = new Error('finalize append cause')
      const observed = []
      const failing = createAppendFailureStore(local.store, { code: fate.code, phase: fate.phase, cause },
        (append) => observed.push(append))
      const result = await lifecycle.finalizeEngineerCase(failing, 'finalize-identity', 'trace', 'Q', 'A', [], '')
      assert.equal(result.ok, false)
      assert.equal(result.kind, fate.finalize)
      assert.equal(result.releasesIdentity, false)
      assert.equal(observed.length, 1)
      assert.equal(result.persistenceFailure.caseIdentity, 'finalize-identity')
      assert.equal(result.persistenceFailure.eventId, observed[0].requested[0].id)
      assert.equal(result.persistenceFailure.isOriginalError(observed[0].originalError), true)
      assert.strictEqual(result.persistenceFailure.primary.cause, cause)
      assert.equal(await findCase(local.store, 'finalize-identity'), null)
    } finally { local.close() }
  })
}

test.todo('WHAT[knowledge-reuse-007] GAP-160: all production writers and recovery paths retain one EventStore authority')
