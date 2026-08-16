// Split from tests/unit/enforcer/tip-v2-contract.test.mjs (cutover Wave 2a); owner: guidance-delivery.
//
// GD-008/GD-011 delivery surface: previous_enforcer_tip blocks in the work
// record (paired with frames) and the prompt anti-repeat / severe-exception
// law. The catalog/codec/RecentTips half (ENFORCER_TIP_01..12, 15) moved to
// behavior-diagnosis (tip-v2-contract.test.mjs).

import assert from 'node:assert/strict'
import test from 'node:test'
import { readFileSync } from 'node:fs'
import { fileURLToPath } from 'node:url'
import { dirname, join } from 'node:path'
import {
  companionPrompt as prompt,
  companionProjection as proj,
  bloggerToml as toml,
} from '../../verification-system/tests/support/domain.mjs'

const ROOT = join(dirname(fileURLToPath(import.meta.url)), '../../..')
const bloggerRoleLawPath = join(ROOT, 'resources/provider/role/blogger/en.md')

// ── 13. work record previous_enforcer_tip blocks (paired with frames) ───────

test('WHAT[GD-008] ENFORCER_TIP_13_work_record_contains_previous_enforcer_tip_blocks', () => {
  const block = toml.renderPreviousEnforcerTip('primitive-obsession', 'msg_c1')
  assert.match(block, /\[\[do_not_exec\]\]/)
  assert.match(block, /kind = "previous_enforcer_tip"/)
  assert.match(block, /tip = "primitive-obsession"/)
  assert.match(block, /cycle = "msg_c1"/)

  const plan = proj.build((s) => `«${s}»`, {
    blogger: 'ses_y',
    epoch: 0,
    kind: proj.normal,
    frames: [
      { digest: 'sha-f0', body: 'frame body 0' },
      { digest: 'sha-f1', body: 'frame body 1' },
    ],
    delta: { messageId: 'msg_delta', toml: '[[new_work_to_record]]\nuser = "work"' },
    previousTips: [
      { field: 'primitive-obsession', cycleId: 'msg_c1' },
      { field: 'ignored-tdd', cycleId: 'msg_c2' },
    ],
  })

  const tipTexts = plan.texts.filter((t) => t.includes('previous_enforcer_tip'))
  assert.equal(tipTexts.length, 2)
  assert.match(tipTexts[0], /tip = "primitive-obsession"/)
  assert.match(tipTexts[1], /tip = "ignored-tdd"/)
  // Paired observation units: tip₀, frame₀, tip₁, frame₁, delta (not tips∥frames).
  assert.equal(plan.roles.length, 5)
  assert.match(plan.texts[0], /previous_enforcer_tip/)
  assert.equal(plan.texts[1].includes('historic_frame'), true)
  assert.match(plan.texts[2], /previous_enforcer_tip/)
  assert.equal(plan.texts[3].includes('historic_frame'), true)
  assert.equal(plan.messages.at(-1).physical, true)
})

// ── 14. prompt anti-repeat + severe exception ───────────────────────────────

test('WHAT[GD-008] ENFORCER_TIP_14_prompt_has_anti_repeat_and_severe_exception', () => {
  const roleLaw = readFileSync(bloggerRoleLawPath, 'utf8')
  assert.match(roleLaw, /One observation[\s\S]*One lesson[\s\S]*One listener/)
  assert.match(roleLaw, /Do not avoid a repeated lesson/)
  assert.match(roleLaw, /Repetition is legal|Diversity is not a goal/i)
  assert.doesNotMatch(roleLaw, /omit all scores|omit zero-valued scores/i)

  assert.match(prompt.normalInstruction, /exactly once/)
  assert.match(prompt.normalInstruction, /required tip|catalog field/)
  assert.match(prompt.squashInstruction, /required tip|catalog field/)
  assert.match(prompt.squashInstruction, /exactly once/)
  assert.doesNotMatch(prompt.squashInstruction, /omit all scores/)
  assert.doesNotMatch(prompt.normalInstruction, /omit.*scores/i)

  const tipMessage = prompt.previousTip('primitive-obsession', 'cycle-1')
  assert.match(tipMessage, /previous_enforcer_tip/)
})
