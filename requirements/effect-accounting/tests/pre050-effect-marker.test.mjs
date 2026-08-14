// Split from tests/unit/journal/fact-codec.test.mjs (cutover Wave 2a); owner: effect-accounting
//
// EFFECT-ACCOUNTING-010: the pre-0.5.0 generic DurableEffectRequested /
// DurableEffectAccepted union markers are refused with the migration message;
// typed effect facts replace them (typed_effect_facts_replace_the_generic_durable_effect_union).

import assert from 'node:assert/strict'
import test from 'node:test'

import { journal } from '../../verification-system/tests/support/domain.mjs'

test('PERSIST_005_pre050_marker_refuses_with_migration_message', () => {
  for (const marker of [
    '"FailuresOnCurrentSide"',
    '"IsDead"',
    '"BaseModelID"',
    '"AgentLinked"',
    '"OrchestratorPublished"',
    '"EnforcementCycleCommitted"',
    '"DurableEffectRequested"',
    '"DurableEffectAccepted"',
  ]) {
    assert.equal(journal.containsLegacyFallbackFields(`{"RuntimeFact":${marker}}`), true, marker)
  }

  const decoded = journal.deserializeFact('{"AgentLinked":{"SessionId":"s"}}')
  assert.equal(decoded.ok, false)
  assert.equal(decoded.error, journal.pre050MigrationMessage)
})
