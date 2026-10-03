import assert from 'node:assert/strict'
import test from 'node:test'
import { withExecutablePlugin } from '../../verification-system/tests/support/plugin-fixture.mjs'
import { admit, context } from './support/plugin.mjs'

const candidateOf = (tipName) => ({
  tipName,
  enforcerTextEn: 'Enforce the distilled mechanism.',
  enforcerTextZh: '执行提炼出的机制。',
  mainTextEn: 'Main handling text.',
  mainTextZh: 'Main 处置正文。',
  trigger: 'the same mechanism recurs',
  negative: 'a one-off local path is not this rule',
})

test('WHAT[institutional-learning-004] GAP-181: an admissible BIRTH candidate commits InstitutionalRuleBorn; conflicting or incomplete candidates are refused with zero live-rulebook change', async () => {
  await withExecutablePlugin(async (hooks, _directory, created, runtime) => {
    const session = 'learning-birth-admission'
    await admit(runtime, session)
    const ctx = (id) => context(session, id)
    const experience = 'A reusable mechanism emerged from this work.'

    const birth = await hooks.tool.celebrate.execute(
      { experience, candidate: candidateOf('fresh-birth-tip') },
      ctx('birth-1'),
    )
    assert.match(birth, /BIRTH/)
    assert.match(birth, /fresh-birth-tip/)

    // Replay of the same occurrence returns the frozen receipt.
    assert.equal(
      await hooks.tool.celebrate.execute({ experience, candidate: candidateOf('fresh-birth-tip') }, ctx('birth-1')),
      birth,
    )

    // The born rule joined the live union: a second candidate with the same
    // TipName is refused and nothing new is created.
    const conflict = await hooks.tool.regret.execute(
      { experience, candidate: candidateOf('fresh-birth-tip') },
      ctx('conflict-1'),
    )
    assert.equal(conflict.includes('BIRTH'), false)

    // A missing bilingual leaf refuses BIRTH.
    const partial = { ...candidateOf('partial-tip'), enforcerTextZh: '' }
    const refused = await hooks.tool.celebrate.execute({ experience, candidate: partial }, ctx('partial-1'))
    assert.equal(refused.includes('BIRTH'), false)

    // Zero live-rulebook change beyond the admitted birth: a new unique
    // TipName still births.
    const second = await hooks.tool.celebrate.execute(
      { experience, candidate: candidateOf('second-birth-tip') },
      ctx('birth-2'),
    )
    assert.match(second, /BIRTH/)
    assert.match(second, /second-birth-tip/)

    assert.deepEqual(created, [])
    assert.deepEqual(runtime.prompts, [])
  })
})
