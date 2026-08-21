import assert from 'node:assert/strict'
import test from 'node:test'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import * as guidance from '../../../dist/Enforcer/Guidance/TipSurface.js'
import * as pair from '../../../dist/OpenCode/Host/PairProgrammingThoughtSurface.js'
import * as resources from '../../../dist/Resources/PromptSurface.js'
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
      bloggerAgent: 'fast-blogger',
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

test('WHAT[GD-004] ENFORCER_TIP_NUDGE_001_latest_tip_first_delivery_is_full_main_md', async () => {
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

test('WHAT[GD-007] ENFORCER_TIP_NUDGE_001b_latestTipNudge_is_same_bytes_as_latestTipGuidance', async () => {
  const fixture = await seed()
  try {
    // 先交付一次 Full（推进 durable Frontier），之后 latest 稳定为 Identity 文本。
    await latestTipNudge(fixture.journal, blogger)
    // GD-007: latestTipNudge 是 latestTipGuidance 的同字节别名（同一时刻
    // 两者返回同一字节——Full/Identity 文本，不是旧 Nudge 字段）。
    const viaNudge = await latestTipNudge(fixture.journal, blogger)
    const viaGuidance = await latestTipGuidance(fixture.journal, blogger)
    assert.equal(viaGuidance, viaNudge)
  } finally {
    fixture.dispose()
  }
})

test('WHAT[GD-006] ENFORCER_TIP_NUDGE_002_missing_recent_tip_returns_none', async () => {
  const fixture = await seed({ withTip: false })
  try {
    assert.equal(await latestTipNudge(fixture.journal, blogger), null)
  } finally {
    fixture.dispose()
  }
})

test('WHAT[GD-006] ENFORCER_TIP_NUDGE_003_missing_owner_returns_none', async () => {
  const fixture = await seed({ withAssociation: false })
  try {
    assert.equal(await latestTipNudge(fixture.journal, blogger), null)
  } finally {
    fixture.dispose()
  }
})

const guideline = '# Pair programming auto-injected'
const anchor = [{ info: { id: 'user-1', role: 'user' }, parts: [{ type: 'text', text: 'task' }] }]
const markerOutput = (messages) => {
  // pair sits before trailing user: completed synthetic skill, user
  const result = messages.find((m) => m?.parts?.[0]?.tool === 'skill' && m?.parts?.[0]?.state?.status === 'completed')
  return result?.parts?.[0]?.state?.output
}

test('WHAT[GD-009] CTX_002_GUIDELINE_001_marker_without_nudge_is_guideline_text', async () => {
  const marker = pair.text
  const result = await tryInject(undefined, marker, anchor)
  assert.equal(result.ok, true, result.error)
  assert.equal(markerOutput(result.value), marker)
  assert.match(marker, /^# /)
})

test('WHAT[GD-009] CTX_002_GUIDELINE_002_marker_with_nudge_is_one_instruction_plane', async () => {
  const nudge = 'A domain concept is crossing a boundary as a primitive. Introduce a distinct type so invalid substitutions become impossible.'
  const marker = `# ${nudge}\n${pair.text}`
  const result = await tryInject(undefined, marker, [
    { info: { id: 'user-2', role: 'user' }, parts: [{ type: 'text', text: 'task' }] },
  ])
  assert.equal(result.ok, true, result.error)
  assert.equal(markerOutput(result.value), marker)
  assert.ok(marker.indexOf('# A domain concept') < marker.indexOf(pair.text.trim()))
})
