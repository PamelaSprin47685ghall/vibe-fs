import assert from 'node:assert/strict'
import test from 'node:test'
import * as gate from '../../../dist/Persistence/UnifiedStoreGateSurface.js'

test('WHAT[DURABLE-EVENTS-016] scanner ids cover unified-store clean-break and history ownership rules', () => {
  assert.equal(gate.isGitObjectExposedToDomain(), false)
})

test('WHAT[DURABLE-EVENTS-016] fixture unified-store-feature-ref.fs is RED for feature-ref', () => {
  assert.equal(gate.isRefAllowedInFeature('refs/heads/feature-custom'), false)
})

test('WHAT[DURABLE-EVENTS-016] fixture unified-store-git-bypass.fs is RED for git-bypass', () => {
  assert.equal(gate.isGitBypassAllowed(), false)
})

test('WHAT[DURABLE-EVENTS-016] canonical refs/wanxiang/store is allowed only under Persist/Git ownership', () => {
  assert.equal(gate.isCanonicalStoreRefAllowed('Persist/Git'), true)
  assert.equal(gate.isCanonicalStoreRefAllowed('Application/Feature'), false)
})
