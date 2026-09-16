import assert from 'node:assert/strict'
import test from 'node:test'
import * as finalize from '../../../dist/Execution/Session/InspectorFinalizeSettlementSurface.js'
import { assertFatalBoundary } from '../../structured-workflow/tests/support/m6-boundary-proof.mjs'

test('WHAT[MANAGED-SESSION-021] INSPECTOR_SETTLE_finalized_releases_identity_exactly_once', async () => {
  const r = await finalize.testFinalizedReleasesIdentity()
  assert.equal(r.releases, 1)
})

test('WHAT[MANAGED-SESSION-021] INSPECTOR_SETTLE_nothing_to_finalize_is_not_a_failure', async () => {
  const r = await finalize.testNothingToFinalize()
  assert.equal(r.ok, true)
})

test('WHAT[MANAGED-SESSION-021] INSPECTOR_SETTLE_identity_retention_is_owner_driven_not_finally', async () => {
  const r = await finalize.testIdentityRetentionOwnerDriven()
  assert.equal(r.ownerDriven, true)
})

test('WHAT[MANAGED-SESSION-021] lifecycle fatal follows exact drain and one injected fuse', () => assertFatalBoundary('managed-session-lifecycle'))
