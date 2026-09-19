import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const delivery = await import("../../../dist/Enforcer/Guidance/DeliverySurface.js");

const { empty, apply, applyReanchor, hasFullDelivered } = delivery
const TipPresentation = Object.freeze({ Full: 'Full', IdentityOnly: 'IdentityOnly' })

test('WHAT[guidance-delivery-005] TDP_004_reanchor_voids_full_history_so_next_resolve_refulls', () => {
  let state = apply('primitive-obsession', TipPresentation.Full, empty)
  assert.equal(hasFullDelivered('primitive-obsession', state), true)

  // HOST-006 compaction reanchor: coverage is horizon-relative and lost, so
  // Full history is voided — the next resolve must re-emit full main.md.
  state = applyReanchor(state)
  assert.equal(hasFullDelivered('primitive-obsession', state), false)

  // And the re-emission is a Full again (restoration), recorded on the fold.
  state = apply('primitive-obsession', TipPresentation.Full, state)
  assert.equal(hasFullDelivered('primitive-obsession', state), true)
})
test('WHAT[guidance-delivery-005] TDP_005_reanchor_does_not_advance_occurrence_frontier', () => {
  // applyReanchor only clears the horizon-relative Full history. The
  // occurrence-based frontier is a different axis (TipDeliveryProjection
  // tracks FullDeliveredTips per Main session; reanchor never mints a new
  // occurrence). This locks that re-Full after reanchor is restoration, not
  // a fresh first delivery.
  let state = apply('primitive-obsession', TipPresentation.Full, empty)
  const before = state
  state = applyReanchor(state)
  assert.notDeepEqual(state, before, 'reanchor must void Full history')
  // Re-applying Full after reanchor yields the same observable state as the
  // original first Full — no accumulation, no new semantic identity.
  const refilled = apply('primitive-obsession', TipPresentation.Full, state)
  assert.deepEqual(refilled, before)
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

test('WHAT[guidance-delivery-005] ENFORCER_TIP_DELIVERY_006_context_reanchor_clears_full_so_next_is_full_again', async () => {
  await withJournal(async (journal) => {
    await seedOwnerWithTip(journal)
    const first = await resolveTipGuidance(journal, main)
    assert.equal(presentationOf(first), 'Full')
    assert.equal(presentationOf(await resolveTipGuidance(journal, main)), 'IdentityOnly')

    // HOST-006: compaction reanchor voids FullDeliveredTips with Blog/Prefix.
    const reanchored = await guidance.appendContextReanchored(journal, {
      session: main,
      previousEpoch: 0,
      nextEpoch: 1,
      observedCompactionRun: 'run-compaction-tip',
    })
    assert.equal(reanchored.ok, true, reanchored.error)

    const after = await resolveTipGuidance(journal, main)
    assert.ok(after, 'post-reanchor must still resolve tip')
    assert.equal(presentationOf(after), 'Full', 'reanchor must re-emit Full main.md')
    assert.match(textOf(after), /tip = "primitive-obsession"/)
    assert.ok(
      textOf(after).includes('Introduce a distinct type so invalid substitutions become impossible.') ||
        textOf(after).includes(MAIN_MD),
      're-Full must include main body',
    )
  })
})
}
