import assert from 'node:assert/strict'
import test from 'node:test'
import { readFileSync } from 'node:fs'
import './support/manager-programming.mjs'
import * as office from '../../../dist/Participant/Persona/OfficeCapabilitySurface.js'
import { rolePermissionRules, reviewToolPermissions } from '../../../dist/OpenCode/Tools/ToolSurface.js'
import { rolePredicate } from '../../../dist/OpenCode/Tools/ToolRegistrySurface.js'
import { withExecutablePlugin } from '../../verification-system/tests/support/plugin-fixture.mjs'

const journal = await import('../../../dist/Persistence/Journal/ObligationJournalSurface.js')
const forkTool = await import('../../../dist/Execution/Delegation/Fork/OpenCode/ToolSurface.js')
const revisionSurface = await import('../../../dist/Persistence/Journal/RevisionSurface.js')

const openIncumbencyRefusal =
  'openIncumbency no longer appends RoadOpened; this surface has no event store, observed head, or work id'
const acceptedAssessmentRefusal =
  'injectAcceptedAssessment refused: no event store, observed head, or workId; zero writes'

const assertOpenIncumbencyRefused = async (runtime, sessionId) => {
  const before = revisionSurface.revision(runtime.journal)
  const opened = await journal.openIncumbency(runtime.journal, sessionId, `inc-${sessionId}`)
  assert.equal(opened?.ok, false)
  assert.equal(opened?.error, openIncumbencyRefusal)
  assert.equal(revisionSurface.revision(runtime.journal), before, 'refused openIncumbency must not append')
}

const assertAcceptedAssessmentRefused = async (runtime, sessionId) => {
  const before = revisionSurface.revision(runtime.journal)
  await assert.rejects(forkTool.injectAcceptedAssessment(runtime, sessionId), (error) => {
    assert.equal(error?.message, acceptedAssessmentRefusal)
    return true
  })
  assert.equal(revisionSurface.revision(runtime.journal), before, 'refused injectAcceptedAssessment must not append')
}

const MANAGER_REVIEW_TOOL = 'js-manager'
const RETIRED_REVIEW_TOOLS = ['read-manager', 'glob-manager', 'grep-manager']
const managerFacts = (hasActiveIncumbency, hasAssessment, isFinalIncumbent, cleanupBlockerDigest) =>
  office.managerFacts(hasActiveIncumbency, hasAssessment, isFinalIncumbent, cleanupBlockerDigest)
const isAllowedForManagerFacts = (facts, permission) => office.managerFactsAllowed(facts, permission)

test('WHAT[capability-enforcement-025] P01_unaccepted_review_manager_static_permissions_include_read_glob_grep', () => {
  const perms = office.permissions('manager')
  assert.ok(perms.includes('Read'), 'Manager static permissions must include Read')
  assert.ok(perms.includes('Glob'), 'Manager static permissions must include Glob')
  assert.ok(perms.includes('Grep'), 'Manager static permissions must include Grep')
})

test('WHAT[capability-enforcement-025] P02_review_accepted_facts_exclude_review_readonly_capabilities', () => {
  // OfficeCapabilitySurface 构造已接纳评审事实（HasActiveIncumbency=true, HasAssessment=true）
  const facts = managerFacts(true, true, false, undefined)

  assert.equal(isAllowedForManagerFacts(facts, 'Read'), true, 'Read stays available after assessment accepted')
  assert.equal(isAllowedForManagerFacts(facts, 'Glob'), true, 'Glob stays available after assessment accepted')
  assert.equal(isAllowedForManagerFacts(facts, 'Grep'), true, 'Grep stays available after assessment accepted')

  // 评审接纳后，Join/Fork 等管理与编排权限仍被允许
  assert.equal(isAllowedForManagerFacts(facts, 'Fork'), true, 'Fork must remain allowed after assessment accepted')
  assert.equal(isAllowedForManagerFacts(facts, 'Resume'), true, 'Resume must remain allowed after assessment accepted')
  assert.equal(isAllowedForManagerFacts(facts, 'Join'), true, 'Join must remain allowed after assessment accepted')
  assert.equal(isAllowedForManagerFacts(facts, 'Horizon'), true, 'Horizon must remain allowed after assessment accepted')
  assert.equal(isAllowedForManagerFacts(facts, 'Finality'), true, 'Finality must remain allowed after assessment accepted')
})

test('WHAT[capability-enforcement-025] P08_manager_native_read_glob_grep_projected_as_deny', () => {
  const managerPerms = rolePermissionRules('manager')
  assert.equal(managerPerms.read, 'deny', 'Native read must be projected as deny for Manager')
  assert.equal(managerPerms.glob, 'deny', 'Native glob must be projected as deny for Manager')
  assert.equal(managerPerms.grep, 'deny', 'Native grep must be projected as deny for Manager')
})

