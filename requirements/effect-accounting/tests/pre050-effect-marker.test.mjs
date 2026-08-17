import assert from 'node:assert/strict'
import test from 'node:test'
import { readFileSync } from 'node:fs'

const source = readFileSync(new URL('../../../src/Wanxiangshu/Persistence/Journal/FactCodec.fs', import.meta.url), 'utf8')

test('WHAT[EFFECT-ACCOUNTING-010] PERSIST_005_pre050_marker_refuses_with_migration_message', () => {
  for (const marker of [
    'FailuresOnCurrentSide',
    'IsDead',
    'BaseModelID',
    'AgentLinked',
    'OrchestratorPublished',
    'EnforcementCycleCommitted',
    'DurableEffectRequested',
    'DurableEffectAccepted',
  ]) {
    assert.equal(source.includes(marker), true, marker)
  }
  assert.match(source, /pre050MigrationMessage/)
  assert.match(source, /containsLegacyFallbackFields/)
})
