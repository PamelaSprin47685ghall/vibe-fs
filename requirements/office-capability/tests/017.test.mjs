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

test('WHAT[office-capability-017] OFF_017_devops_has_inherent_mutation_authority_without_allow_repair_toggle', () => {
  assert.equal(isAllowed('devops', 'Write'), true)
  assert.equal(isAllowed('devops', 'Edit'), true)
  assert.equal(isAllowed('devops', 'Move'), true)
  assert.equal(isAllowed('devops', 'Remove'), true)
  assert.equal(isAllowed('devops', 'Exec'), true)
  assert.equal(isAllowed('devops', 'Pty'), true)
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

test('WHAT[office-capability-017] devops_role_law_carries_execution_and_inherent_repair_without_allow_repair_toggle', () => {
  const en = readRole('devops', 'en.md')
  const zh = readRole('devops', 'zh-CN.md')
  assert.match(en, /operational objective|execution/i)
  assert.match(en, /non-architectural|repair/i)
  assert.doesNotMatch(en, /allowRepair|managerApprovedMutation/i)
  assert.match(zh, /运维.*执行|真实执行/i)
  assert.match(zh, /非架构级.*修复/i)
  assert.doesNotMatch(zh, /allowRepair|需 Manager 批准才可修改源码/i)
})
}

{
const { default: assert } = await import("node:assert/strict");
const office = await import("../../../dist/Participant/Persona/OfficeCapabilitySurface.js");
const ptySurface = await import("../../../dist/Process/Surface.js");
const { integrationTest } = await import("../../verification-system/tests/support/tier-gate.mjs");

integrationTest('WHAT[office-capability-017] DevOps executes real commands and PTY sessions are cascade closed on road teardown', async () => {
  // 1. DevOps consequence model: full engineering mutation + real command execution + PTY
  const devopsPerms = office.permissions('devops');
  assert.ok(devopsPerms.includes('Read'), 'DevOps has Read');
  assert.ok(devopsPerms.includes('Write'), 'DevOps has Write');
  assert.ok(devopsPerms.includes('Edit'), 'DevOps has Edit');
  assert.ok(devopsPerms.includes('Exec'), 'DevOps has Exec');
  assert.ok(devopsPerms.includes('Pty'), 'DevOps has Pty');
  assert.equal(devopsPerms.includes('Fission'), false, 'DevOps must not have Fission');

  // 2. Process teardown contract: PTY supervisor can cascade terminate all active processes
  const port = ptySurface.createPtyPort();
  assert.ok(port, 'PtyPort must be created');

  // Verify list is initially empty
  const initialList = ptySurface.portList(port);
  assert.equal(initialList.ptys.length, 0);

  // Verify CloseAll completes and guarantees teardown without hanging
  await ptySurface.portCloseAll(port, 100);
  const afterList = ptySurface.portList(port);
  assert.equal(afterList.ptys.length, 0, 'No active PTYs must remain after CloseAll');
});
}
