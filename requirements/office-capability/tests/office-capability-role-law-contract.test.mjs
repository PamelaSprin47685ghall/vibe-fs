/**
 * office-capability — role-law contract tests for propositions whose proof
 * anchors live in the provider Role Laws (bilingual docs under
 * resources/provider/role/*). These are live-repo canaries: each proposition
 * is proven by asserting its entitled consequence / non-consequence against
 * the actual role documents, in both locales.
 *
 * Imports: node builtins only (contract §4.6).
 */
import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'

const ROOT = join(dirname(fileURLToPath(import.meta.url)), '../../..')
const readRole = (role, locale) => readFileSync(join(ROOT, 'resources/provider/role', role, locale), 'utf8')

test('WHAT[OFF-004] capability_is_consequence_model_not_tool_whitelist_transcription', () => {
  const en = readRole('manager', 'en.md')
  const zh = readRole('manager', 'zh-CN.md')
  assert.match(en, /Know another office by its promises, not by its keys/i)
  assert.match(en, /not by the instruments hidden[\s\S]{0,20}inside it/i)
  assert.match(zh, /应看它的承诺，而不是它的钥匙/)
  assert.match(zh, /而不是看它内部隐藏着什么工具/)
})

test('WHAT[OFF-007] manager_has_no_personal_repository_witness_and_no_fission', () => {
  const en = readRole('manager', 'en.md')
  const zh = readRole('manager', 'zh-CN.md')
  assert.match(en, /do not establish repository facts with your own hands/i)
  assert.match(zh, /不以自己的双手去建立 repository 事实/)
  assert.match(en, /cannot (?:use )?Fission/i, 'Manager must explicitly deny fission')
  assert.match(zh, /不能(?:使用)?\s*Fission/, 'Manager must explicitly deny fission')
})

test('WHAT[OFF-011] manager_audit_pending_consequence_is_readonly_assessment_not_mutation', () => {
  const en = readRole('manager', 'en.md')
  const zh = readRole('manager', 'zh-CN.md')
  assert.match(en, /do not establish repository facts with your own hands/i)
  assert.match(zh, /不以自己的双手去建立 repository 事实/)
})

test('WHAT[OFF-012] orchestrator_commissions_manager_roads_not_phases', () => {
  const en = readRole('orchestrator', 'en.md')
  const zh = readRole('orchestrator', 'zh-CN.md')
  assert.match(en, /You commission independent destinations, not technical phases/i)
  assert.match(en, /give it its own Manager and its own road/i)
  assert.match(zh, /你委派的是彼此独立的目的地，而不是技术阶段/)
  assert.match(zh, /给它自己的 Manager，给它自己的道路/)
})

test('WHAT[OFF-016] engineer_role_law_carries_investigation_mutation_and_exclusive_fission', () => {
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

test('WHAT[OFF-017] devops_role_law_carries_execution_and_inherent_repair_without_allow_repair_toggle', () => {
  const en = readRole('devops', 'en.md')
  const zh = readRole('devops', 'zh-CN.md')
  assert.match(en, /operational objective|execution/i)
  assert.match(en, /non-architectural|repair/i)
  assert.doesNotMatch(en, /allowRepair|managerApprovedMutation/i)
  assert.match(zh, /运维.*执行|真实执行/i)
  assert.match(zh, /非架构级.*修复/i)
  assert.doesNotMatch(zh, /allowRepair|需 Manager 批准才可修改源码/i)
})
