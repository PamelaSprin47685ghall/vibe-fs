import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { readFileSync } = await import("node:fs");
const { dirname, join } = await import("node:path");
const { fileURLToPath } = await import("node:url");
const { default: test } = await import("node:test");
const { isAllowed, managerForkableOffices, permissions } = await import("../../../dist/Participant/Persona/OfficeCapabilitySurface.js");
const { assertJsData } = await import("../../verification-system/tests/support/js-contract.mjs");

const ROOT = join(dirname(fileURLToPath(import.meta.url)), '../../..')
const PROVIDER = join(ROOT, 'resources/provider')
const read = (rel) => readFileSync(join(PROVIDER, rel), 'utf8')
const ACTIVE_OFFICES = [
  {
    id: 'engineer-investigation-mutation',
    managerEn: /entrust.*Engineer/i,
    managerZh: /托付.*Engineer/,
    forkEn: /Engineer[\s\S]{0,160}local facts[\s\S]{0,80}source/i,
    forkZh: /Engineer[\s\S]{0,120}本地事实[\s\S]{0,80}源码/,
    lawEn: /local facts|changing the written world|implement.*refactor/i,
    lawZh: /本地事实|书写出来的世界|源码/,
  },
]

test('WHAT[OFF-007] OFF_007_manager_forkable_offices_is_strictly_engineer', () => {
  assert.deepEqual(managerForkableOffices(), ['Engineer'])
})
}

{
const { default: assert } = await import("node:assert/strict");
const { readFileSync } = await import("node:fs");
const { dirname, join } = await import("node:path");
const { fileURLToPath } = await import("node:url");
const { default: test } = await import("node:test");

const ROOT = join(dirname(fileURLToPath(import.meta.url)), '../../..')
const readRole = (role, locale) => readFileSync(join(ROOT, 'resources/provider/role', role, locale), 'utf8')

test('WHAT[OFF-007] manager_has_no_personal_repository_witness_and_no_fission', () => {
  const en = readRole('manager', 'en.md')
  const zh = readRole('manager', 'zh-CN.md')
  assert.match(en, /do not establish repository facts with your own hands/i)
  assert.match(zh, /不以自己的双手去建立 repository 事实/)
  assert.match(en, /cannot (?:use )?Fission/i, 'Manager must explicitly deny fission')
  assert.match(zh, /不能(?:使用)?\s*Fission/, 'Manager must explicitly deny fission')
})
}
