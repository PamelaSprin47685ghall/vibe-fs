import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'

const ROOT = join(dirname(fileURLToPath(import.meta.url)), '../../..')
const readRole = (role, locale) => readFileSync(join(ROOT, 'resources/provider/role', role, locale), 'utf8')

test('WHAT[OFF-013] browser_consequence_is_external_facts_with_provenance_not_local_repo', () => {
  const en = readRole('browser', 'en.md')
  const zh = readRole('browser', 'zh-CN.md')
  assert.match(en, /establish facts from the Internet and[\s\S]{0,60}other external web sources/i)
  assert.match(en, /Do not inspect the local repository/i)
  assert.match(zh, /从 Internet 与其他外部 web sources 建立事实/)
  assert.match(zh, /本地 repository，就去检查/)
})
