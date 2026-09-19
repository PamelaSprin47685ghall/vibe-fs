import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'

const ROOT = join(dirname(fileURLToPath(import.meta.url)), '../../..')

const readRole = (role, locale) => readFileSync(join(ROOT, 'resources/provider/role', role, locale), 'utf8')

test('WHAT[office-capability-011] manager_audit_pending_consequence_is_readonly_assessment_not_mutation', () => {
  const en = readRole('manager', 'en.md')
  const zh = readRole('manager', 'zh-CN.md')
  assert.match(en, /do not establish repository facts with your own hands/i)
  assert.match(zh, /不以自己的双手去建立 repository 事实/)
})
