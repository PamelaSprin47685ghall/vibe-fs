import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { readFileSync } = await import("node:fs");
const { join } = await import("node:path");
const { default: test } = await import("node:test");

const root = new URL('../../../', import.meta.url).pathname
const read = (path) => readFileSync(join(root, path), 'utf8')
const firstCheckpointSurfaces = [
  ['planning-table/en', 'resources/provider/lifecycle/manager/planning-table/en.md'],
  ['planning-table/zh-CN', 'resources/provider/lifecycle/manager/planning-table/zh-CN.md'],
  ['todowrite-description/en', 'resources/provider/lifecycle/magic-todo/todowrite-description/en.md'],
  ['todowrite-description/zh-CN', 'resources/provider/lifecycle/magic-todo/todowrite-description/zh-CN.md'],
]

test('WHAT[OBLIGATION-LEDGER-027] provider prose freezes progressive elaboration around workingOn', () => {
  for (const path of [
    'resources/provider/lifecycle/magic-todo/todowrite-description/en.md',
    'resources/provider/lifecycle/magic-todo/todowrite-description/zh-CN.md',
    'resources/provider/lifecycle/magic-todo/plan-complete-description/en.md',
    'resources/provider/lifecycle/magic-todo/plan-complete-description/zh-CN.md',
    'resources/provider/lifecycle/magic-todo/working-on-description/en.md',
    'resources/provider/lifecycle/magic-todo/working-on-description/zh-CN.md',
    'resources/provider/lifecycle/magic-todo/obligation-work-description/en.md',
    'resources/provider/lifecycle/magic-todo/obligation-work-description/zh-CN.md',
  ]) {
    const text = read(path)
    assert.match(text, /near/i, `${path}: must explain near resolution`)
    assert.match(text, /far/i, `${path}: must explain far resolution`)
    assert.match(text, /frontier|前沿/i, `${path}: horizon must be relative to the execution frontier`)
  }

  for (const path of [
    'resources/provider/lifecycle/magic-todo/todowrite-description/en.md',
    'resources/provider/lifecycle/magic-todo/todowrite-description/zh-CN.md',
    'resources/provider/lifecycle/magic-todo/plan-complete-description/en.md',
    'resources/provider/lifecycle/magic-todo/plan-complete-description/zh-CN.md',
  ]) {
    const text = read(path)
    assert.match(text, /coverage|覆盖/i, `${path}: plan completeness means coverage`)
    assert.match(text, /uniform|均匀/i, `${path}: completeness must not mean uniform decomposition`)
  }
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const todo = await import("../../../dist/Mission/Obligation/Todo/MagicTodoSemanticSurface.js");

const sha256 = (value) => `digest:${value}`
const life = 'manager-life'
const firstCall = 'first-call'
const secondCall = 'second-call'
const obligation = (name, work, horizon = 'near') => ({ name, horizon, work })
const ok = (result) => {
  assert.equal(result.ok, true, result.ok ? '' : JSON.stringify(result.error))
  return result.value
}
const rejected = (result) => {
  assert.equal(result.ok, false, 'expected rejection')
  return result.error
}
const localized = (callId, ordinal, frontier, digest) => ({
  toolCallId: callId,
  toolPartOrdinal: ordinal,
  todowriteCallIds: [callId],
  reviewFrontier: frontier,
  providerInputDigest: digest,
})
const items = [
  obligation('implementation', 'Implement the requested behavior.'),
  obligation('verification', 'Verify the behavior with evidence.', 'far'),
]

test('WHAT[OBLIGATION-LEDGER-027] horizon is planning resolution, not provider-visible lifecycle state', () => {
  const wire = todo.canonicalObligationListWire([
    obligation('now', 'Close the directly actionable unit.', 'near'),
    obligation('next', 'Preserve the next meaningful outcome.', 'mid'),
    obligation('later', 'Cover the remaining outcome without premature steps.', 'far'),
  ])
  assert.match(wire, /"horizon":"near"/)
  assert.match(wire, /"horizon":"mid"/)
  assert.match(wire, /"horizon":"far"/)
  assert.doesNotMatch(wire, /"status"|"phase"|"priority"/)
})
}
