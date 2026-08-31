import assert from 'node:assert/strict'
import { existsSync, mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'

import {
  FCS_NORMALIZED_SCHEMA_VERSION,
  FCS_REUSE_PATH_ENV,
  FCS_REUSE_RUN_ID_ENV,
  scanProjectSymbolUses,
} from '../../../../scripts/checks/owner-dependencies.mjs'

const repositoryScratchRoot = fileURLToPath(new URL('../../../../.fable-build/', import.meta.url))
const normalizedEvidence = join(repositoryScratchRoot, 'owner-dependencies-fcs', 'normalized-evidence.json')
const operations = fileURLToPath(new URL('../../../../src/Wanxiangshu/Git/Operations.fs', import.meta.url))

test('WHAT[STRUCTURED-WORKFLOW-011] one tagged production scan is reused fail-closed and consumer-filtered', () => {
  mkdirSync(repositoryScratchRoot, { recursive: true })
  const scratchRoot = mkdtempSync(join(repositoryScratchRoot, 'owner-dependencies-reuse-'))
  assert.ok(existsSync(normalizedEvidence), 'owner-dep must produce normalized evidence before integration reuse')
  const normalized = JSON.parse(readFileSync(normalizedEvidence, 'utf8'))
  assert.equal(normalized.schemaVersion, FCS_NORMALIZED_SCHEMA_VERSION)
  assert.match(normalized.runId, /\S/)
  assert.match(normalized.inputFingerprint, /^[0-9a-f]{64}$/)
  const runId = normalized.runId
  const previous = {
    path: process.env[FCS_REUSE_PATH_ENV],
    reuseId: process.env[FCS_REUSE_RUN_ID_ENV],
  }

  try {
    assert.ok(!('applicationCandidates' in normalized) && !('applicationRanges' in normalized))

    process.env[FCS_REUSE_PATH_ENV] = normalizedEvidence
    process.env[FCS_REUSE_RUN_ID_ENV] = runId
    const unusedScratch = join(scratchRoot, 'must-not-spawn')
    const reused = scanProjectSymbolUses({
      scratchRoot: unusedScratch,
      resultPath: join(unusedScratch, 'unused.json'),
      applicationConsumerPaths: [operations],
    })
    assert.ok(!existsSync(unusedScratch), 'reuse must not start a scanner or create its scratch directory')
    assert.ok(reused.applicationUses.length > 0)
    assert.ok(reused.symbolUses.length > 0)
    assert.ok(reused.symbolUses.every((entry) => entry.consumerPath === 'src/Wanxiangshu/Git/Operations.fs'))
    assert.ok(reused.applicationUses.every((entry) => entry.consumerPath === 'src/Wanxiangshu/Git/Operations.fs'))
    for (const collection of [
      reused.matchExpressions,
      reused.bindExpressions,
      reused.lambdaExpressions,
      reused.conditionalExpressions,
      reused.tryExpressions,
      reused.loopExpressions,
      reused.localFunctionBindings,
    ]) assert.ok(collection.every((entry) => entry.consumerPath === 'src/Wanxiangshu/Git/Operations.fs'))

    process.env[FCS_REUSE_RUN_ID_ENV] = `${runId}-wrong`
    assert.throws(() => scanProjectSymbolUses(), /run ID does not match/)
    const wrongSchema = join(scratchRoot, 'wrong-schema.json')
    writeFileSync(wrongSchema, JSON.stringify({ ...normalized, schemaVersion: FCS_NORMALIZED_SCHEMA_VERSION + 1 }))
    process.env[FCS_REUSE_PATH_ENV] = wrongSchema
    process.env[FCS_REUSE_RUN_ID_ENV] = runId
    assert.throws(() => scanProjectSymbolUses(), /schema version does not match/)
    process.env[FCS_REUSE_PATH_ENV] = normalizedEvidence
    const wrongFingerprint = join(scratchRoot, 'wrong-fingerprint.json')
    writeFileSync(wrongFingerprint, JSON.stringify({ ...normalized, inputFingerprint: '0'.repeat(64) }))
    process.env[FCS_REUSE_PATH_ENV] = wrongFingerprint
    assert.throws(() => scanProjectSymbolUses(), /inputs do not match/)
    process.env[FCS_REUSE_PATH_ENV] = normalizedEvidence
    delete process.env[FCS_REUSE_RUN_ID_ENV]
    assert.throws(() => scanProjectSymbolUses(), /requires both absolute path and run ID/)
    process.env[FCS_REUSE_RUN_ID_ENV] = runId
    process.env[FCS_REUSE_PATH_ENV] = join(scratchRoot, 'missing.json')
    assert.throws(() => scanProjectSymbolUses(), /reuse file is missing/)
  } finally {
    const restore = (name, value) => value === undefined ? delete process.env[name] : process.env[name] = value
    restore(FCS_REUSE_PATH_ENV, previous.path)
    restore(FCS_REUSE_RUN_ID_ENV, previous.reuseId)
    rmSync(scratchRoot, { recursive: true, force: true })
  }
})
