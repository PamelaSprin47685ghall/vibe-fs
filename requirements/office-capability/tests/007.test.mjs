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

test('WHAT[office-capability-007] OFF_007_manager_forkable_offices_is_strictly_engineer', () => {
  assert.deepEqual(managerForkableOffices(), ['engineer'])
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

test('WHAT[office-capability-007] manager_has_no_personal_repository_witness_and_no_fission', () => {
  const en = readRole('manager', 'en.md')
  const zh = readRole('manager', 'zh-CN.md')
  assert.match(en, /do not establish repository facts with your own hands/i)
  assert.match(zh, /不以自己的双手去建立 repository 事实/)
  assert.match(en, /cannot (?:use )?Fission/i, 'Manager must explicitly deny fission')
  assert.match(zh, /不能(?:使用)?\s*Fission/, 'Manager must explicitly deny fission')
})

test('WHAT[office-capability-007] manager_direct_read_window_is_bounded_by_review_acceptance', () => {
  const en = readRole('manager', 'en.md')
  const zh = readRole('manager', 'zh-CN.md')
  assert.match(en, /review-only read tool/i, 'role law must name the review-only read tool')
  assert.match(zh, /评审专用只读工具/, 'role law must name the review-only read tool in zh-CN')
  assert.match(en, /before[\s\S]{0,80}review is accepted/i, 'role law must open the window before review acceptance')
  assert.match(zh, /评审未接纳前/, 'role law must open the window before review acceptance in zh-CN')
  assert.match(en, /acceptance[\s\S]{0,40}closes/i, 'role law must close the window at acceptance')
  assert.match(zh, /评审一旦接纳/, 'role law must close the window at acceptance in zh-CN')
})
}

{
const { default: assert } = await import("node:assert/strict");
const office = await import("../../../dist/Participant/Persona/OfficeCapabilitySurface.js");
const bindingSurface = await import("../../../dist/OpenCode/Host/SessionBindingSurface.js");
const { integrationTest } = await import("../../verification-system/tests/support/tier-gate.mjs");

integrationTest('WHAT[office-capability-007] Manager road resumes fixed DevOps across relay incumbency iterations without creating substitute DevOps', async () => {
  // 1. Manager consequence: has resume for existing devops, but no Fission
  const managerPerms = office.permissions('manager');
  assert.ok(managerPerms.includes('Resume'), 'Manager must have Resume permission');
  assert.equal(managerPerms.includes('Fission'), false, 'Manager must have NO Fission');
  assert.equal(managerPerms.includes('Exec'), false, 'Manager must have NO Exec');

  // 2. Road-bound DevOps binding survives relay incumbency handoff
  const roadSessionId = 'ses-manager-road-relay';
  const devopsSessionId = 'ses-devops-road-agent';
  const initialModel = { providerID: 'host', modelID: 'devops-fixed-model' };

  // Initial road binding
  bindingSurface.bind(roadSessionId, devopsSessionId, 'devops');
  bindingSurface.bindDevOpsModel(devopsSessionId, initialModel);

  assert.equal(bindingSurface.tryParent(devopsSessionId), roadSessionId);
  assert.equal(bindingSurface.tryAgent(devopsSessionId), 'devops');

  // Relay incumbency handoff: Manager iteration 1 retires (Continue), Manager iteration 2 takes over
  // Road-level DevOps binding remains unchanged and identical
  assert.equal(bindingSurface.tryParent(devopsSessionId), roadSessionId);
  assert.equal(bindingSurface.tryAgent(devopsSessionId), 'devops');

  // Model binding remains locked across iterations
  bindingSurface.verifyDevOpsModel(devopsSessionId, initialModel);

  // Attempting to substitute or drift model across iterations is rejected fail-closed
  assert.throws(
    () => {
      bindingSurface.verifyDevOpsModel(devopsSessionId, { providerID: 'host', modelID: 'deviated-model' });
    },
    /CRASH-020/,
    'DevOps model drift across relay iterations must fail-closed',
  );
});
}
