import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'

const ROOT = join(fileURLToPath(new URL('.', import.meta.url)), '../../..')
const read = (rel) => readFileSync(join(ROOT, rel), 'utf8')

const LOCALES = ['en', 'zh-CN']

const readTool = (tool, locale) => read(`resources/provider/tool/${tool}/description/${locale}.md`)

test('WHAT[ACTION-AFFORDANCE-009] AA_prompt_020_fork_calling_names_differ_in_persona_not_authority', () => {
  const fork = readTool('fork', 'en')
  assert.match(fork, /The two calling names belonging to one office differ in persona and reasoning depth,\nnot in the office's authority\./i)
})