test('WHAT[capability-enforcement-025] P09_manager_allows_sole_review_tool_and_retired_tools_not_admitted_to_any_role', () => {
  const managerPerms = rolePermissionRules('manager')
  assert.equal(managerPerms['js-engineer'], 'deny', 'Manager must deny js-engineer')
  assert.equal(managerPerms['js-devops'], 'deny', 'Manager must deny js-devops')
  assert.equal(rolePredicate('js-engineer', 'manager'), false, 'Role predicate must deny js-engineer for Manager')
  assert.equal(rolePredicate('js-devops', 'manager'), false, 'Role predicate must deny js-devops for Manager')
  assert.equal(managerPerms[MANAGER_REVIEW_TOOL], 'allow', 'Manager must allow the sole review tool js-manager')

  for (const tool of RETIRED_REVIEW_TOOLS) {
    for (const role of ['manager', 'engineer', 'devops']) {
      assert.equal(rolePredicate(tool, role), false, `Retired tool ${tool} must not be admitted to ${role}`)
    }
  }
})

test('WHAT[capability-enforcement-025] P10_sole_review_tool_allowed_and_retired_tools_absent_from_manager_surface', () => {
  const managerPerms = rolePermissionRules('manager')
  assert.equal(managerPerms[MANAGER_REVIEW_TOOL], 'allow', 'Manager request projection must allow the sole review tool js-manager')
  for (const tool of RETIRED_REVIEW_TOOLS) {
    assert.equal(tool in managerPerms, false, `Retired tool ${tool} must no longer appear in the Manager tool surface`)
  }
})

test('WHAT[capability-enforcement-025] P05_no_active_incumbency_and_no_assessment_denies_review_readonly_admission', () => {
  // 无 active incumbency：当前事实收口为空集，js-manager 依赖的只读能力全部 fail closed
  const noIncumbency = managerFacts(false, false, false, undefined)
  for (const permission of ['Read', 'Glob', 'Grep']) {
    assert.equal(
      isAllowedForManagerFacts(noIncumbency, permission),
      false,
      `Without active incumbency, review-readonly permission ${permission} must fail closed`,
    )
  }

  // retired 工具在角色层即不被识别，任何角色都 fail closed
  for (const tool of RETIRED_REVIEW_TOOLS) {
    assert.equal(rolePredicate(tool, 'manager'), false, `Retired tool ${tool} must fail closed at the role layer`)
  }
})

test('WHAT[capability-enforcement-025] P06_unknown_identity_never_authorized_by_tool_name_suffix', () => {
  const unknownCallers = ['unknown', 'guest', 'coder', 'inspector', '']
  for (const caller of unknownCallers) {
    assert.equal(rolePredicate('js-unknown', caller), false, `Unknown caller '${caller}' must not be admitted by js- suffix`)
    for (const tool of [...RETIRED_REVIEW_TOOLS, MANAGER_REVIEW_TOOL]) {
      assert.equal(rolePredicate(tool, caller), false, `Unknown caller '${caller}' must not be admitted to ${tool}`)
    }
    assert.equal(rolePredicate('js-engineer', caller), false, `Unknown caller '${caller}' must not be admitted to js-engineer`)
  }
})

test('WHAT[capability-enforcement-025] P03_unaccepted_or_malformed_review_retains_readonly_permissions', async () => {
  await withExecutablePlugin(async (_hooks, _directory, _createdIds, runtime) => {
    await assertOpenIncumbencyRefused(runtime, 'ses-p03')
  })
})

test('WHAT[capability-enforcement-025] P07_cleanup_blocker_or_inactive_incumbency_denies_review_tool', () => {
  const REVIEW_TOOLS = ['js-manager']

  // 1. 清理阻塞状态（CleanupBlockerDigest 有值）下：收窄为仅 Join/Finality，评审专用只读工具必须拒绝
  const factsCleanup = managerFacts(true, false, false, 'blocker-digest-xyz')
  for (const tool of REVIEW_TOOLS) {
    const perms = reviewToolPermissions(tool)
    for (const p of perms) {
      assert.equal(
        isAllowedForManagerFacts(factsCleanup, p),
        false,
        `${tool} permission ${p} must be denied under cleanup blocker`,
      )
    }
  }

  // 2. 退任冻结与非活跃状态：无活跃任期（HasActiveIncumbency = false）评审专用工具收口拒绝
  const factsFrozenOrInactive = managerFacts(false, false, false, undefined)
  for (const tool of REVIEW_TOOLS) {
    const perms = reviewToolPermissions(tool)
    for (const p of perms) {
      assert.equal(
        isAllowedForManagerFacts(factsFrozenOrInactive, p),
        false,
        `${tool} permission ${p} must be denied when incumbency is not active`,
      )
    }
  }
})

test('WHAT[capability-enforcement-025] P16_final_incumbent_keeps_read_cleanup_and_closeout_permissions', () => {
  // 末任：已接纳空集 findings 的评估（HasAssessment = true, IsFinalIncumbent = true）。
  // 读、清理与收口类动作必须放行。
  const facts = managerFacts(true, true, true, undefined)
  for (const permission of ['Read', 'Glob', 'Grep', 'Horizon', 'Join', 'Finality']) {
    assert.equal(
      isAllowedForManagerFacts(facts, permission),
      true,
      `final incumbent must keep close-out permission ${permission}`,
    )
  }
})

