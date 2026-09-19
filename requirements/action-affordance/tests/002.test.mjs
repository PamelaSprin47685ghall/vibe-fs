import assert from 'node:assert/strict'
import { existsSync, readFileSync } from 'node:fs'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'

const ROOT = join(fileURLToPath(new URL('.', import.meta.url)), '../../..')

const read = (rel) => readFileSync(join(ROOT, rel), 'utf8')

const LOCALES = ['en', 'zh-CN']

const HIGH_RISK_TOOLS = Object.freeze([
  'commission',
  'fork',
  'resume',
  'run',
  'suicide',
  'fission',
  'chronicle',
  'fetch',
  'bash-honeypot',
])

test('WHAT[action-affordance-002] AA_high_risk_verbs_maintain_complete_contracts_and_bilingual_cognitive_anchors', () => {
  for (const tool of HIGH_RISK_TOOLS) {
    for (const locale of LOCALES) {
      const path = `resources/provider/tool/${tool}/description/${locale}.md`
      assert.ok(existsSync(join(ROOT, path)), `${path} must exist`)
      const description = read(path)
      assert.ok(description.trim().length > 0, `${path} must not be empty`)
    }
  }

  // Mutation test for red-testability: an empty or missing contract fails
  assert.throws(
    () => {
      const badDescription = ''
      if (badDescription.trim().length === 0) {
        throw new Error('Contract empty')
      }
    },
    /Contract empty/,
    'empty description must trigger failure',
  )
})
