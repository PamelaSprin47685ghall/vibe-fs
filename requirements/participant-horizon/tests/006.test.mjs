import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'

const ROOT = join(fileURLToPath(new URL('.', import.meta.url)), '../../..')
const read = (rel) => readFileSync(join(ROOT, rel), 'utf8')
const LOCALES = ['en', 'zh-CN']

test('WHAT[PARTICIPANT-HORIZON-006] PH_exec_030_internal_machine_state_renders_as_consequence_not_dto', () => {
  const machineStateVocabulary = /\b(lane|offset|spool|job\s*id)\b/i
  for (const tool of ['join', 'horizon']) {
    for (const locale of LOCALES) {
      const text = read(`resources/provider/tool/${tool}/description/${locale}.md`)
      assert.doesNotMatch(text, machineStateVocabulary, `${tool}/${locale}.md carries internal machine state`)
    }
  }
  for (const locale of LOCALES) {
    const joinText = read(`resources/provider/tool/join/description/${locale}.md`)
    assert.match(joinText, /consequence|后果/i, `join/${locale}.md must frame outcomes as consequences`)
  }
})
