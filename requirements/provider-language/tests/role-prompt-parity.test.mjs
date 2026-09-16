import assert from 'node:assert/strict'
import { existsSync, readFileSync } from 'node:fs'
import { join } from 'node:path'
import test from 'node:test'

const ROOT = new URL('../../..', import.meta.url).pathname
const readIfExists = (rel) => {
  const full = join(ROOT, rel)
  if (!existsSync(full)) return null
  return readFileSync(full, 'utf8')
}

test('WHAT[PROVIDER-LANGUAGE-012] bilingual prompts maintain parity on fission exclusivity and forbid manager/devops fission', () => {
  // 1. Engineer prompt existence and exclusivity
  const engZh = readIfExists('resources/provider/role/engineer/zh-CN.md')
  const engEn = readIfExists('resources/provider/role/engineer/en.md')
  assert.ok(engZh, 'resources/provider/role/engineer/zh-CN.md must exist')
  assert.ok(engEn, 'resources/provider/role/engineer/en.md must exist')

  assert.match(engZh, /唯一允许使用 Fission|唯一具备 Fission/i, 'Engineer zh prompt must assert fission exclusivity')
  assert.match(engEn, /only role permitted to use Fission|sole role authorized for Fission/i, 'Engineer en prompt must assert fission exclusivity')

  // 2. Manager prompt: forbids manager fission and management clones
  const mgrZh = readIfExists('resources/provider/role/manager/zh-CN.md')
  const mgrEn = readIfExists('resources/provider/role/manager/en.md')
  assert.ok(mgrZh, 'resources/provider/role/manager/zh-CN.md must exist')
  assert.ok(mgrEn, 'resources/provider/role/manager/en.md must exist')
  assert.match(mgrZh, /不能(?:使用)?\s*Fission/, 'Manager zh prompt must explicitly forbid fission')
  assert.match(mgrEn, /cannot (?:use )?Fission/i, 'Manager en prompt must explicitly forbid fission')
  assert.doesNotMatch(mgrZh, /Manager 可分身|管理分身/i, 'Manager zh prompt must not contain manager fission claims')

  // 3. DevOps prompt: direct repair authorization and forbids fission
  const devopsZh = readIfExists('resources/provider/role/devops/zh-CN.md')
  const devopsEn = readIfExists('resources/provider/role/devops/en.md')
  assert.ok(devopsZh, 'resources/provider/role/devops/zh-CN.md must exist')
  assert.ok(devopsEn, 'resources/provider/role/devops/en.md must exist')

  assert.match(devopsZh, /直接修复|自行调查/i, 'DevOps zh prompt must include direct repair authorization')
  assert.match(devopsZh, /不能 Fission|禁止 Fission/i, 'DevOps zh prompt must forbid fission')
  assert.match(devopsEn, /cannot Fission|must not Fission/i, 'DevOps en prompt must forbid fission')
  assert.doesNotMatch(devopsZh, /托付给 Coder|交由 Coder/i, 'DevOps zh prompt must not delegate repairs to Coder')
})
