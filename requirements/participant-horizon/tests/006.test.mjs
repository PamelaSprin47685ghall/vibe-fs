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
]

test('WHAT[participant-horizon-006] PH_exec_030_internal_machine_state_renders_as_consequence_not_dto', () => {
  const machineStateVocabulary = /\b(lane|offset|spool|job\s*id)\b/i
  for (const tool of ['join', 'horizon']) {
    for (const locale of LOCALES) {
      const text = read(`resources/provider/tool/${tool}/description/${locale}.md`)
      assert.doesNotMatch(text, machineStateVocabulary, `${tool}/${locale}.md carries internal machine state`)
    }
  }
  for (const locale of LOCALES) {
    const join = read(`resources/provider/tool/join/description/${locale}.md`)
    assert.match(join, /consequence|后果/i, `join/${locale}.md must frame outcomes as consequences`)
  }
})
