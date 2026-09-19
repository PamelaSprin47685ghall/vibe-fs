import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'

const ROOT = join(fileURLToPath(new URL('.', import.meta.url)), '../../..')

const read = (rel) => readFileSync(join(ROOT, rel), 'utf8')

const LOCALES = ['en', 'zh-CN']

const HIDDEN_ORCHESTRATION = /\b(reviewer|witness|barrier|cohort|2N|confirmation rounds?)\b|见证|屏障|评审者/i

const INTERNAL_PARTICIPANTS = /\b(blogger|distiller|bookkeeper)\b/i

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

test('WHAT[participant-horizon-008] PH_glory_002_030_manager_surface_hides_review_orchestration', () => {
  for (const surface of MANAGER_VISIBLE_SURFACES) {
    for (const locale of LOCALES) {
      const text = read(`resources/provider/${surface}/${locale}.md`)
      assert.doesNotMatch(text, HIDDEN_ORCHESTRATION, `${surface}/${locale}.md leaks review orchestration`)
    }
  }
})
