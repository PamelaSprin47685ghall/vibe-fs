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

test('WHAT[participant-horizon-010] PH_agent_009_fork_visible_set_is_strictly_engineer', () => {
  for (const locale of LOCALES) {
    const fork = read(`resources/provider/tool/fork/description/${locale}.md`)
    assert.match(fork, /Engineer/i, `fork/${locale}.md must present Engineer`)
    assert.doesNotMatch(fork, /\bReviewer\b/i, `fork/${locale}.md must not offer Reviewer`)
    assert.doesNotMatch(fork, /\bCoder\b/i, `fork/${locale}.md must not offer Coder`)
    assert.doesNotMatch(fork, /\bInspector\b/i, `fork/${locale}.md must not offer Inspector`)
  }
})
