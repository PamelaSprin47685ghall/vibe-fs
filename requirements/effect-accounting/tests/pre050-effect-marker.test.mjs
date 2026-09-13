import assert from 'node:assert/strict'
import test from 'node:test'
import * as codec from '../../../dist/Persistence/Journal/FactCodecSurface.js'

test('WHAT[EFFECT-ACCOUNTING-010] PERSIST_005_pre050_marker_refuses_with_migration_message', () => {
  const markers = [
    'FailuresOnCurrentSide',
    'IsDead',
    'BaseModelID',
    'AgentLinked',
    'OrchestratorPublished',
    'EnforcementCycleCommitted',
    'DurableEffectRequested',
    'DurableEffectAccepted',
  ]
  for (const marker of markers) {
    const payload = JSON.stringify({ legacyKey: marker, data: 42 })
    const result = codec.decode(payload)
    assert.equal(result.ok, false, `marker ${marker} must be rejected`)
    assert.equal(result.error, codec.pre050MigrationMessage, `marker ${marker} must produce pre050MigrationMessage`)
  }
})
