import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const toml = await import("../../../dist/Context/Companion/Blogger/TomlSurface.js");
const owner = await import("../../../dist/Context/Companion/ProjectionSurface.js");

const ident = owner
const prompt = owner
const proj = owner
const spy = (input) => `«${input}»`
const frames = (count) =>
  Array.from({ length: count }, (_, n) => ({ digest: `sha-f${n}`, body: `frame body ${n}` }))
const dataItems = [{ role: 'user', kind: 'text', text: 'work', truncated: false }]
const dataToml = '[[new_work_to_record]]\nuser = "work"\n'
const combinedDelta = prompt.newWork(dataItems)
const isHistoricFrame = (text) => text.startsWith('[[do_not_exec]]') && text.includes('historic_frame')
const isCombinedNormalDelta = (text) =>
  text.startsWith('# Write the dense work-log continuation now') && text.includes('[[new_work_to_record]]')
const isPreviousTip = (text) => text.includes('previous_enforcer_tip')

test('WHAT[CONTEXT-COMPRESSION-017] COMPANION_010_same_session_lwr_returns_responsibility_without_delegation_fields', () => {
  const lwr = 'Opening\nhuman-root task\n\nChronicle\nself history'
  const block = prompt.memoryBlock(lwr)

  for (const line of ['Opening', 'human-root task', 'Chronicle', 'self history']) {
    assert.match(block, new RegExp(`^# ${line}$`, 'm'))
  }
  assert.doesNotMatch(block, /(?:^|\n)commissioner_record\s*=/)
  assert.doesNotMatch(block, /(?:^|\n)attached_work_record\s*=/)
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const prefix = await import("../../../dist/Context/Prefix/Surface.js");
const magicTodo = await import("../../../dist/Mission/Obligation/Todo/MagicTodoSemanticSurface.js");

const floor = ({ hasOpenLife = true, planCommitted = false, xTraceHeadSequence = 0, legacyProtectedPrefixEnd, parts = [] } = {}) =>
  magicTodo.effectiveOpeningFloor(hasOpenLife, planCommitted, 1, null, null, xTraceHeadSequence, parts)

test('WHAT[CONTEXT-COMPRESSION-017] CTX_016_pre_t1_floor_stops_after_true_opening', () => {
  assert.equal(
    Number(floor({ planCommitted: false, xTraceHeadSequence: 17 })),
    2,
    'Pre-T1 planning material after the opening is ordinary compressible history',
  )
})
test('WHAT[CONTEXT-COMPRESSION-017] CTX_016_t1_does_not_change_the_compression_floor', () => {
  assert.equal(Number(floor({ planCommitted: false, xTraceHeadSequence: 17 })), 2)
  assert.equal(Number(floor({ planCommitted: true, xTraceHeadSequence: 17 })), 2)
})
test('WHAT[CONTEXT-COMPRESSION-017] CTX_016_work_activated_is_inert_and_does_not_move_the_floor', () => {
  const without = Number(floor({ xTraceHeadSequence: 2 }))
  const withLegacy = Number(floor({ xTraceHeadSequence: 2, legacyProtectedPrefixEnd: 42 }))

  assert.equal(withLegacy, without, 'WorkActivated (inert legacy) must not change the structural floor')
  assert.notEqual(withLegacy, 42, 'the legacy ProtectedPrefixEndSequence (42) must never be read')
})
test('WHAT[CONTEXT-COMPRESSION-017] CTX_016_blogger_effective_start_is_max_of_record_coverage_and_floor', () => {
  assert.equal(
    Number(magicTodo.bloggerEffectiveStart(1, 3)),
    3,
    'coverage behind floor → effective start = floor',
  )

  assert.equal(
    Number(magicTodo.bloggerEffectiveStart(5, 3)),
    5,
    'coverage ahead of floor → effective start = record coverage',
  )
})
}
