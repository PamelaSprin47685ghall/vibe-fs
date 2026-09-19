import assert from 'node:assert/strict'
import test from 'node:test'
import { mkdtempSync, rmSync, readFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join, dirname } from 'node:path'
import { fileURLToPath } from 'node:url'
import * as guidance from '../../../dist/Enforcer/Guidance/TipSurface.js'
import * as language from '../../../dist/Participant/Provider/LanguageSurface.js'
import * as resources from '../../../dist/Resources/PromptSurface.js'

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

test('WHAT[guidance-delivery-002] ENFORCER_TIP_DELIVERY_001_first_resolve_is_full_main_md', async () => {
  await withJournal(async (journal) => {
    await seedOwnerWithTip(journal)
    const guidance = await resolveTipGuidance(journal, main)
    assert.ok(guidance, 'expected tip guidance')
    assert.equal(presentationOf(guidance), 'Full')
    const text = textOf(guidance)
    assert.match(text, /tip = "primitive-obsession"/)
    assert.ok(
      text.includes(MAIN_MD) || text.includes('Introduce a distinct type so invalid substitutions become impossible.'),
      'Full guidance must include main.md body',
    )
    assert.ok(text.length > 80, `Full text too short: ${text.length}`)
  })
})

test('WHAT[guidance-delivery-002] ENFORCER_PROMPT_017_full_tip_guidance_uses_owner_session_zh_cn_rulebook', async () => {
  language.clearAllForTests()
  try {
    const bound = language.bindOnce(main, 'SimplifiedChinese')
    assert.equal(bound.ok, true)
    await withJournal(async (journal) => {
      await seedOwnerWithTip(journal, { runSuffix: '7' })
      const guidance = await resolveTipGuidance(journal, main)
      assert.equal(presentationOf(guidance), 'Full')
      const text = textOf(guidance)
      assert.match(text, /# Enforcer Tip（规则提示）/)
      assert.ok(text.includes(MAIN_ZH_CN_MD), 'Full zh-CN guidance must include main.zh-CN.md body')
      assert.match(text, /[\u3400-\u9fff]/)
      assert.ok(!text.includes(MAIN_MD), 'zh-CN guidance must not silently fall back to English main.md')
    })
  } finally {
    language.clearAllForTests()
  }
})
