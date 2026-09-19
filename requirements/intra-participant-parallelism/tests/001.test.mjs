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

test('WHAT[intra-participant-parallelism-001] lanes carry no provider-visible identity or handle and keep the same logical participant', () => {
  const lane = fission.startedLane(1, 'lane-session-1', 'lane input')
  assertJsData(lane, 'started lane')
  assert.equal(lane.index, 1)
  assert.equal(lane.prompt, 'lane input')
  assert.equal('sessionId' in lane, false, 'physical lane session identity must stay inside Host')
  assert.equal(lane.hasAgentId, false, 'lane record must not expose a provider-visible AgentId')
  assert.equal(lane.hasHandle, false, 'lane record must not expose a provider-visible handle')
  assert.equal(lane.hasParent, false, 'lane record must not add a parent join obligation of its own')

  const startup = fission.startup(2, 0, 'lane A', 'CANONICAL-LWR')
  assert.match(startup, /same logical participant/, 'startup keeps the lanes under one logical identity')
  assert.match(startup, /Do not treat sibling lanes as delegated agents/, 'startup must not turn lanes into new delegation identities')
})
}

{
const { default: assert } = await import("node:assert/strict");
const { readFileSync } = await import("node:fs");
const { default: test } = await import("node:test");

const read = (path) => readFileSync(path, 'utf8')

test('WHAT[intra-participant-parallelism-001] TaskResult and Parallel helpers are pure utilities with no authority side effects', () => {
  const taskResult = read('src/Wanxiangshu/Foundation/TaskResult.fs')
  const parallel = read('src/Wanxiangshu/Foundation/Parallel.fs')
  assert.match(taskResult, /taskResult\b/)
  assert.match(parallel, /mapBounded\b/)
  assert.doesNotMatch(taskResult, /AgentJournal|appendAgent|AbortSession|Obligation/)
  assert.doesNotMatch(parallel, /AgentJournal|appendAgent|AbortSession|Obligation/)
})
}
