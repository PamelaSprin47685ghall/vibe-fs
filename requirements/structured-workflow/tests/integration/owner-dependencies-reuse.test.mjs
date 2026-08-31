import assert from 'node:assert/strict'
import { randomUUID } from 'node:crypto'
import { existsSync, mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'

import {
  FCS_NORMALIZED_OUTPUT_ENV,
  FCS_NORMALIZED_SCHEMA_VERSION,
  FCS_REUSE_PATH_ENV,
  FCS_REUSE_RUN_ID_ENV,
  scanProjectSymbolUses,
} from '../../../../scripts/checks/owner-dependencies.mjs'

const repositoryScratchRoot = fileURLToPath(new URL('../../../../.fable-build/', import.meta.url))
const operations = fileURLToPath(new URL('../../../../src/Wanxiangshu/Git/Operations.fs', import.meta.url))

test('WHAT[STRUCTURED-WORKFLOW-011] one tagged production scan is reused fail-closed and consumer-filtered', () => {
  mkdirSync(repositoryScratchRoot, { recursive: true })
  const scratchRoot = mkdtempSync(join(repositoryScratchRoot, 'owner-dependencies-reuse-'))
  const rawEvidence = join(scratchRoot, 'raw-symbol-uses.json')
  const normalizedEvidence = join(scratchRoot, 'normalized-evidence.json')
  const runId = randomUUID()
  const previous = {
    producer: process.env.OMP_FCS_EVIDENCE_RUN_ID,
    output: process.env[FCS_NORMALIZED_OUTPUT_ENV],
    path: process.env[FCS_REUSE_PATH_ENV],
    reuseId: process.env[FCS_REUSE_RUN_ID_ENV],
  }

  try {
    delete process.env[FCS_REUSE_PATH_ENV]
    delete process.env[FCS_REUSE_RUN_ID_ENV]
    process.env.OMP_FCS_EVIDENCE_RUN_ID = runId
    process.env[FCS_NORMALIZED_OUTPUT_ENV] = normalizedEvidence
    const produced = scanProjectSymbolUses({
      scratchRoot: join(scratchRoot, 'producer'),
      resultPath: rawEvidence,
    })
    assert.ok(produced.productionFiles.length > 0)
    assert.equal(JSON.parse(readFileSync(rawEvidence, 'utf8')).runId, runId)
    const normalized = JSON.parse(readFileSync(normalizedEvidence, 'utf8'))
    assert.equal(normalized.schemaVersion, FCS_NORMALIZED_SCHEMA_VERSION)
    assert.equal(normalized.runId, runId)
    assert.ok(!('applicationCandidates' in normalized) && !('applicationRanges' in normalized))

    delete process.env.OMP_FCS_EVIDENCE_RUN_ID
    delete process.env[FCS_NORMALIZED_OUTPUT_ENV]
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
    delete process.env[FCS_REUSE_RUN_ID_ENV]
    assert.throws(() => scanProjectSymbolUses(), /requires both absolute path and run ID/)
    process.env[FCS_REUSE_RUN_ID_ENV] = runId
    process.env[FCS_REUSE_PATH_ENV] = join(scratchRoot, 'missing.json')
    assert.throws(() => scanProjectSymbolUses(), /reuse file is missing/)
  } finally {
    const restore = (name, value) => value === undefined ? delete process.env[name] : process.env[name] = value
    restore('OMP_FCS_EVIDENCE_RUN_ID', previous.producer)
    restore(FCS_NORMALIZED_OUTPUT_ENV, previous.output)
    restore(FCS_REUSE_PATH_ENV, previous.path)
    restore(FCS_REUSE_RUN_ID_ENV, previous.reuseId)
    rmSync(scratchRoot, { recursive: true, force: true })
  }
})
