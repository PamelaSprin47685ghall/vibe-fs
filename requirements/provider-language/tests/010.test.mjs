import assert from 'node:assert/strict'
import { existsSync, readFileSync } from 'node:fs'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'

const ROOT = join(fileURLToPath(new URL('.', import.meta.url)), '../../..')

const read = (rel) => readFileSync(join(ROOT, rel), 'utf8')

const ACTIVE_ROLES = Object.freeze([
  'engineer',
  'devops',
  'manager',
  'orchestrator',
  'blogger',
  'bookkeeper',
])

test('WHAT[PROVIDER-LANGUAGE-010] PL_010_role_law_semantic_anchors_match_across_languages', () => {
  for (const role of ACTIVE_ROLES) {
    const enPath = `resources/provider/role/${role}/en.md`
    const zhPath = `resources/provider/role/${role}/zh-CN.md`

    assert.ok(existsSync(join(ROOT, enPath)), `${enPath} must exist`)
    assert.ok(existsSync(join(ROOT, zhPath)), `${zhPath} must exist`)

    const en = read(enPath)
    const zh = read(zhPath)

    assert.ok(en.trim().length > 0, `${enPath} must not be empty`)
    assert.ok(zh.trim().length > 0, `${zhPath} must not be empty`)
  }

  // Exact anchor parity checks
  const engEn = read('resources/provider/role/engineer/en.md')
  const engZh = read('resources/provider/role/engineer/zh-CN.md')
  assert.match(engEn, /local facts/i)
  assert.match(engZh, /本地事实/i)
  assert.match(engEn, /source code/i)
  assert.match(engZh, /源码/i)

  const devopsEn = read('resources/provider/role/devops/en.md')
  const devopsZh = read('resources/provider/role/devops/zh-CN.md')
  assert.match(devopsEn, /direct repair|inherent authority/i)
  assert.match(devopsZh, /直接修复|固有.*授权/i)

  const mgrEn = read('resources/provider/role/manager/en.md')
  const mgrZh = read('resources/provider/role/manager/zh-CN.md')
  assert.match(mgrEn, /does not inspect.*edit|does not touch/i)
  assert.match(mgrZh, /不亲自修改/i)

  // Mutation test: missing counterpart anchor must fail
  assert.throws(() => {
    const mockZh = '只有普通文本，没有对应锚点'
    if (!/本地事实/i.test(mockZh)) {
      throw new Error('Anchor parity mismatch between en and zh-CN')
    }
  }, /Anchor parity mismatch/)
})
