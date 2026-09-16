// requirements/structured-workflow/tests/boundary-exemption-ratchet.test.mjs
//
// Regression ratchet: boundary assertions replaced by behavioral contracts.
//
// 1. OrchestratorProjection no-fold constraint:
//    - Replay/recovery contracts reconstruct state directly from facts without resumable continuation addresses.
//
// 2. SuicideTool retirement dispatch ownership (SW-006 / RETIRE-001 / RETIRE-003):
//    - Tool admission resolves OfficeRole and ToolPermission.Finality for Manager, and rejects non-manager roles.
//    - Freeze fence rejects concurrent or stale admissions, and decision completes without session-scoped abort.

import assert from 'node:assert/strict'
import test from 'node:test'
import * as tr from '../../../dist/OpenCode/Tools/ToolRegistrySurface.js'
import * as retirement from '../../../dist/Mission/Relay/Retirement/Surface.js'

// ── 2. OrchestratorProjection no-fold constraint ────────────────────────────

test('WHAT[STRUCTURED-WORKFLOW-003] Orchestrator retirement and decision state operates without resumable workflow continuation addresses', () => {
  const decision = retirement.decide([], {
    assessed: true,
    openObligations: 0,
    testsPassing: true,
    dirty: false,
    unmerged: false,
  })
  assert.deepEqual(decision, { decision: 'Retire' })
})

// ── 3. SuicideTool retirement dispatch ownership ────────────────────────────

test('WHAT[STRUCTURED-WORKFLOW-006] SuicideTool admission gate requires OfficeRole and admits Manager while rejecting others', () => {
  assert.equal(tr.admissionAuthority('suicide'), 'office')
  assert.equal(tr.rolePredicate('suicide', 'Manager'), true)
  assert.equal(tr.rolePredicate('suicide', 'Coder'), false)
  assert.equal(tr.rolePredicate('suicide', 'Orchestrator'), false)
  assert.equal(tr.rolePredicate('suicide', 'Inspector'), false)
})

test('WHAT[STRUCTURED-WORKFLOW-006] SuicideTool retirement freeze fence order rejects concurrent and stale admissions without session abort', () => {
  const frozen = retirement.freeze('inc-mgr-1', 100)
  assert.equal(retirement.fenceAppliesTo(frozen, 'inc-mgr-1'), true)
  assert.equal(retirement.fenceAppliesTo(frozen, 'inc-other'), false)
  assert.deepEqual(retirement.admitResource(frozen, 100), { ok: false, error: 'IncumbencyAdmissionsFrozen' })
  assert.deepEqual(retirement.admitResource(frozen, 99), { ok: false, error: 'StaleIncumbencyAdmissionFence' })
  const decision = retirement.decide([], {})
  assert.deepEqual(decision, { decision: 'Retire' })
})
