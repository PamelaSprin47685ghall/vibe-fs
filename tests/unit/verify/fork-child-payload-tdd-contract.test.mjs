// tests/unit/verify/fork-child-payload-tdd-contract.test.mjs — PENDING 7 RED.
//
// Layer 0 static contract: `ForkChildPayload` must carry the Coder TDD phase all
// the way from the domain record into the rendered child payload.
//
// PENDING 7: Manager `fork` of a coder role requires `tdd = red | green`
// (CoderTool schema / TddPhase wire codec), but ForkTool.fs composes the TDD text
// with `TddPhase.composeAssignment` into the bare prompt and never routes the
// phase through `ForkChildPayload.render`. The child's payload therefore has no
// durable TDD phase, and the facade cannot render it.
//
// RED by construction: every assertion below targets a missing fact.
// Static source assertions only — never import dist, never import domain.mjs,
// so this file cannot break any other test.
import assert from 'node:assert/strict'
import { existsSync, readFileSync } from 'node:fs'
import { join } from 'node:path'
import test from 'node:test'

const ROOT = new URL('../../../', import.meta.url).pathname
const PAYLOAD_FS = join(ROOT, 'src/Wanxiangshu/Domain/ForkChildPayload.fs')
const FORK_TOOL_FS = join(ROOT, 'src/Wanxiangshu/Infrastructure/OpenCode/Tools/ForkTool.fs')
const DOMAIN_FACADE = join(ROOT, 'tests/unit/support/domain.mjs')

test('FORK_CHILD_PAYLOAD_TDD_001_assignment_carries_tdd_phase_field', () => {
  assert.ok(existsSync(PAYLOAD_FS), 'src/Wanxiangshu/Domain/ForkChildPayload.fs must exist')
  const source = readFileSync(PAYLOAD_FS, 'utf8')
  const recordStart = source.indexOf('type ForkChildAssignment =')
  const moduleStart = source.indexOf('[<RequireQualifiedAccess>]')

  assert.ok(recordStart !== -1, 'ForkChildPayload.fs must define ForkChildAssignment')
  assert.ok(moduleStart !== -1 && moduleStart > recordStart, 'ForkChildAssignment must precede the module')
  const recordBlock = source.slice(recordStart, moduleStart)

  // PENDING 7: the assignment record must carry the TDD phase as an optional field
  // so the renderer can inject the phase into the child payload.
  assert.match(
    recordBlock,
    /\bTddPhase\s*:\s*TddPhase\s+option/,
    'ForkChildAssignment must carry a `TddPhase: TddPhase option` field',
  )
})

test('FORK_CHILD_PAYLOAD_TDD_002_render_references_tdd_phase', () => {
  assert.ok(existsSync(PAYLOAD_FS), 'src/Wanxiangshu/Domain/ForkChildPayload.fs must exist')
  const source = readFileSync(PAYLOAD_FS, 'utf8')
  const renderStart = source.indexOf('let render')
  const relayStart = source.indexOf('let relay')

  assert.ok(renderStart !== -1, 'ForkChildPayload.fs must define render')
  assert.ok(relayStart !== -1 && relayStart > renderStart, 'render must precede relay')
  const renderBody = source.slice(renderStart, relayStart)

  // PENDING 7: `render` must reference the phase and inject the TDD comment/field
  // into the rendered child payload.
  assert.match(
    renderBody,
    /TddPhase|tdd|TDD|phase/i,
    'render must reference TddPhase and inject the TDD instruction/field',
  )
})

test('FORK_CHILD_PAYLOAD_TDD_003_facade_render_destructures_tdd', () => {
  assert.ok(existsSync(DOMAIN_FACADE), 'tests/unit/support/domain.mjs must exist')
  const facade = readFileSync(DOMAIN_FACADE, 'utf8')
  const blockStart = facade.indexOf('export const forkChildPayload')
  const blockEnd = facade.indexOf('export const tddPhase')

  assert.ok(blockStart !== -1, 'domain.mjs must export forkChildPayload')
  assert.ok(blockEnd !== -1 && blockEnd > blockStart, 'forkChildPayload must precede tddPhase')
  const block = facade.slice(blockStart, blockEnd)

  // PENDING 7: the facade render must receive/destructure `tdd` so the production
  // payload renderer's phase parameter is reachable from the mjs test layer.
  assert.match(
    block,
    /\btdd\b/i,
    'forkChildPayload.render must receive/destructure a `tdd` parameter',
  )
})

test('FORK_CHILD_PAYLOAD_TDD_004_executeManager_forwards_tdd_phase_into_payload', () => {
  assert.ok(existsSync(FORK_TOOL_FS), 'src/Wanxiangshu/Infrastructure/OpenCode/Tools/ForkTool.fs must exist')
  const source = readFileSync(FORK_TOOL_FS, 'utf8')
  const managerStart = source.indexOf('let private executeManager')
  const orchestratorStart = source.indexOf('let private executeOrchestrator')

  assert.ok(managerStart !== -1, 'ForkTool.fs must define executeManager')
  assert.ok(
    orchestratorStart !== -1 && orchestratorStart > managerStart,
    'executeManager must precede executeOrchestrator',
  )
  const body = source.slice(managerStart, orchestratorStart)

  // PENDING 7: the child payload must be produced through ForkChildPayload.render
  // (or a ForkChildAssignment construction) with the TddPhase passed in. The two
  // assertions are combined: `tddError` already matches a loose /tdd/i, so the
  // render/assignment surface AND the TddPhase reference must both be present.
  const failures = []
  if (!/ForkChildPayload\s*\.\s*render|ForkChildAssignment/.test(body)) {
    failures.push('executeManager must call ForkChildPayload.render or construct ForkChildAssignment')
  }
  if (!/\bTddPhase\b/.test(body)) {
    failures.push('executeManager must pass TddPhase into the child payload')
  }

  assert.deepEqual(
    failures,
    [],
    'executeManager must route TddPhase through ForkChildPayload.render',
  )
})
