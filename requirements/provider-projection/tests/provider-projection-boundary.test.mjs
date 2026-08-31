// WHAT[PROVIDER-PROJECTION-007] — Provider projection owns only generic,
// deterministic decode, projection, render, and Host writeback mechanics.

import assert from 'node:assert/strict'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'

import {
  scanProviderProjectionRepo,
  scanProjectionCoreSource,
} from '../../../scripts/checks/provider-projection-boundary.mjs'

const ROOT = join(dirname(fileURLToPath(import.meta.url)), '../../..')
const FIXTURE_FILE = 'src/Wanxiangshu/Participant/Provider/Projection/Fixture.fs'

const violationFor = (source) => scanProjectionCoreSource(FIXTURE_FILE, source)

const expectedViolation = (rule, line, text) => ({
  id: 'provider-projection-owner',
  rule,
  file: FIXTURE_FILE,
  line,
  text,
})

test('WHAT[PROVIDER-PROJECTION-007] generic decode projection and writeback remain allowed', () => {
  const source = `namespace Wanxiangshu.Participant.Provider.Projection

open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.LlmFacing
open Wanxiangshu.OpenCode

module GenericProjection =
    type ProjectionMessageBase =
        { Key: string
          Rows: obj list }
    type ProjectionSnapshot = { CurrentProjection: obj }
    type ProjectionIntent =
        | ReplaceMessageBase of ProjectionMessageBase
        | InsertMessageRows of obj list
    let decode raw = ProviderWireDecode.decodePart raw
    let render document = LlmFacing.render document
    let projectionSnapshot currentProjection = { CurrentProjection = currentProjection }
    let replaceMessageBase cutoffDigest rows = ReplaceMessageBase { Key = cutoffDigest; Rows = rows }
    let renderMessagesWithHostIds snapshot baseMessages intents =
        snapshot.CurrentProjection, baseMessages, intents
    let renderMessagesWithIntents snapshot baseMessages intents =
        renderMessagesWithHostIds snapshot baseMessages intents
    let tryApplyRenderedMessages sessionId sha256 rendered =
        ProjectionMessageEdit.tryApplyRenderedMessages sessionId sha256 rendered
    let tryApplyRenderedInsertionsPreservingBase sessionId sha256 raw insertions =
        ProjectionMessageEdit.tryApplyRenderedInsertionsPreservingBase sessionId sha256 raw insertions
`

  assert.deepEqual(violationFor(source), [])
})

test('WHAT[PROVIDER-PROJECTION-007] Strength namespace imports are rejected precisely', () => {
  assert.deepEqual(
    violationFor('namespace Fixture\nopen Wanxiangshu.Strength.Prediction\n'),
    [expectedViolation('strength-import', 2, 'open Wanxiangshu.Strength.Prediction')],
  )
})

for (const [label, source, reference] of [
  ['Context', 'open Wanxiangshu.Context.Prefix', 'Wanxiangshu.Context.Prefix'],
  ['Enforcer', 'open Wanxiangshu.Enforcer', 'Wanxiangshu.Enforcer'],
  ['Interaction', 'open Wanxiangshu.Interaction.Dispatch', 'Wanxiangshu.Interaction.Dispatch'],
  ['Strength reference', 'let frame = Wanxiangshu.Strength.Frame.empty', 'Wanxiangshu.Strength.Frame.empty'],
  ['Session Recovery', 'open Wanxiangshu.Execution.Session.Recovery', 'Wanxiangshu.Execution.Session.Recovery'],
]) {
  test(`WHAT[PROVIDER-PROJECTION-007] ${label} owner references are rejected precisely`, () => {
    assert.deepEqual(
      violationFor(`module Fixture\n${source}\n`),
      [expectedViolation('foreign-owner-reference', 2, reference)],
    )
  })
}

for (const name of [
  'ActivatePrefixEpoch',
  'InsertBlogFrames',
  'BlogFramesIntent',
  'InsertRepair',
  'SuppressTransportOnly',
  'ReanchorAfterCompaction',
  'CompanionProjectionBuilder',
  'ProjectionConstants.RepairInstruction',
]) {
  test(`WHAT[PROVIDER-PROJECTION-007] foreign materialization ${name} is rejected precisely`, () => {
    assert.deepEqual(
      violationFor(`module Fixture\nlet value = ${name}\n`),
      [expectedViolation('foreign-materialization', 2, name)],
    )
  })
}

test('WHAT[PROVIDER-PROJECTION-007] Strength writeback API names are rejected precisely', () => {
  assert.deepEqual(
    violationFor('module Fixture\nlet tryApplyStrengthRenderedMessages value = value\n'),
    [expectedViolation('strength-api', 2, 'tryApplyStrengthRenderedMessages')],
  )
})

for (const [label, phrase] of [
  ['Host adapter policy', 'Strength Host adapter'],
  ['tool policy', 'Strength tool'],
  ['replica view policy', 'Replica provider view'],
]) {
  test(`WHAT[PROVIDER-PROJECTION-007] ${label} vocabulary is rejected precisely`, () => {
    assert.deepEqual(
      violationFor(`module Fixture\nlet error = "${phrase}"\n`),
      [expectedViolation('strength-policy-vocabulary', 2, phrase)],
    )
  })
}

for (const [label, identifier] of [
  ['retry', 'retryDecision'],
  ['recovery', 'RecoveryDisposition'],
  ['lifecycle', 'advanceLifecycle'],
]) {
  test(`WHAT[PROVIDER-PROJECTION-007] ${label} decision identifiers are rejected precisely`, () => {
    assert.deepEqual(
      violationFor(`module Fixture\nlet ${identifier} value = value\n`),
      [expectedViolation('policy-identifier', 2, identifier)],
    )
  })
}

test('WHAT[PROVIDER-PROJECTION-007] projection conflict lifecycle vocabulary remains legal', () => {
  assert.deepEqual(
    violationFor('module Fixture\ntype ProjectionConflict = ConflictingPrefixLifecycle\n'),
    [],
  )
})

test('WHAT[PROVIDER-PROJECTION-007] lifecycle control imports are rejected precisely', () => {
  assert.deepEqual(
    violationFor('namespace Fixture\nopen Wanxiangshu.Execution.Session.Lifecycle\n'),
    [expectedViolation('policy-identifier', 2, 'Lifecycle')],
  )
})

test('WHAT[PROVIDER-PROJECTION-007] production provider projection owners are policy-free', () => {
  assert.deepEqual(
    scanProviderProjectionRepo(ROOT),
    [],
    'provider-projection-owner violations must move to their policy owner; this proof remains RED until the production cutover',
  )
})
