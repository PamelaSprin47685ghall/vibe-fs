// FROZEN — 2026-08-14. Written before implementation by explicit user request.
// Intentionally NOT executed before implementation.
//
// DURABLE-EVENTS-013/014/019: one canonical F# CE Integrator owns history iteration.
// Business modules register single-event integration rules and read Current; they never replay history themselves.

import assert from 'node:assert/strict'
import { readFile } from 'node:fs/promises'
import test from 'node:test'

const read = (relative) => readFile(new URL(`../../../${relative}`, import.meta.url), 'utf8')

test('DURABLE_EVENTS_019_canonical_integrator_is_an_FSharp_CE_with_registered_business_rules', async () => {
  const source = await read('src/Wanxiangshu/Persistence/EventStore/CanonicalIntegrator.fs')

  assert.match(source, /type\s+IntegratorBuilder|type\s+CanonicalIntegratorBuilder/)
  assert.match(source, /member\s+_\.(Bind|Combine|Yield|Return)/)
  assert.match(source, /integrator\s*\{/)

  for (const registration of [
    'StructuralIntegration.rule',
    'JournalIntegration.rule',
    'StrengthIntegration.rule',
    'CasebookIntegration.rule',
    'JsTransactionIntegration.rule',
  ]) {
    assert.equal(source.includes(registration), true, `canonical program must register ${registration}`)
  }

  assert.match(source, /integrateRules/, 'one event may feed structural + business registered oracles')
  assert.doesNotMatch(source, /multiple integration rules accepted one event/)
  assert.doesNotMatch(source, /type\s+\w*(StateMachine|LifecycleState)\b/)
})

test('DURABLE_EVENTS_013_019_business_modules_do_not_own_history_read_or_replay_loops', async () => {
  const forbiddenOwners = [
    'src/Wanxiangshu/Strength/Persistence/Store.fs',
    'src/Wanxiangshu/Repository/Knowledge/Casebook/Store.fs',
    'src/Wanxiangshu/Repository/Knowledge/Casebook/Index.fs',
    'src/Wanxiangshu/Repository/Programming/Js/TransactionStore.fs',
    'src/Wanxiangshu/Persistence/Journal/EventStoreJournalWriter.fs',
  ]

  const forbiddenHistoryTokens = [
    'EventStoreMergeSpec.merge',
    'GitRawStore.loadEventEnvelopes',
    'loadEventEnvelopes raw',
    'loadEvents raw',
    'EventStoreFold.fold envelopes',
  ]

  for (const file of forbiddenOwners) {
    const source = await read(file)
    for (const token of forbiddenHistoryTokens) {
      assert.equal(source.includes(token), false, `${file} must not manually integrate history via ${token}`)
    }
  }
})

test('DURABLE_EVENTS_013_boot_and_live_share_the_same_single_event_integration_program', async () => {
  const source = await read('src/Wanxiangshu/Persistence/EventStore/CanonicalIntegrator.fs')

  assert.match(source, /integrateOne/)
  assert.match(source, /replay/)
  assert.match(source, /integrateLive/)

  // Both entry points must delegate to the same single-event primitive rather than maintaining two folds.
  const calls = source.match(/integrateOne/g) ?? []
  assert.ok(calls.length >= 3, 'definition + replay + live should all reference integrateOne')
  assert.doesNotMatch(source, /replayFold|liveFold|replayReducer|liveReducer/)
})

test('DURABLE_EVENTS_019_only_CanonicalIntegrator_may_derive_Current_from_event_history', async () => {
  const project = await read('src/Wanxiangshu/Wanxiangshu.fsproj')
  assert.match(project, /Persistence\/EventStore\/CanonicalIntegrator\.fs/)
  assert.match(project, /Persistence\/EventStore\/ProcessEventLog\.fs/)

  const workspaceStore = await read('src/Wanxiangshu/OpenCode/Host/WorkspaceEventStore.fs')
  assert.match(workspaceStore, /CanonicalIntegrator|EventStore\.createLocal/)
  assert.doesNotMatch(workspaceStore, /ProcessGitRawStore\.create/)
})
