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

test('WHAT[office-capability-016] OFF_016_engineer_is_the_only_office_entitled_to_fission', () => {
  assert.equal(isAllowed('engineer', 'Fission'), true)
  assert.equal(isAllowed('manager', 'Fission'), false)
  assert.equal(isAllowed('orchestrator', 'Fission'), false)
  assert.equal(isAllowed('devops', 'Fission'), false)
  assert.equal(isAllowed('blogger', 'Fission'), false)
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

test('WHAT[office-capability-016] engineer_role_law_carries_investigation_mutation_and_exclusive_fission', () => {
  const en = readRole('engineer', 'en.md')
  const zh = readRole('engineer', 'zh-CN.md')
  assert.match(en, /local facts|read, create, modify, move, delete/i)
  assert.match(en, /only.*fission/i)
  assert.match(en, /do not execute real commands|do not.*devops|not execute real commands/i)
  assert.match(zh, /本地事实调查.*源码/i)
  assert.match(zh, /唯一允许使用 Fission|唯一具备 Fission/i)
  assert.match(zh, /不执行真实命令，即使是只读命令也不例外/)
  assert.match(zh, /不要借[\s\S]{0,80}DevOps[\s\S]{0,30}绕过/)
})
}
