import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'

const ROOT = join(fileURLToPath(new URL('.', import.meta.url)), '../../..')
const read = (rel) => readFileSync(join(ROOT, rel), 'utf8')

const LOCALES = ['en', 'zh-CN']

const readTool = (tool, locale) => read(`resources/provider/tool/${tool}/description/${locale}.md`)

test('WHAT[ACTION-AFFORDANCE-011] AA_prompt_021_callers_see_the_boundary_mirror_not_just_callee_role_law', () => {
  const inspect = readTool('inspect', 'en')
  assert.match(inspect, /read-only in the causal sense/i, 'caller-facing description must mirror the causal boundary')
  assert.match(inspect, /Do not use inspect to ask for code changes/i, 'caller must be told the forbidden request shape')
  const establish = readTool('establish-behavior', 'en')
  assert.match(establish, /Coder completion is not execution evidence|does not run those tests/i)
})
