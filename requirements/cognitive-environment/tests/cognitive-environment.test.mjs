// cognitive-environment — enduring World / Role / inherited-knowledge layers.
//
// What this file proves:
//   1. Prompt Composition Protocol (PROMPT-015): one system per role, composed
//      Common Law → Role Law → Office Library in canonical order.
//   2. Role Law is the enduring self-model layer; one Role Law per office (no
//      fast/deep split, no tool inventory, no hidden orchestration).
//   3. Office Library = inherited craft; knowledge ≠ authority (PROMPT-016).
//   4. Pair Hint craft payload: NEEDHELP is normal collaboration, parallel wave
//      default, no scarcity language, no machine identity (AGENT-031, HOST-013).
//   5. Role Law cognition anchors are present in both locales (ROLE_SEMANTIC_ANCHORS).

import assert from 'node:assert/strict'
import { readFileSync, readdirSync } from 'node:fs'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'

import { promptResources } from '../../verification-system/tests/support/domain.mjs'
import { ROLE_SEMANTIC_ANCHORS } from '../../../scripts/checks/semantic-anchors.mjs'

const ROOT = join(fileURLToPath(new URL('.', import.meta.url)), '../../..')
const read = (rel) => readFileSync(join(ROOT, rel), 'utf8')

/** Collect every markdown resource under a provider-relative directory (en + zh-CN). */
const walkMarkdown = (relDir) => {
  const out = []
  const walkDir = (dir) => {
    for (const entry of readdirSync(dir, { withFileTypes: true })) {
      const p = join(dir, entry.name)
      if (entry.isDirectory()) walkDir(p)
      else if (entry.name.endsWith('.md')) out.push(readFileSync(p, 'utf8'))
    }
  }
  walkDir(join(ROOT, relDir))
  return out
}

/** Roles whose Role Law layer belongs to cognitive-environment. */
const ROLE_LAW_ROLES = Object.freeze([
  'manager',
  'coder',
  'inspector',
  'devops',
  'orchestrator',
  'blogger',
  'distiller',
  'bookkeeper',
])

/**
 * Manager consequence-choice anchors mirror office-capability's canonical five-office
 * model (ARCH-017 projection "Manager Role Law: worldview — choose office by consequence").
 * The Role Law layer still belongs to cognitive-environment; the mirrored fact stays with
 * office-capability. This test skips them to keep one semantic assertion per owner.
 */
const MIRRORED_BY_OFFICE_CAPABILITY = new Set(['entrust-by-consequence', 'choose-by-return', 'no-omnipotent-charge'])

const LANGUAGE = 'English'

test('WHAT[COGNITIVE-ENVIRONMENT-001] CE_prompt_015_one_system_prompt_per_role', () => {
  const catalog = promptResources.loadForLanguage(LANGUAGE)
  const prompts = Object.values(catalog)
  assert.equal(prompts.length, 10, 'exactly one canonical system prompt per public office')
})

test('WHAT[COGNITIVE-ENVIRONMENT-005] CE_prompt_015_no_tier_split_duplicates', () => {
  const catalog = promptResources.loadForLanguage(LANGUAGE)
  const prompts = Object.values(catalog)
  const unique = new Set(prompts)
  assert.equal(unique.size, 10, 'no two offices share a prompt; no tier-split duplicates')
})

test('WHAT[COGNITIVE-ENVIRONMENT-003] CE_prompt_015_canonical_composition_common_law_role_law_office_library', () => {
  const catalog = promptResources.loadForLanguage(LANGUAGE)
  const coder = catalog.CoderSystemPrompt
  assert.match(coder, /You awaken in a world already in motion/, 'Common Law must lead')
  assert.match(coder, /written world/i, 'Role Law must follow')
  assert.match(coder, /one more inheritance/, 'Office Library ingress must be present for book-owning offices')
  assert.match(coder, /The Kolmogorov Book/, 'inherited volume must be composed')
  assert.match(coder, /These books are older than this assignment/, 'Office Library closing must close the composition')

  const manager = catalog.ManagerSystemPrompt
  assert.match(manager, /The Book of Scarcity/, 'Manager inherits scarcity volume')
  assert.doesNotMatch(coder, /The Book of Scarcity/, 'Coder does not inherit the Manager-only volume')
})

test('WHAT[COGNITIVE-ENVIRONMENT-004] CE_prompt_015_system_prompt_does_not_enumerate_runtime_tool_surface', () => {
  const catalog = promptResources.loadForLanguage(LANGUAGE)
  for (const prompt of Object.values(catalog)) {
    assert.doesNotMatch(prompt, /\b(fast|deep)-[a-z]+/, 'machine binding names must not appear in system prompts')
    assert.doesNotMatch(prompt, /auto-injected|ToolPermission/, 'runtime tool-surface machinery must not enter Role Law')
  }
})

test('WHAT[COGNITIVE-ENVIRONMENT-006] CE_prompt_016_library_ingress_books_do_not_enlarge_authority', () => {
  for (const locale of ['en', 'zh-CN']) {
    const text = read(`resources/provider/library/ingress/${locale}.md`)
    assert.match(text, /do not enlarge your authority|不扩大|不会扩大|不?扩大你的权/i)
    assert.match(text, /do not override the Common Law|不会覆盖 Common Law|Common Law/i)
  }
})

test('WHAT[COGNITIVE-ENVIRONMENT-007] CE_prompt_016_library_ingress_teaches_craft_within_existing_authority', () => {
  for (const locale of ['en', 'zh-CN']) {
    const text = read(`resources/provider/library/ingress/${locale}.md`)
    assert.match(text, /teach the craft|craft|技艺|手艺/i)
    assert.doesNotMatch(text, /grant.{0,40}authority|授予.*authority/i)
  }
})

