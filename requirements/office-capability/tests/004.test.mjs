import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'

const ROOT = join(dirname(fileURLToPath(import.meta.url)), '../../..')

const readRole = (role, locale) => readFileSync(join(ROOT, 'resources/provider/role', role, locale), 'utf8')

test('WHAT[office-capability-004] capability_is_consequence_model_not_tool_whitelist_transcription', () => {
  const en = readRole('manager', 'en.md')
  const zh = readRole('manager', 'zh-CN.md')
  assert.match(en, /Know another office by its promises, not by its keys/i)
  assert.match(en, /not by the instruments hidden[\s\S]{0,20}inside it/i)
  assert.match(zh, /应看它的承诺，而不是它的钥匙/)
  assert.match(zh, /而不是看它内部隐藏着什么工具/)
})
