import assert from 'node:assert/strict'
import test from 'node:test'
import { scanRetryOwnership } from '../../../scripts/checks/retry-owner.mjs'
import { fileURLToPath } from 'node:url'

test('WHAT[host-provider-failure-ownership-005] retry ownership heuristic detects no known forbidden source pattern', () => {
  assert.deepEqual(scanRetryOwnership(fileURLToPath(new URL('../../../', import.meta.url))), [])
})

test.todo('WHAT[host-provider-failure-ownership-005] actual Host observer and Change Orchestrator cannot send recovery without the policy owner licence (GAP-143)')
