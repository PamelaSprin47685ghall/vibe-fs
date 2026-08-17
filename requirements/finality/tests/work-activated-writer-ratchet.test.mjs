// WHAT[FINALITY-021] Source ratchet: canonical production paths never write
// WorkActivated. The only remaining writer is the named bounded-compat function
// `appendLegacyMigrationWorkActivatedCompat` in Workflow.fs, called exclusively
// from `materializeInitialAgentOwnerLife` for the e2e long-stroke scenario
// (LEGACY-010). If `acceptActivation`, `applyAcceptedActivation`, or any other
// ordinary production writer is reintroduced, this test fails.

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

test('WHAT[FINALITY-021] the only WorkActivated writer in Workflow.fs is appendLegacyMigrationWorkActivatedCompat', () => {
  const src = read(WORKFLOW)
  // Every occurrence of ManagerLifecycleFact.WorkActivated must be inside the
  // compat function or its doc comment.
  const compatStart = src.indexOf('appendLegacyMigrationWorkActivatedCompat')
  assert.ok(compatStart > 0, 'compat function must exist')

  const compatBodyStart = src.indexOf('appendLifecycle', compatStart)
  const compatBodyEnd = src.indexOf('\n\n', compatBodyStart)
  const compatBody = src.slice(compatStart, compatBodyEnd)

  // The compat function body contains the WorkActivated fact.
  assert.ok(
    compatBody.includes('ManagerLifecycleFact.WorkActivated'),
    'compat function must be the WorkActivated writer',
  )

  // No other function body in Workflow.fs may contain WorkActivated.
  // Strip the compat function and its comment, then check the rest.
  const commentStart = src.lastIndexOf('/// BOUNDED-COMPAT', compatStart)
  const stripped = src.slice(0, commentStart) + src.slice(compatBodyEnd)
  assert.equal(
    stripped.includes('ManagerLifecycleFact.WorkActivated'),
    false,
    'no WorkActivated writer outside the compat function',
  )
})

test('WHAT[FINALITY-021] appendLegacyMigrationWorkActivatedCompat is called only from materializeInitialAgentOwnerLife', () => {
  const src = read(WORKFLOW)
  const calls = [...src.matchAll(/appendLegacyMigrationWorkActivatedCompat/g)]
  // One definition + one call site = 2 occurrences.
  assert.equal(calls.length, 2, 'exactly one definition and one call site')

  // The call site must be inside materializeInitialAgentOwnerLife.
  const matStart = src.indexOf('let private materializeInitialAgentOwnerLife')
  const matEnd = src.indexOf('\n    ///', matStart + 1)
  const matBody = src.slice(matStart, matEnd)
  assert.ok(
    matBody.includes('appendLegacyMigrationWorkActivatedCompat'),
    'compat call must be inside materializeInitialAgentOwnerLife',
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