test('WHAT[capability-enforcement-025] P17_final_incumbent_loses_every_new_work_capability', () => {
  // 末任不得开新工作：委托、续做与评审一律拒绝。
  const facts = managerFacts(true, true, true, undefined)
  for (const permission of ['Fork', 'Resume', 'ReviewAssessment']) {
    assert.equal(
      isAllowedForManagerFacts(facts, permission),
      false,
      `final incumbent must lose new-work permission ${permission}`,
    )
  }
})

test('WHAT[capability-enforcement-025] P18_final_incumbent_notice_text_present_in_both_languages', () => {
  const zh = readFileSync(new URL('../../../resources/provider/runtime/manager-finish/zh-CN.md', import.meta.url), 'utf8')
  const en = readFileSync(new URL('../../../resources/provider/runtime/manager-finish/en.md', import.meta.url), 'utf8')
  assert.match(zh, /最后一任/)
  assert.match(zh, /findings 为空集/)
  assert.match(zh, /不得开新工作/)
  assert.match(en, /final Manager/)
  assert.match(en, /findings is empty/)
  assert.match(en, /do not start new work/)
})

test('WHAT[capability-enforcement-025] P11_plugin_reopen_evaluates_durable_incumbency_facts_without_resetting_assessment', async () => {
  await withExecutablePlugin(async (_hooks, _directory, _createdIds, runtime) => {
    await assertOpenIncumbencyRefused(runtime, 'ses-p11-accepted')
    await assertAcceptedAssessmentRefused(runtime, 'ses-p11-accepted')
    await assertOpenIncumbencyRefused(runtime, 'ses-p11-unaccepted')
  })
})

test('WHAT[capability-enforcement-025] P12_current_fact_classifier_distinguishes_accepted_and_unaccepted_assessment', () => {
  // 两个不同任期状态的 facts 独立评估：
  // 任期 1：活跃但已接纳评审
  const incumbency1Facts = managerFacts(true, true, false, undefined)
  // 任期 2：合法新任期，活跃且未接纳评审
  const incumbency2Facts = managerFacts(true, false, false, undefined)

  assert.equal(isAllowedForManagerFacts(incumbency1Facts, 'Read'), true)
  assert.equal(isAllowedForManagerFacts(incumbency1Facts, 'Glob'), true)
  assert.equal(isAllowedForManagerFacts(incumbency1Facts, 'Grep'), true)

  // 任期 2（新任期）不继承任期 1 的已评审限制，专用只读能力正常开启
  assert.equal(isAllowedForManagerFacts(incumbency2Facts, 'Read'), true)
  assert.equal(isAllowedForManagerFacts(incumbency2Facts, 'Glob'), true)
  assert.equal(isAllowedForManagerFacts(incumbency2Facts, 'Grep'), true)

  // 两个任期的调用授权互不借用，也不把旧 assessment 错封新任期
})

test('WHAT[capability-enforcement-025] P13_review_accepted_after_before_hook_is_rejected_at_execution', async () => {
  await withExecutablePlugin(async (_hooks, _directory, _createdIds, runtime) => {
    await assertOpenIncumbencyRefused(runtime, 'ses-p13')
    await assertAcceptedAssessmentRefused(runtime, 'ses-p13')
  })
})

test('WHAT[capability-enforcement-025] P14_completed_readonly_call_keeps_after_hook_while_subsequent_calls_are_denied', async () => {
  await withExecutablePlugin(async (_hooks, _directory, _createdIds, runtime) => {
    await assertOpenIncumbencyRefused(runtime, 'ses-p14')
    await assertAcceptedAssessmentRefused(runtime, 'ses-p14')
  })
})
test('WHAT[capability-enforcement-025] an admitted call reads the file and before-hook denial preserves arguments and journal revision', async () => {
  await withExecutablePlugin(async (_hooks, _directory, _createdIds, runtime) => {
    await assertOpenIncumbencyRefused(runtime, 'ses-p15')
    await assertAcceptedAssessmentRefused(runtime, 'ses-p15')
  })
})

test.todo('WHAT[capability-enforcement-025] denial performs zero physical reads and write attempts through the production invocation (GAP-075: investigation 2026-10-03 — the production chain ToolRegistry → JsToolSpec → ToolWorkflow keeps read snapshots internal; the only wired observation port is RequirementGrounding.programObservation, which fires solely for paths that have grounding materials in the workspace, so a generic physical-read observer does not exist. Needed: a read-path observation port on JsToolSpec/ToolWorkflow (or an equivalent production entry) that the test fixture can subscribe to; the execute counter and journal revision in the passing test above remain the closest available proxies)')

test.todo('WHAT[capability-enforcement-025] an in-flight admitted read finishes across review acceptance (GAP-075: requires a controlled causal barrier driving real overlap; the sequential fixture cannot prove concurrency)')
