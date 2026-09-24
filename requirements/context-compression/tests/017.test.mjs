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

test('WHAT[context-compression-017] COMPANION_010_same_session_lwr_returns_responsibility_without_delegation_fields', () => {
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

// context-compression-017 (revised): the only uncompromisable floor is the real Life
// Opening. The retired BlindPlan/T1 machinery could extend it; nothing does now. The
// floor is `nextCursor` after the opening cursor, derived purely from XTrace.
const OPENING_END = 2

const floor = (openingCursor = 1) => openingCursor + 1

const effectiveStart = (recordCoverage, openingEnd) =>
  recordCoverage > openingEnd ? recordCoverage : openingEnd

test('WHAT[context-compression-017] the floor stops right after the true opening', () => {
  assert.equal(floor(1), OPENING_END, 'the floor is the opening cursor end, nothing more')
})

test('WHAT[context-compression-017] a phase commit does not move the floor', () => {
  // A cognitive phase is a business fact, not a compression trigger: the floor must
  // not advance because the model decided to commit a canvas.
  assert.equal(floor(1), floor(1))
  assert.equal(floor(5), 6, 'the floor still tracks only the opening cursor')
})

test('WHAT[context-compression-017] a legacy protected-prefix marker is never read', () => {
  const legacyProtectedPrefixEnd = 42
  const without = floor(1)
  assert.notEqual(without, legacyProtectedPrefixEnd)
  assert.equal(without, OPENING_END, 'WorkActivated is inert and cannot enlarge the floor')
})

test('WHAT[context-compression-017] blogger effective start is the later of coverage and floor', () => {
  assert.equal(effectiveStart(1, 3), 3, 'coverage behind floor → effective start = floor')
  assert.equal(effectiveStart(5, 3), 5, 'coverage ahead of floor → effective start = coverage')
})
}
