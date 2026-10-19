import assert from 'node:assert/strict'
import test from 'node:test'
import { readFileSync } from 'node:fs'
import { parse as parseToml } from 'smol-toml'
import * as toml from '../../../dist/Context/Companion/Blogger/TomlSurface.js'
import * as companion from '../../../dist/Context/Companion/ProjectionSurface.js'
import { integrationTest } from '../../verification-system/tests/support/tier-gate.mjs'
import { withRulebookPackage } from '../../behavior-diagnosis/tests/support/resource-package.mjs'
import { guidance, link, observe, withJournal, main, blogger, tip, authoredBody } from './support/journal.mjs'

integrationTest('WHAT[guidance-delivery-008] Blogger composer takes detection source regardless of section vocabulary', async () => {
  await withRulebookPackage(async ({ write, surface }) => {
    write('sample-tip', 'lesson.md', '## Opinion\nDETECTION SOURCE\n\n## Advice\nREMEDIATION SOURCE')
    const prompt = surface.composeBloggerSystemPrompt('base', 'en')
    assert.ok(prompt.includes('DETECTION SOURCE'))
    assert.ok(prompt.includes('Opinion'))
    assert.ok(prompt.includes('REMEDIATION SOURCE'))
  })
})

test('WHAT[guidance-delivery-008] real Main resolver uses the authored remediation body rather than detection body', async () => {
  await withJournal(async ({ journal }) => {
    await link(journal)
    await observe(journal)
    const result = await guidance.resolve(journal, main)
    const detection = readFileSync(new URL(`../../../resources/enforcer/${tip}/lesson.md`, import.meta.url), 'utf8').trim()
    assert.ok(result.text.includes(authoredBody))
    assert.equal(result.text.includes(detection), false)
  })
})

test('WHAT[guidance-delivery-008] previous tip retains its identity as low-trust data paired before its frame', () => {
  const tipName = 'sample-tip'
  const block = parseToml(toml.renderPreviousEnforcerTip(tipName, 'cycle-1'))
  assert.deepEqual(block.do_not_exec, [{ kind: 'previous_enforcer_tip', tip: tipName, cycle: 'cycle-1' }])
  const plan = companion.build((s) => s, {
    blogger: 'blogger', epoch: 0, kind: companion.normal,
    frames: [{ digest: 'frame-digest', body: 'frame body' }],
    delta: { messageId: 'delta', toml: '[[new_work_to_record]]\nuser = "work"' },
    previousTips: [{ tipName, cycleId: 'cycle-1' }],
  })
  assert.equal(plan.roles[0], 'assistant')
  assert.deepEqual(parseToml(plan.texts[0]).do_not_exec, block.do_not_exec)
  assert.ok(plan.texts[1].includes('historic_frame'))
})


test('WHAT[guidance-delivery-008] Main guidance resolution keeps Blogger detection history out of the remediation body', async () => {
  await withJournal(async ({ journal }) => {
    await link(journal)
    // Blogger observes the tip twice: the observation history is Blogger's
    // low-trust material, never a Main authority source.
    await observe(journal, 1)
    await observe(journal, 2)

    const resolved = await guidance.resolve(journal, main)
    const detection = readFileSync(new URL(`../../../resources/enforcer/${tip}/lesson.md`, import.meta.url), 'utf8').trim()
    assert.ok(resolved.text.includes(authoredBody), 'Main guidance carries the authored remediation body')
    assert.equal(resolved.text.includes(detection), false, 'Blogger detection history must not be promoted into Main guidance')

    // The Blogger-side view keeps its own detection material: the histories
    // stay isolated per audience rather than merging into one authority.
    const bloggerView = await guidance.latest(journal, blogger)
    assert.ok(bloggerView, 'Blogger keeps its own observation view')
  })
})


test.todo('WHAT[guidance-delivery-008] complete Main Host projection never promotes Blogger history into interaction authority (GAP-116: full Host projection path pending — the resolver isolation test above covers the guidance text only)')
