import assert from 'node:assert/strict'
import { mkdtemp, mkdir, writeFile } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'

import { censusFromRootsFile } from '../../../scripts/checks/legacy-horizon-census.mjs'

const workspace = async (lines) => {
  const root = await mkdtemp(join(tmpdir(), 'wanxiang-horizon-'))
  const events = join(root, '.git', 'wanxiang', 'events')
  await mkdir(events, { recursive: true })
  await writeFile(join(events, 'writer.ndjson'), lines.join('\n') + '\n')
  return root
}

const rootsFile = async (roots) => {
  const dir = await mkdtemp(join(tmpdir(), 'wanxiang-roots-'))
  const path = join(dir, 'roots.txt')
  await writeFile(path, roots.join('\n') + '\n')
  return path
}

test('WHAT[DURABLE-EVENTS-009] legacy_horizon_census_counts_each_historical_detector', async () => {
  const root = await workspace([
    '{"Fact":{"IsDead":true}}',
    '{"Fact":{"BlogObservationCommitted":{"ScoreVectorRef":"x"}}}',
    '{"Fact":{"PairProgrammingGuidelineAppended":{"MarkerText":"x"}}}',
    '{"Fact":{"HandleCompleted":{"CompletionRef":"r"}}}',
  ])
  const result = await censusFromRootsFile(await rootsFile([root]))
  assert.deepEqual(result.counts, {
    pre050: 1,
    scoreVector: 1,
    unanchoredGuideline: 1,
    incompleteHandleCompleted: 1,
  })
  assert.equal(result.workspaces, 1)
  assert.equal(result.journals, 1)
  assert.equal(result.lines, 4)
  assert.match(result.rootsDigest, /^[0-9a-f]{64}$/)
})

test('WHAT[DURABLE-EVENTS-009] legacy_horizon_census_modern_bytes_are_zero', async () => {
  const root = await workspace([
    '{"Fact":{"BlogObservationCommitted":{"TipRuleId":"ENFORCER-001"}}}',
    '{"Fact":{"HandleCompleted":{"CompletionRef":"r","CompletionDigest":"d"}}}',
  ])
  const result = await censusFromRootsFile(await rootsFile([root]))
  assert.deepEqual(Object.values(result.counts), [0, 0, 0, 0])
})

test('WHAT[DURABLE-EVENTS-009] legacy_horizon_census_declared_missing_workspace_fails_closed', async () => {
  const missing = join(tmpdir(), 'wanxiang-horizon-missing')
  const inventory = await rootsFile([missing])
  await assert.rejects(() => censusFromRootsFile(inventory))
})

test('WHAT[DURABLE-EVENTS-009] legacy_horizon_census_empty_inventory_fails_closed', async () => {
  const inventory = await rootsFile([])
  await assert.rejects(() => censusFromRootsFile(inventory))
})
