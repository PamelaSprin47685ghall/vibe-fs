import assert from 'node:assert/strict'
import { mkdirSync, mkdtempSync, rmSync } from 'node:fs'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'

import {
  FCS_REUSE_PATH_ENV,
  FCS_REUSE_RUN_ID_ENV,
  scanProjectSymbolUses,
} from '../../../../scripts/checks/owner-dependencies.mjs'

const fixtureRoot = fileURLToPath(new URL('../fixtures/owner-dependencies/', import.meta.url))
const repositoryScratchRoot = fileURLToPath(new URL('../../../../.fable-build/', import.meta.url))

test('WHAT[STRUCTURED-WORKFLOW-011] explicit fixture scan cannot consume production evidence', () => {
  mkdirSync(repositoryScratchRoot, { recursive: true })
  const scratchRoot = mkdtempSync(join(repositoryScratchRoot, 'owner-dependencies-isolation-'))
  const previousPath = process.env[FCS_REUSE_PATH_ENV]
  const previousRunId = process.env[FCS_REUSE_RUN_ID_ENV]

  try {
    process.env[FCS_REUSE_PATH_ENV] = join(scratchRoot, 'must-not-be-read.json')
    process.env[FCS_REUSE_RUN_ID_ENV] = 'foreign-production-run'
    const fixtureScan = scanProjectSymbolUses({
      projectFile: join(fixtureRoot, 'Fixture.fsproj'),
      productionRoot: fixtureRoot,
      scratchRoot: join(scratchRoot, 'scan'),
      resultPath: join(scratchRoot, 'scan', 'result.json'),
    })
    assert.ok(fixtureScan.productionFiles.some((path) => path.endsWith('/Consumer.fs')))
  } finally {
    const restore = (name, value) => value === undefined ? delete process.env[name] : process.env[name] = value
    restore(FCS_REUSE_PATH_ENV, previousPath)
    restore(FCS_REUSE_RUN_ID_ENV, previousRunId)
    rmSync(scratchRoot, { recursive: true, force: true })
  }
})
