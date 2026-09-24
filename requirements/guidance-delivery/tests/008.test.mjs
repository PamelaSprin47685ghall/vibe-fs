import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { readdirSync, readFileSync } = await import("node:fs");
const { dirname, join } = await import("node:path");
const { fileURLToPath } = await import("node:url");
const enforcer = await import("../../../dist/Enforcer/Surface.js");

const ROOT = join(dirname(fileURLToPath(import.meta.url)), '../../..')
const RULEBOOK = join(ROOT, 'resources/enforcer')
const tipNames = () =>
  readdirSync(RULEBOOK, { withFileTypes: true })
    .filter((entry) => entry.isDirectory())
    .map((entry) => entry.name)
    .sort()

test('WHAT[guidance-delivery-008] AUDIENCE_001_main_md_sections_never_enter_blogger_system_prompt', () => {
  const composed = enforcer.composeBloggerSystemPrompt('base', 'en')

  // `## What To Do Now` is the main.md-only remediation section (0/120
  // enforcer.md contain it). The Blogger system must never see it.
  assert.equal(composed.includes('## What To Do Now'), false)
  // The `# Enforcer Tip` marker is the Main-only delivery header.
  assert.equal(composed.includes('# Enforcer Tip'), false)
})
test('WHAT[guidance-delivery-008] AUDIENCE_002_corpus_level_detection_and_remediation_do_not_leak', () => {
  for (const name of tipNames()) {
    const enforcerText = readFileSync(join(RULEBOOK, name, 'enforcer.md'), 'utf8')
    const mainText = readFileSync(join(RULEBOOK, name, 'main.md'), 'utf8')

    // Remediation protocol never re-does classification (Rulebook §22):
    // main.md must not carry the detection-only trigger section.
    assert.equal(
      mainText.includes('## Trigger When'),
      false,
      `${name}/main.md must not re-run detection (no "Trigger When" section)`,
    )
    // Detection doc never becomes a repair manual (Rulebook §21):
    // enforcer.md must not carry the remediation action section.
    assert.equal(
      enforcerText.includes('## What To Do Now'),
      false,
      `${name}/enforcer.md must not contain remediation instructions`,
    )
  }
})
test('WHAT[guidance-delivery-008] AUDIENCE_003_previous_tip_history_is_not_main_authority', () => {
  // ENFORCER-071 Y side: Blogger's own history is rendered as low-trust
  // previous_enforcer_tip ([[do_not_exec]], role=assistant) — it must not be
  // repurposed as Main instruction. The Main surface is TipGuidance only.
  const tipNamesList = tipNames()
  assert.ok(tipNamesList.length === 120)
  for (const name of tipNamesList) {
    const mainText = readFileSync(join(RULEBOOK, name, 'main.md'), 'utf8')
    assert.equal(
      mainText.includes('[[do_not_exec]]'),
      false,
      `${name}/main.md must not carry the Blogger low-trust history block`,
    )
  }
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { readFileSync } = await import("node:fs");
const { fileURLToPath } = await import("node:url");
const { dirname, join } = await import("node:path");
const companion = await import("../../../dist/Context/Companion/ProjectionSurface.js");
const toml = await import("../../../dist/Context/Companion/Blogger/TomlSurface.js");

const prompt = companion
const proj = companion
const ROOT = join(dirname(fileURLToPath(import.meta.url)), '../../..')
const bloggerRoleLawPath = join(ROOT, 'resources/provider/role/blogger/en.md')

test('WHAT[guidance-delivery-008] ENFORCER_TIP_13_work_record_contains_previous_enforcer_tip_blocks', () => {
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
test('WHAT[guidance-delivery-008] ENFORCER_TIP_14_prompt_has_anti_repeat_and_severe_exception', () => {
  const roleLaw = readFileSync(bloggerRoleLawPath, 'utf8')
  assert.match(roleLaw, /One observation[\s\S]*One lesson[\s\S]*One listener/)
  assert.match(roleLaw, /Do not avoid a repeated lesson/)
  assert.match(roleLaw, /Repetition is legal|Diversity is not a goal/i)
  assert.doesNotMatch(roleLaw, /omit all scores|omit zero-valued scores/i)

  // The instruction plane is localized per the bound provider language, so the
  // anchors accept either surface: "exactly once" / "恰好一次".
  assert.match(prompt.normalInstruction, /exactly once|恰好一次|一次/)
  assert.match(prompt.normalInstruction, /required tip|catalog field|必填 tip|目录中的一个字段/)
  assert.match(prompt.squashInstruction, /required tip|catalog field|必填 tip|目录中的一个字段/)
  assert.match(prompt.squashInstruction, /exactly once|恰好一次|一次/)
  assert.doesNotMatch(prompt.squashInstruction, /omit all scores/)
  assert.doesNotMatch(prompt.normalInstruction, /omit.*scores/i)

  const tipMessage = prompt.previousTip('primitive-obsession', 'cycle-1')
  assert.match(tipMessage, /previous_enforcer_tip/)
})
}
