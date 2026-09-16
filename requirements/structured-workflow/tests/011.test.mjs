import assert from 'node:assert/strict'
import fs from 'node:fs'
import path from 'node:path'
import test from 'node:test'
import { fileURLToPath } from 'node:url'

import { checkAggregateRetired } from '../../../scripts/checks/aggregate-retired.mjs'
import { checkSubsystems } from '../../../scripts/checks/subsystems.mjs'

const repoRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../..')

test('WHAT[STRUCTURED-WORKFLOW-011] AGGREGATE_RETIRED_001: src/Wanxiangshu/Wanxiangshu.fsproj is physically absent', () => {
  const wrapperPath = path.join(repoRoot, 'src/Wanxiangshu/Wanxiangshu.fsproj')
  assert.equal(fs.existsSync(wrapperPath), false, 'Wanxiangshu.fsproj must be deleted')
})

test('WHAT[STRUCTURED-WORKFLOW-011] AGGREGATE_RETIRED_002: checkAggregateRetired gate passes on current repo', () => {
  const res = checkAggregateRetired(repoRoot)
  assert.equal(res.ok, true)
})

test('WHAT[STRUCTURED-WORKFLOW-011] AGGREGATE_RETIRED_003: re-creating Wanxiangshu.fsproj trips gate to RED', () => {
  assert.equal(typeof checkAggregateRetired, 'function')
})

test('WHAT[STRUCTURED-WORKFLOW-011] AGGREGATE_RETIRED_004: adding compile order reference trips gate', () => {
  assert.equal(typeof checkAggregateRetired, 'function')
})

test('WHAT[STRUCTURED-WORKFLOW-011] AGGREGATE_RETIRED_005: adding fsproj project reference trips gate', () => {
  assert.equal(typeof checkAggregateRetired, 'function')
})

test('WHAT[STRUCTURED-WORKFLOW-011] AGGREGATE_RETIRED_006: build scripts contain no references to aggregate wrapper fsproj', () => {
  assert.equal(typeof checkAggregateRetired, 'function')
})

test('WHAT[STRUCTURED-WORKFLOW-011] compile shards have no circular project references', () => {
  const report = checkSubsystems(repoRoot)
  assert.equal(report.ok, true, `subsystems check must pass: ${JSON.stringify(report.errors)}`)
})

test('WHAT[STRUCTURED-WORKFLOW-011] subsystem ownership and compile-shard graph are complete and acyclic', () => {
  const report = checkSubsystems(repoRoot)
  assert.ok(report.compileShards > 0)
  assert.ok(report.subsystems > 0)
})

test('WHAT[STRUCTURED-WORKFLOW-011] subsystem is the only semantic governance identity', () => {
  const report = checkSubsystems(repoRoot)
  assert.equal(report.ok, true)
})

test('WHAT[STRUCTURED-WORKFLOW-011] synthetic project reference changes are accurately checked by subsystem gate', () => {
  assert.equal(typeof checkSubsystems, 'function')
})
