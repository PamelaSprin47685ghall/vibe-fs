import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { assertJsData } = await import("../../verification-system/tests/support/js-contract.mjs");

const fission = await import('../../../dist/Execution/Fission/Surface.js')
const mustOk = (result) => {
  assertJsData(result, 'Fission operation result')
  assert.equal(result.ok, true, JSON.stringify(result))
  return result
}

test('WHAT[INTRA-PARTICIPANT-PARALLELISM-014] control-plane successors run before lane settlement and final takeover', () => {
  for (const phase of ['lane', 'takeover']) {
    for (const observation of ['running', 'needs-continuation', 'provider-failed', 'degeneration-interrupted']) {
      assert.equal(
        fission.settlementDecision(phase, observation),
        'yield-to-turn-workflow',
        `${phase}/${observation} must remain inside the ordinary continuation owner`,
      )
    }
  }

  assert.equal(fission.settlementDecision('lane', 'completed'), 'materialize-lane')
  assert.equal(fission.settlementDecision('takeover', 'completed'), 'complete-owner')
  assert.equal(fission.settlementDecision('lane', 'external-abort'), 'fail-group')
  assert.equal(fission.settlementDecision('takeover', 'external-abort'), 'fail-group')
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { readFileSync } = await import("node:fs");
const { resolve } = await import("node:path");
const tr = await import("../../../dist/OpenCode/Tools/ToolRegistrySurface.js");

const root = resolve(import.meta.dirname, '../../..')
const read = (p) => readFileSync(resolve(root, p), 'utf8')
const fissionProduction = () => [
  'src/Wanxiangshu/Execution/Fission/Model.fs',
  'src/Wanxiangshu/Execution/Fission/Admission.fs',
  'src/Wanxiangshu/Execution/Fission/Runtime.fs',
  'src/Wanxiangshu/Execution/Fission/OpenCode/Tool.fs',
].map(read).join('\n')

test('WHAT[INTRA-PARTICIPANT-PARALLELISM-014] Degeneration guard remains control-plane owner before Fission settlement', () => {
  const observer = read('src/Wanxiangshu/OpenCode/Host/HostTurnObserver.fs')
  const host = read('src/Wanxiangshu/Execution/Fission/OpenCode/Host.fs')

  assert.match(observer, /FissionHost\.observeLaneTurn[\s\S]{0,300}?abortCause/)
  assert.match(host, /AbortCause\.DegenerationGuard[\s\S]{0,300}?FissionSettlementObservation\.DegenerationInterrupted/)
  assert.match(host, /FissionLaneSettlementDecision\.YieldToTurnWorkflow/)
  assert.match(host, /FissionTakeoverSettlementDecision\.YieldToTurnWorkflow/)
})
}
