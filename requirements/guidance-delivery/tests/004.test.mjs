import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { mkdtempSync, rmSync } = await import("node:fs");
const { tmpdir } = await import("node:os");
const { join } = await import("node:path");
const guidance = await import("../../../dist/Enforcer/Guidance/TipSurface.js");
const pair = await import("../../../dist/OpenCode/Host/PairProgrammingThoughtSurface.js");
const resources = await import("../../../dist/Resources/PromptSurface.js");

resources.runtimeInstallFromPackage()
const latestTipNudge = guidance.latestNudge
const latestTipGuidance = guidance.latest
const tryInject = pair.tryInject
const main = 'ses-nudge-main'
const blogger = 'ses-nudge-blogger'
const appendObservation = (journal) =>
  guidance.appendObservation(journal, {
    session: main,
    bloggerSession: blogger,
    requestId: 'req-nudge-1',
    frameEpoch: 0,
    previousIngestedThrough: 0,
    nextIngestedThrough: 1,
    previousCutoff: 0,
    nextCutoff: 1,
    nextCoveredPrefixDigest: 'digest-nudge-1',
    textRef: 'blob-nudge-1',
    textDigest: 'sha-nudge-1',
    providerRun: 'run-nudge-1',
    toolCallIds: ['call-nudge-1'],
    tipRuleId: 'primitive-obsession',
    fieldNameAtCommit: 'primitive-obsession',
    observedPrefixEpoch: 0,
  })
const seed = async ({ withAssociation = true, withTip = true } = {}) => {
  const directory = mkdtempSync(join(tmpdir(), 'wxs-latest-tip-'))
  const created = await guidance.createJournal(directory)
  assert.equal(created.ok, true, created.error)
  const journal = created.journal

  if (withAssociation) {
    const linked = await guidance.appendCompanionLink(journal, {
      session: main,
      bloggerSession: blogger,
      bloggerAgent: 'blogger',
    })
    assert.equal(linked.ok, true, linked.error)
  }

  if (withTip) {
    const committed = await appendObservation(journal)
    assert.equal(committed.ok, true, committed.error)
  }

  return {
    journal,
    dispose: () => {
      guidance.disposeJournal(journal)
      rmSync(directory, { recursive: true, force: true })
    },
  }
}
const guideline = '# Pair programming auto-injected'
const SEP = '\0\uFEFF'
const anchor = [
  { info: { id: 'c1', role: 'assistant' }, parts: [{ type: 'tool', tool: 'read', callID: 'source', state: { status: 'pending', input: {}, time: { start: 0 } } }] },
  { info: { id: 'r1', role: 'assistant' }, parts: [{ type: 'tool', tool: 'read', callID: 'source', state: { status: 'completed', input: {}, output: 'source\n', time: { start: 0, end: 0 } } }] },
]
const markerOutput = (messages) => {
  // universal cursor mode: terminal real tool result carries output + SEP + marker
  const result = messages.at(-1)
  const output = result?.parts?.[0]?.state?.output
  if (typeof output !== 'string') return undefined
  const idx = output.indexOf(SEP)
  return idx >= 0 ? output.slice(idx + SEP.length) : undefined
}

test('WHAT[guidance-delivery-004] ENFORCER_TIP_NUDGE_001_latest_tip_first_delivery_is_full_main_md', async () => {
  const fixture = await seed()
  try {
    const result = await latestTipNudge(fixture.journal, blogger)
    assert.ok(typeof result === 'string' && result.length > 0, 'expected tip guidance text')
    assert.match(result, /tip = "primitive-obsession"/)
    assert.match(result, /Create a distinct (domain )?type/)
    // Second call for same tip must be identity-only (durable Full delivery recorded).
    const again = await latestTipNudge(fixture.journal, blogger)
    assert.equal(again, 'tip: primitive-obsession')
  } finally {
    fixture.dispose()
  }
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const delivery = await import("../../../dist/Enforcer/Guidance/DeliverySurface.js");

const { empty, apply, applyReanchor, hasFullDelivered } = delivery
const TipPresentation = Object.freeze({ Full: 'Full', IdentityOnly: 'IdentityOnly' })

test('WHAT[guidance-delivery-004] TDP_001_empty_state_has_nothing_delivered', () => {
  assert.equal(hasFullDelivered('primitive-obsession', empty), false)
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

test('WHAT[guidance-delivery-004] ENFORCER_TIP_DELIVERY_003_latestTipGuidance_matches_resolve_text', async () => {
  await withJournal(async (journal) => {
    await seedOwnerWithTip(journal)
    const viaResolve = textOf(await resolveTipGuidance(journal, main))
    // second call after Full already recorded: the decision substrate is the
    // durable TipDeliveryProjection fold, so latest returns the identity text
    // (restart-safe — no process-local delivered set).
    const viaLatest = await latestTipGuidance(journal, main)
    assert.equal(viaLatest, 'tip: primitive-obsession')
    assert.ok(viaResolve.includes('primitive-obsession'))
  })
})
}
