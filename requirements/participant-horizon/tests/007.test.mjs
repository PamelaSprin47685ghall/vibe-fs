import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'

const ROOT = join(fileURLToPath(new URL('.', import.meta.url)), '../../..')
const read = (rel) => readFileSync(join(ROOT, rel), 'utf8')
const LOCALES = ['en', 'zh-CN']
const INTERNAL_PARTICIPANTS = /\b(blogger|distiller|bookkeeper)\b/i

test('WHAT[PARTICIPANT-HORIZON-007] PH_agent_008_internal_participants_absent_from_provider_visible_surfaces', () => {
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
