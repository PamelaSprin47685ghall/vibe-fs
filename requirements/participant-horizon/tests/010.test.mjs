import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'

const ROOT = join(fileURLToPath(new URL('.', import.meta.url)), '../../..')
const read = (rel) => readFileSync(join(ROOT, rel), 'utf8')
const LOCALES = ['en', 'zh-CN']

test('WHAT[PARTICIPANT-HORIZON-010] PH_agent_009_fork_visible_set_is_exactly_the_five_forkable_offices', () => {
  const fiveOffices = [/Coder/i, /Scout|Investigator/i, /Technician|Operator/i, /Navigator|Researcher/i, /Analyst|Inquirer/i]
  for (const locale of LOCALES) {
    const fork = read(`resources/provider/tool/fork/description/${locale}.md`)
    for (const office of fiveOffices) {
      assert.match(fork, office, `fork/${locale}.md must present every forkable office`)
    }
    assert.doesNotMatch(fork, /\bReviewer\b/i, `fork/${locale}.md must not offer Reviewer`)
  }
})
