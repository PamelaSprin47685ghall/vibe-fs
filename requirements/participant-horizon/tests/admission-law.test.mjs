// participant-horizon — positive information-admission law over provider-visible
// fixed surfaces (AGENT-008/009, EXEC-030, GLORY-002/030/032, SURFACE-005,
// PROMPT-013, HOST-018). The leak gates enforce the renderer side; these
// assertions pin the positive law: what IS allowed across the horizon and what
// must stay on the machine side.
//
// Resource paths are read relative to the repository root.

import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'

const ROOT = join(fileURLToPath(new URL('.', import.meta.url)), '../../..')
const read = (rel) => readFileSync(join(ROOT, rel), 'utf8')

const LOCALES = ['en', 'zh-CN']

/** Hidden review orchestration (GLORY-002/030, SURFACE-005, PROMPT-013, HOST-018). */
const HIDDEN_ORCHESTRATION = /\b(reviewer|witness|barrier|cohort|2N|confirmation rounds?)\b|见证|屏障|评审者/i

/** Internal participants must never reach a provider-visible surface (AGENT-008). */
const INTERNAL_PARTICIPANTS = /\b(blogger|distiller|bookkeeper)\b/i

/** Machine execution binding must not be provider-visible (EXEC-030). */
const MACHINE_BINDING = /\b(fast|deep)-[a-z]+/

const MANAGER_VISIBLE_SURFACES = [
  'role/manager',
  'tool/fork/description',
  'tool/commission/description',
  'tool/horizon/description',
  'tool/join/description',
  'tool/suicide/description',
  'lifecycle/magic-todo/todowrite-description',
  'lifecycle/magic-todo/manager-guideline',
]

test('PH_agent_008_internal_participants_absent_from_provider_visible_surfaces', () => {
  const surfaces = [
    'role/manager',
    'tool/fork/description',
    'tool/commission/description',
    'tool/horizon/description',
    'tool/join/description',
    'tool/suicide/description',
  ]
  for (const surface of surfaces) {
    for (const locale of LOCALES) {
      const text = read(`resources/provider/${surface}/${locale}.md`)
      assert.doesNotMatch(text, INTERNAL_PARTICIPANTS, `${surface}/${locale}.md leaks an internal participant`)
    }
  }
})

test('PH_agent_008_machine_binding_names_absent_from_provider_visible_surfaces', () => {
  for (const surface of MANAGER_VISIBLE_SURFACES) {
    for (const locale of LOCALES) {
      const text = read(`resources/provider/${surface}/${locale}.md`)
      assert.doesNotMatch(text, MACHINE_BINDING, `${surface}/${locale}.md leaks fast-/deep- binding`)
    }
  }
})

test('PH_glory_002_030_manager_surface_hides_review_orchestration', () => {
  for (const surface of MANAGER_VISIBLE_SURFACES) {
    for (const locale of LOCALES) {
      const text = read(`resources/provider/${surface}/${locale}.md`)
      assert.doesNotMatch(text, HIDDEN_ORCHESTRATION, `${surface}/${locale}.md leaks review orchestration`)
    }
  }
})

test('PH_agent_009_fork_visible_set_is_exactly_the_five_forkable_offices', () => {
  const fiveOffices = [/Coder/i, /Scout|Investigator/i, /Technician|Operator/i, /Navigator|Researcher/i, /Analyst|Inquirer/i]
  for (const locale of LOCALES) {
    const fork = read(`resources/provider/tool/fork/description/${locale}.md`)
    for (const office of fiveOffices) {
      assert.match(fork, office, `fork/${locale}.md must present every forkable office`)
    }
    assert.doesNotMatch(fork, /\bReviewer\b/i, `fork/${locale}.md must not offer Reviewer`)
  }
})

test('PH_exec_030_no_generic_state_dto_vocabulary_in_join_or_horizon_descriptions', () => {
  const dtoVocabulary = /\b(status|session_id|agent_id|pty_id|code|ordinal|kind|count)\b/i
  for (const tool of ['join', 'horizon']) {
    for (const locale of LOCALES) {
      const text = read(`resources/provider/tool/${tool}/description/${locale}.md`)
      assert.doesNotMatch(text, dtoVocabulary, `${tool}/${locale}.md carries state-machine DTO vocabulary`)
    }
  }
})

test('PH_exec_005_horizon_description_declares_pull_only_and_hides_machinery', () => {
  for (const locale of LOCALES) {
    const text = read(`resources/provider/tool/horizon/description/${locale}.md`)
    assert.match(text, /pull-only|只在调用时主动读取一次|不?轮询|do not poll/i)
    assert.match(text, /hidden machinery|隐藏机|hidden machinery|不 dump 隐藏/i)
  }
})
