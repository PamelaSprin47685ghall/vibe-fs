// WHAT[FINALITY-021] Source ratchet: canonical production paths never write
// WorkActivated. The LEGACY-010 compat writer
// `appendLegacyMigrationWorkActivatedCompat` has been deleted (long-stroke oracle
// no longer requires WorkActivated). WorkActivated remains only as an inert fact
// case in Facts.fs + decode in Projection.fs (permanent decode-only legacy).
// If any production writer of WorkActivated is reintroduced, this test fails.

import assert from 'node:assert/strict'
import test from 'node:test'
import { readFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'

const ROOT = join(dirname(fileURLToPath(import.meta.url)), '../../..')

const read = (rel) => readFileSync(join(ROOT, rel), 'utf8')

const WORKFLOW = 'src/Wanxiangshu/Mission/Manager/Life/Workflow.fs'
const NARRATIVE = 'src/Wanxiangshu/Mission/Manager/OpenCode/NarrativeTransform.fs'
const PLUGIN = 'src/Wanxiangshu/OpenCode/Plugin/PluginTransforms.fs'

test('WHAT[FINALITY-021] acceptActivation is absent from Workflow.fs', () => {
  const src = read(WORKFLOW)
  assert.equal(src.includes('let acceptActivation'), false, 'acceptActivation must not be defined')
  assert.equal(src.includes('acceptActivation'), false, 'no acceptActivation reference may remain')
})

test('WHAT[FINALITY-021] ensureMigrated writes only LifeOpened (no WorkActivated)', () => {
  const src = read(WORKFLOW)
  const start = src.indexOf('let ensureMigrated')
  const end = src.indexOf('\n    ///', start + 1)
  const body = src.slice(start, end)
  assert.equal(body.includes('WorkActivated'), false, 'ensureMigrated must not append WorkActivated')
})

test('WHAT[FINALITY-021] appendLegacyMigrationWorkActivatedCompat is absent from Workflow.fs', () => {
  const src = read(WORKFLOW)
  assert.equal(
    src.includes('appendLegacyMigrationWorkActivatedCompat'),
    false,
    'LEGACY-010 compat writer must be deleted from Workflow.fs',
  )
})

test('WHAT[FINALITY-021] no WorkActivated writer exists anywhere in Workflow.fs', () => {
  const src = read(WORKFLOW)
  assert.equal(
    src.includes('ManagerLifecycleFact.WorkActivated'),
    false,
    'no production path in Workflow.fs may write WorkActivated',
  )
})

test('WHAT[FINALITY-021] applyAcceptedActivation and acceptActivation are absent from NarrativeTransform.fs', () => {
  const src = read(NARRATIVE)
  assert.equal(src.includes('applyAcceptedActivation'), false, 'applyAcceptedActivation must be deleted')
  assert.equal(src.includes('acceptActivation'), false, 'acceptActivation must be absent')
  assert.equal(src.includes('isWorkActivationMessage'), false, 'wire activation detection must be deleted')
  assert.equal(src.includes('workActivationAnchors'), false, 'workActivationAnchors must be deleted')
  assert.equal(src.includes('lifeNeedingActivation'), false, 'lifeNeedingActivation must be deleted')
  assert.equal(src.includes('commitActivation'), false, 'commitActivation must be deleted')
})

test('WHAT[FINALITY-021] PluginTransforms.fs does not call applyAcceptedActivation', () => {
  const src = read(PLUGIN)
  assert.equal(src.includes('applyAcceptedActivation'), false, 'PluginTransforms must not call applyAcceptedActivation')
})

test('WHAT[FINALITY-021] ensureMigrated signature has no protectedPrefixEndSequence parameter', () => {
  const src = read(WORKFLOW)
  const start = src.indexOf('let ensureMigrated')
  const end = src.indexOf(': Task<Result<unit, string>>', start)
  const signature = src.slice(start, end)
  assert.equal(
    signature.includes('protectedPrefixEndSequence'),
    false,
    'ensureMigrated must not take protectedPrefixEndSequence',
  )
})