test('WHAT[COGNITIVE-ENVIRONMENT-008] CE_prompt_016_office_library_closing_books_older_than_assignment', () => {
  for (const locale of ['en', 'zh-CN']) {
    const text = read(`resources/provider/library/closing/${locale}.md`)
    assert.match(text, /older than this assignment|比.*assignment|比.*更老|旧于/i)
  }
})

test('WHAT[COGNITIVE-ENVIRONMENT-009] CE_prompt_016_office_library_closing_work_not_forced_to_resemble_book', () => {
  for (const locale of ['en', 'zh-CN']) {
    const text = read(`resources/provider/library/closing/${locale}.md`)
    assert.match(text, /do not force the work|不?要强|不要.*模仿|Don't force/i)
  }
})

test('WHAT[COGNITIVE-ENVIRONMENT-008] CE_role_law_cognition_anchors_present_in_both_locales', () => {
  for (const role of ROLE_LAW_ROLES) {
    const anchors = ROLE_SEMANTIC_ANCHORS[role]
    assert.ok(anchors, `catalog missing role: ${role}`)
    const en = read(`resources/provider/role/${role}/en.md`)
    const zh = read(`resources/provider/role/${role}/zh-CN.md`)
    for (const { id, en: enRe, zh: zhRe } of anchors) {
      if (MIRRORED_BY_OFFICE_CAPABILITY.has(id)) continue
      assert.match(en, enRe, `${role}/en.md missing anchor ${id}`)
      assert.match(zh, zhRe, `${role}/zh-CN.md missing anchor ${id}`)
    }
  }
})

test('WHAT[COGNITIVE-ENVIRONMENT-005] CE_role_law_is_enduring_self_model_without_tier_split_or_hidden_orchestration', () => {
  for (const role of ['coder', 'manager', 'devops', 'inspector', 'orchestrator', 'blogger', 'distiller']) {
    for (const locale of ['en', 'zh-CN']) {
      const text = read(`resources/provider/role/${role}/${locale}.md`)
      assert.doesNotMatch(text, /\b(fast|deep)-[a-z]+/, `${role}/${locale}.md must not expose tier identity`)
      assert.doesNotMatch(
        text,
        /\breviewer\b|\bbarrier\b|\b2N\b|\bcohort\b|confirmation rounds/i,
        `${role}/${locale}.md must not teach hidden review orchestration`,
      )
    }
  }
})

test('WHAT[COGNITIVE-ENVIRONMENT-013] CE_agent_031_pair_hint_teaches_needhelp_as_normal_collaboration', () => {
  for (const locale of ['en', 'zh-CN']) {
    const text = read(`resources/provider/host/pair-programming-guideline/${locale}.md`)
    assert.match(text, /\[NEEDHELP\]/)
    assert.match(text, /not failure|不是失败|正常协作|normal collaboration|normal/i)
    assert.doesNotMatch(text, /only (?:when|if) truly blocked|只在确实卡住|普通情况下不要使用/i)
  }
})

test('WHAT[COGNITIVE-ENVIRONMENT-013] CE_pair_hint_teaches_parallel_wave_without_global_concurrency_number', () => {
  for (const locale of ['en', 'zh-CN']) {
    const text = read(`resources/provider/host/pair-programming-guideline/${locale}.md`)
    assert.match(text, /parallel|并行/i)
    assert.match(text, /dependenc|依赖/i)
    assert.doesNotMatch(text, /最多\s*\d+|max(?:imum)?\s+\d+/i)
  }
})

test('WHAT[COGNITIVE-ENVIRONMENT-010] CE_010_lifecycle_texts_orient_without_educating_or_replacing_system_prompt', () => {
  const transientTexts = [...walkMarkdown('resources/provider/lifecycle'), ...walkMarkdown('resources/provider/runtime')]
  assert.ok(transientTexts.length > 0, 'lifecycle and runtime provider texts must exist')
  for (const text of transientTexts) {
    assert.doesNotMatch(text, /educate|teach|教学|培训|lesson/i, 'lifecycle texts orient, they do not educate')
    assert.doesNotMatch(text, /system prompt|system-prompt|系统提示词/i, 'no lifecycle text triggers a system prompt replacement')
    assert.doesNotMatch(text, /envelope|第二套/i, 'no second envelope is stacked onto the canonical prompt')
  }
})

test('WHAT[COGNITIVE-ENVIRONMENT-012] CE_012_reviewer_prompt_carries_role_law_and_ledger_without_process_mechanics', () => {
  for (const locale of ['en', 'zh-CN']) {
    const text = read(`resources/provider/role/reviewer/${locale}.md`)
    assert.match(text, /Examiner|Ledger|判断|judge/i, 'Reviewer prompt = Role Law + Examiner Ledger composition')
    assert.doesNotMatch(
      text,
      /\bbarrier\b|\b2N\b|\bwitness\b|\bcohort\b|confirmation rounds|dedicated session|双 PERFECT|双完美/i,
      `${locale}: hidden PERFECT-process mechanics must not enter the Reviewer prompt`,
    )
  }
})

test('WHAT[COGNITIVE-ENVIRONMENT-011] CE_011_transient_texts_do_not_rewrite_role_self_model', () => {
  const transientTexts = [...walkMarkdown('resources/provider/lifecycle'), ...walkMarkdown('resources/provider/runtime')]
  assert.ok(transientTexts.length > 0, 'lifecycle and runtime provider texts must exist')
  for (const text of transientTexts) {
    assert.doesNotMatch(text, /identity|persona|self-model|你是谁|身份/i, 'the office identity is decided by Role Law, not by the current phase')
    assert.doesNotMatch(text, /\b(fast|deep)-[a-z]+/, 'transient texts never expose fast/deep machine identity')
  }
})
