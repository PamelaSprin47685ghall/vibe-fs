import assert from 'node:assert/strict'
import test from 'node:test'
import * as learning from '../../../dist/Enforcer/InstitutionalLearning/Surface.js'
import { withExecutablePlugin } from '../../verification-system/tests/support/plugin-fixture.mjs'
import { admit, context } from './support/plugin.mjs'

test('WHAT[institutional-learning-003] evaluator consumes the supplied rule names for an explicit absorb claim rather than an independent hidden rule-name list', () => {
  const experience = 'known-rule applies to this success'
  // Same mechanism, different wording: the caller claims coverage by name;
  // the experience text itself never has to name the rule.
  assert.equal(learning.evaluate('the mechanism that made this work', ['known-rule'], null, 'known-rule').disposition, 'ABSORB')
  // The claim must hit the supplied rulebook: an empty or different list —
  // no hidden rule-name source can satisfy it.
  assert.equal(learning.evaluate(experience, [], null, 'known-rule').disposition, 'DISCARD')
  assert.equal(learning.evaluate(experience, ['different-rule'], null, 'known-rule').disposition, 'DISCARD')
  assert.equal(learning.evaluate(experience, ['known-rule']).disposition, 'DISCARD')
})

const candidate = {
  tipName: 'fresh-tip',
  enforcerTextEn: 'enforce the distilled mechanism',
  enforcerTextZh: '执行提炼出的机制',
  mainTextEn: 'main handling text',
  mainTextZh: 'Main 处置正文',
  trigger: 'the same mechanism recurs',
  negative: 'a one-off local path is not this rule',
}

test('WHAT[institutional-learning-003] GAP-181: candidate admission is mechanical — TipName conflict and incomplete bilingual leaves fall back; the abstraction itself stays with the caller', () => {
  assert.deepEqual(learning.evaluate('anything', ['known-rule'], candidate), { disposition: 'BIRTH' })
  assert.equal(learning.evaluate('anything', ['known-rule'], { ...candidate, tipName: 'known-rule' }).disposition, 'DISCARD')
  assert.equal(learning.evaluate('anything', ['known-rule'], { ...candidate, enforcerTextZh: '' }).disposition, 'DISCARD')
  assert.equal(learning.evaluate('anything', ['known-rule'], { ...candidate, mainTextEn: undefined }).disposition, 'DISCARD')
  // The caller owns the semantic judgment: an admissible candidate wins even
  // when the experience also mentions an existing rule name.
  assert.deepEqual(learning.evaluate('known-rule covered it', ['known-rule'], candidate), { disposition: 'BIRTH' })
})

test('WHAT[institutional-learning-003] GAP-181: an admissible candidate outranks a simultaneous explicit absorb claim — both supplied, BIRTH concludes', () => {
  // Both channels are legal and supplied together: a mechanically admissible
  // candidate (unique TipName) and an explicit claim naming a live rule. The
  // candidate channel concludes BIRTH; an evaluation that checked the absorb
  // claim first would conclude ABSORB here instead.
  assert.deepEqual(learning.evaluate('known-rule covered it', ['known-rule'], candidate, 'known-rule'), { disposition: 'BIRTH' })
})

test('WHAT[institutional-learning-003] GAP-181: substring matching is not an abstraction oracle — a mention without a caller claim never absorbs, and raw command/path/timestamp text never promotes', () => {
  // Same word, different mechanism: the experience names a live rule but the
  // caller does not claim coverage, so nothing absorbs.
  assert.equal(learning.evaluate('known-rule did not apply; a different mechanism was at work', ['known-rule']).disposition, 'DISCARD')
  // An explicit claim decides; the wording of the experience does not. The
  // claim names a rule of the supplied live rulebook even though the prose
  // says the rule was rejected — the caller claim, not the text, concludes.
  assert.equal(learning.evaluate('I considered known-rule but rejected it', ['known-rule'], null, 'known-rule').disposition, 'ABSORB')
  // Un-abstracted command flow, file path and timestamp are not promoted to
  // a permanent rule by the evaluator.
  assert.equal(learning.evaluate('ran git status && ls -la /home/user/project at 2026-10-04T10:00:00Z; exit 0', ['known-rule']).disposition, 'DISCARD')
  // The bounded evaluation is a pure decision over the supplied inputs.
  assert.deepEqual(
    learning.evaluate('same words twice', ['known-rule'], null, 'known-rule'),
    learning.evaluate('same words twice', ['known-rule'], null, 'known-rule'),
  )
})

const candidateOf = (tipName) => ({
  tipName,
  enforcerTextEn: 'Enforce the distilled mechanism.',
  enforcerTextZh: '执行提炼出的机制。',
  mainTextEn: 'Main handling text.',
  mainTextZh: 'Main 处置正文。',
  trigger: 'the same mechanism recurs',
  negative: 'a one-off local path is not this rule',
})

test('WHAT[institutional-learning-003] real tool chain: explicit absorb claim over the canonical live rulebook; raw experience is not institutionalized; the bounded evaluation performs no extra IO', async () => {
  await withExecutablePlugin(async (hooks, _directory, created, runtime) => {
    const session = 'learning-input-isolation'
    await admit(runtime, session)
    const ctx = (id) => context(session, id)

    // Raw command flow, file path and timestamp: no candidate, no claim —
    // nothing permanent is created.
    const raw = await hooks.tool.celebrate.execute(
      { experience: 'ran `git status && ls -la /home/user/project` at 2026-10-04T10:00:00Z; exit 0' },
      ctx('raw-1'),
    )
    assert.match(raw, /DISCARD/)
    assert.doesNotMatch(raw, /BIRTH/)

    // Birth one rule so the live rulebook contains a name known to the test.
    const birth = await hooks.tool.celebrate.execute(
      { experience: 'A reusable mechanism emerged from this work.', candidate: candidateOf('iso-tip') },
      ctx('birth-1'),
    )
    assert.match(birth, /BIRTH/)

    // Same word, different mechanism on the real chain: the experience
    // mentions the born rule but the caller makes no claim — no ABSORB.
    const mention = await hooks.tool.regret.execute(
      { experience: 'iso-tip did not apply here; the mechanism was different.' },
      ctx('mention-1'),
    )
    assert.doesNotMatch(mention, /ABSORB/)

    // Same mechanism, different wording on the real chain: an explicit claim
    // naming a live rule concludes ABSORB.
    const covered = await hooks.tool.celebrate.execute(
      { experience: 'the same trick worked again in another area.', absorbedRule: 'iso-tip' },
      ctx('covered-1'),
    )
    assert.match(covered, /ABSORB/)

    // A claim naming a rule outside the live rulebook falls back to DISCARD.
    const unknown = await hooks.tool.regret.execute(
      { experience: 'some mechanism worth judging.', absorbedRule: 'no-such-rule' },
      ctx('unknown-1'),
    )
    assert.match(unknown, /DISCARD/)

    // Input isolation: the bounded evaluation created no files and issued no
    // provider requests — there is no network or repository channel.
    assert.deepEqual(created, [])
    assert.deepEqual(runtime.prompts, [])
  })
})
