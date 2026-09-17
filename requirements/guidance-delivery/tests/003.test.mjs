import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const delivery = await import("../../../dist/Enforcer/Guidance/DeliverySurface.js");

const { empty, apply, applyReanchor, hasFullDelivered } = delivery
const TipPresentation = Object.freeze({ Full: 'Full', IdentityOnly: 'IdentityOnly' })

test('WHAT[GD-003] TDP_002_full_marks_tip_delivered_identity_only_does_not', () => {
  let state = apply('primitive-obsession', TipPresentation.Full, empty)
  assert.equal(hasFullDelivered('primitive-obsession', state), true)

  // IdentityOnly repeat is audit-only: it must not record a Full delivery.
  state = apply('ignored-tdd', TipPresentation.IdentityOnly, state)
  assert.equal(hasFullDelivered('ignored-tdd', state), false)
  assert.equal(hasFullDelivered('primitive-obsession', state), true)
})
test('WHAT[GD-003] TDP_003_blank_or_null_tip_name_is_ignored', () => {
  const afterBlank = apply('   ', TipPresentation.Full, empty)
  assert.equal(hasFullDelivered('   ', afterBlank), false)
  assert.deepEqual(afterBlank, empty)
  const afterNull = apply(null, TipPresentation.Full, empty)
  assert.deepEqual(afterNull, empty)
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { mkdtempSync, rmSync, readFileSync } = await import("node:fs");
const { tmpdir } = await import("node:os");
const { join, dirname } = await import("node:path");
const { fileURLToPath } = await import("node:url");
const guidance = await import("../../../dist/Enforcer/Guidance/TipSurface.js");
const language = await import("../../../dist/Participant/Provider/LanguageSurface.js");
const resources = await import("../../../dist/Resources/PromptSurface.js");

resources.runtimeInstallFromPackage()
const latestTipGuidance = guidance.latest
const latestTipNudge = guidance.latestNudge
const resolveTipGuidance = guidance.resolve
const ROOT = join(dirname(fileURLToPath(import.meta.url)), '../../..')
const MAIN_MD = readFileSync(
  join(ROOT, 'resources/enforcer/primitive-obsession/main.md'),
  'utf8',
).trim()
const MAIN_ZH_CN_MD = readFileSync(
  join(ROOT, 'resources/enforcer/primitive-obsession/main.zh-CN.md'),
  'utf8',
).trim()
const main = 'ses-tip-delivery-main'
const blogger = 'ses-tip-delivery-blogger'
const seedOwnerWithTip = async (journal, { tip = 'primitive-obsession', runSuffix = '1' } = {}) => {
  const linked = await guidance.appendCompanionLink(journal, {
    session: main,
    bloggerSession: blogger,
    bloggerAgent: 'blogger',
  })
  assert.equal(linked.ok, true, linked.error)

  const committed = await guidance.appendObservation(journal, {
    session: main,
    bloggerSession: blogger,
    requestId: `req-tip-${runSuffix}`,
    frameEpoch: 0,
    previousIngestedThrough: 0,
    nextIngestedThrough: Number(runSuffix),
    previousCutoff: 0,
    nextCutoff: Number(runSuffix),
    nextCoveredPrefixDigest: `digest-tip-${runSuffix}`,
    textRef: `blob-tip-${runSuffix}`,
    textDigest: `sha-tip-${runSuffix}`,
    providerRun: `run-tip-${runSuffix}`,
    toolCallIds: [`call-tip-${runSuffix}`],
    tipRuleId: tip,
    fieldNameAtCommit: tip,
    observedPrefixEpoch: 0,
  })
  assert.equal(committed.ok, true, committed.error)
}
const withJournal = async (fn) => {
  const directory = mkdtempSync(join(tmpdir(), 'wxs-tip-guidance-'))
  const created = await guidance.createJournal(directory)
  assert.equal(created.ok, true, created.error)
  try {
    return await fn(created.journal)
  } finally {
    guidance.disposeJournal(created.journal)
    rmSync(directory, { recursive: true, force: true })
  }
}
const presentationOf = (value) => value?.presentation
const textOf = (value) => value?.text

test('WHAT[GD-003] ENFORCER_TIP_DELIVERY_002_second_resolve_same_tip_is_identity_only', async () => {
  await withJournal(async (journal) => {
    await seedOwnerWithTip(journal)
    const first = await resolveTipGuidance(journal, main)
    assert.equal(presentationOf(first), 'Full')
    const firstText = textOf(first)

    const second = await resolveTipGuidance(journal, main)
    assert.ok(second, 'expected repeat guidance')
    assert.equal(presentationOf(second), 'IdentityOnly')
    const secondText = textOf(second)
    assert.equal(secondText, 'tip: primitive-obsession')
    assert.ok(!secondText.includes(MAIN_MD), 'Identity must not repeat full main.md')
    assert.ok(
      !secondText.includes('Introduce a distinct type so invalid substitutions become impossible.'),
      'Identity must not include main body nudge sentence',
    )
    assert.ok(firstText.length > secondText.length, 'Full body longer than identity')
  })
})
}
