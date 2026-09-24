import assert from 'node:assert/strict'
import test from 'node:test'
import * as office from '../../../dist/Participant/Persona/OfficeCapabilitySurface.js'
import { permissionObj } from '../../../dist/OpenCode/Tools/StaticTools.js'
import { rolePredicate } from '../../../dist/OpenCode/Tools/ToolRegistrySurface.js'
import { Role } from '../../../dist/Foundation/Roles.js'
import {
  ManagerCapabilityFacts,
  OfficeCapability_isAllowedForManagerFacts as isAllowedForManagerFacts,
  ToolPermission,
} from '../../../dist/Foundation/OfficeCapability.js'
import {
  acceptAuthorityRoot,
  openIncumbency,
  injectAcceptedAssessment,
  withExecutablePlugin,
  withRestartablePlugin,
} from '../../verification-system/tests/support/plugin-fixture.mjs'
import { requiredPermissions } from '../../../dist/OpenCode/Tools/ManagerReviewTools.js'

const MANAGER_REVIEW_TOOL = 'js-manager'
const RETIRED_REVIEW_TOOLS = ['read-manager', 'glob-manager', 'grep-manager']

test('WHAT[capability-enforcement-025] P01_unaccepted_review_manager_static_permissions_include_read_glob_grep', () => {
  const perms = office.permissions('manager')
  assert.ok(perms.includes('Read'), 'Manager static permissions must include Read')
  assert.ok(perms.includes('Glob'), 'Manager static permissions must include Glob')
  assert.ok(perms.includes('Grep'), 'Manager static permissions must include Grep')
})

test('WHAT[capability-enforcement-025] P02_review_accepted_facts_exclude_review_readonly_capabilities', () => {
  // 依据 dist/Foundation/OfficeCapability.js 构造已接纳评审事实（HasActiveIncumbency=true, HasAssessment=true）
  const facts = new ManagerCapabilityFacts(true, true, false, undefined)

  // 评审接纳后，只读能力 Read/Glob/Grep 均不被允许
  assert.equal(isAllowedForManagerFacts(facts, ToolPermission.Read), false, 'Read must be denied after assessment accepted')
  assert.equal(isAllowedForManagerFacts(facts, ToolPermission.Glob), false, 'Glob must be denied after assessment accepted')
  assert.equal(isAllowedForManagerFacts(facts, ToolPermission.Grep), false, 'Grep must be denied after assessment accepted')

  // 评审接纳后，Join/Fork 等管理与编排权限仍被允许
  assert.equal(isAllowedForManagerFacts(facts, ToolPermission.Fork), true, 'Fork must remain allowed after assessment accepted')
  assert.equal(isAllowedForManagerFacts(facts, ToolPermission.Resume), true, 'Resume must remain allowed after assessment accepted')
  assert.equal(isAllowedForManagerFacts(facts, ToolPermission.Join), true, 'Join must remain allowed after assessment accepted')
  assert.equal(isAllowedForManagerFacts(facts, ToolPermission.Horizon), true, 'Horizon must remain allowed after assessment accepted')
  assert.equal(isAllowedForManagerFacts(facts, ToolPermission.Finality), true, 'Finality must remain allowed after assessment accepted')
  assert.equal(isAllowedForManagerFacts(facts, ToolPermission.Sphinx), true, 'Sphinx must remain allowed after assessment accepted')
})

test('WHAT[capability-enforcement-025] P08_manager_native_read_glob_grep_projected_as_deny', () => {
  const managerPerms = permissionObj(Role.Manager)
  assert.equal(managerPerms.read, 'deny', 'Native read must be projected as deny for Manager')
  assert.equal(managerPerms.glob, 'deny', 'Native glob must be projected as deny for Manager')
  assert.equal(managerPerms.grep, 'deny', 'Native grep must be projected as deny for Manager')
})

test('WHAT[capability-enforcement-025] P09_manager_allows_sole_review_tool_and_retired_tools_not_admitted_to_any_role', () => {
  const managerPerms = permissionObj(Role.Manager)
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
  const managerPerms = permissionObj(Role.Manager)
  assert.equal(managerPerms[MANAGER_REVIEW_TOOL], 'allow', 'Manager request projection must allow the sole review tool js-manager')
  for (const tool of RETIRED_REVIEW_TOOLS) {
    assert.equal(tool in managerPerms, false, `Retired tool ${tool} must no longer appear in the Manager tool surface`)
  }
})

test('WHAT[capability-enforcement-025] P05_no_active_incumbency_and_no_assessment_denies_review_readonly_admission', () => {
  // 无 active incumbency：当前事实收口为空集，js-manager 依赖的只读能力全部 fail closed
  const noIncumbency = new ManagerCapabilityFacts(false, false, false, undefined)
  for (const permission of [ToolPermission.Read, ToolPermission.Glob, ToolPermission.Grep]) {
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
  await withExecutablePlugin(async (hooks, _directory, _createdIds, runtime) => {
    const sessionID = 'ses-p03'
    // 权威确立与任期建立：先确立 Manager 权威，再打开 AuditPending 任期
    await acceptAuthorityRoot(runtime, sessionID, 'manager')
    await openIncumbency(runtime, sessionID)

    // 驱动 malformed review 尝试：传入无效评分或缺少规范字段
    const reviewResult = await hooks.tool.review.execute(
      { scores: 'invalid-scores-malformed' },
      { sessionID, agent: 'manager' },
    )
    // 评审未被接纳（返回 recorded = false 或拒绝提示）
    assert.match(String(reviewResult), /false|invalid|missing|有效评分|valid ratings/i)

    // 评审未被接纳前，js-manager 专用只读工具仍准入，不得误封
    const beforeOutput = {
      args: { program: "class Js extends JsProgram { async run() { const f = await this.file('src/Model.fs'); return f.text('^', '$'); } }", contract: 'do-not-use-except-for-review' },
    }
    await hooks['tool.execute.before'](
      { tool: 'js-manager', sessionID, callID: 'call-p03' },
      beforeOutput,
    )
    assert.equal('contract' in beforeOutput.args, false, 'js-manager must remain admitted and hide contract')
  })
})

test('WHAT[capability-enforcement-025] P07_valid_certificate_cleanup_blocker_or_retirement_freeze_denies_review_tool', () => {
  const REVIEW_TOOLS = ['js-manager']

  // 1. 有效绑定证书状态（HasValidBoundCertificate = true）下：收窄为仅 Join/Finality，评审专用只读工具必须拒绝
  const factsCert = new ManagerCapabilityFacts(true, false, true, undefined)
  for (const tool of REVIEW_TOOLS) {
    const perms = requiredPermissions(tool)
    for (const p of perms) {
      assert.equal(
        isAllowedForManagerFacts(factsCert, p),
        false,
        `${tool} permission ${p.cases ? p.cases()[p.tag] : p} must be denied under valid certificate`,
      )
    }
  }

  // 2. 清理阻塞状态（CleanupBlockerDigest 有值）下：收窄为仅 Join/Finality，评审专用只读工具必须拒绝
  const factsCleanup = new ManagerCapabilityFacts(true, false, false, 'blocker-digest-xyz')
  for (const tool of REVIEW_TOOLS) {
    const perms = requiredPermissions(tool)
    for (const p of perms) {
      assert.equal(
        isAllowedForManagerFacts(factsCleanup, p),
        false,
        `${tool} permission ${p.cases ? p.cases()[p.tag] : p} must be denied under cleanup blocker`,
      )
    }
  }

  // 3. 退任冻结与非活跃状态：无活跃任期（HasActiveIncumbency = false）评审专用工具收口拒绝
  const factsFrozenOrInactive = new ManagerCapabilityFacts(false, false, false, undefined)
  for (const tool of REVIEW_TOOLS) {
    const perms = requiredPermissions(tool)
    for (const p of perms) {
      assert.equal(
        isAllowedForManagerFacts(factsFrozenOrInactive, p),
        false,
        `${tool} permission ${p.cases ? p.cases()[p.tag] : p} must be denied when incumbency is not active`,
      )
    }
  }
})

test('WHAT[capability-enforcement-025] P11_session_restart_and_compaction_evaluates_recovered_incumbency_facts_without_resetting_assessment', async () => {
  await withRestartablePlugin(async (start, _directory, { stop, withRuntime }) => {
    const sessionAccepted = 'ses-p11-accepted'
    const sessionUnaccepted = 'ses-p11-unaccepted'

    const firstPlugin = await start()

    await withRuntime(async (runtime) => {
      // sessionAccepted: 活跃任期且已接纳评审
      await openIncumbency(runtime, sessionAccepted)
      await injectAcceptedAssessment(runtime, sessionAccepted)

      // sessionUnaccepted: 活跃任期但未接纳评审
      await openIncumbency(runtime, sessionUnaccepted)
    })

    // 重启插件实例（模拟 session 重启与崩溃恢复）
    await stop(firstPlugin)
    const restartedPlugin = await start()

    // 重启后验证已接纳评审的 session：js-manager 专用只读工具仍严格拒绝，不重置评审
    const acceptedBeforeOutput = {
      args: { program: "class Js extends JsProgram { async run() { const f = await this.file('src/Model.fs'); return f.text('^', '$'); } }", contract: 'do-not-use-except-for-review' },
    }
    await assert.rejects(
      async () => {
        await restartedPlugin['tool.execute.before'](
          { tool: 'js-manager', sessionID: sessionAccepted, callID: 'call-p11-acc' },
          acceptedBeforeOutput,
        )
      },
      /not permitted under current manager capability facts/i,
      'js-manager must remain denied after restart when assessment was already accepted',
    )

    // 重启后验证未接纳评审的 session：js-manager 专用只读工具仍准入，contract 正常被隐藏
    const unacceptedBeforeOutput = {
      args: { program: "class Js extends JsProgram { async run() { const f = await this.file('src/Model.fs'); return f.text('^', '$'); } }", contract: 'do-not-use-except-for-review' },
    }
    await restartedPlugin['tool.execute.before'](
      { tool: 'js-manager', sessionID: sessionUnaccepted, callID: 'call-p11-unacc' },
      unacceptedBeforeOutput,
    )
    assert.equal(
      'contract' in unacceptedBeforeOutput.args,
      false,
      'js-manager must remain admitted after restart when assessment was not yet accepted',
    )

    await stop(restartedPlugin)
  })
})

test('WHAT[capability-enforcement-025] P12_distinct_incumbencies_do_not_leak_authorization_or_misattribute_assessment', async () => {
  // 两个不同任期状态的 facts 独立评估：
  // 任期 1：活跃但已接纳评审
  const incumbency1Facts = new ManagerCapabilityFacts(true, true, false, undefined)
  // 任期 2：合法新任期，活跃且未接纳评审
  const incumbency2Facts = new ManagerCapabilityFacts(true, false, false, undefined)

  // 任期 1 专用只读能力被封禁
  assert.equal(isAllowedForManagerFacts(incumbency1Facts, ToolPermission.Read), false)
  assert.equal(isAllowedForManagerFacts(incumbency1Facts, ToolPermission.Glob), false)
  assert.equal(isAllowedForManagerFacts(incumbency1Facts, ToolPermission.Grep), false)

  // 任期 2（新任期）不继承任期 1 的已评审限制，专用只读能力正常开启
  assert.equal(isAllowedForManagerFacts(incumbency2Facts, ToolPermission.Read), true)
  assert.equal(isAllowedForManagerFacts(incumbency2Facts, ToolPermission.Glob), true)
  assert.equal(isAllowedForManagerFacts(incumbency2Facts, ToolPermission.Grep), true)

  // 两个任期的调用授权互不借用，也不把旧 assessment 错封新任期
})

test('WHAT[capability-enforcement-025] P13_review_accepted_after_before_hook_blocks_execution_with_zero_reads', async () => {
  await withExecutablePlugin(async (hooks, _directory, _createdIds, runtime) => {
    const sessionID = 'ses-p13'
    const callID = 'call-p13'
    // 权威确立与任期建立：先确立 Manager 权威根，再打开 AuditPending 任期
    await acceptAuthorityRoot(runtime, sessionID, 'manager')
    await openIncumbency(runtime, sessionID)

    const beforeOutput = {
      args: {
        program: "class Js extends JsProgram { async run() { const f = await this.file('src/App.fs'); return f.text('^', '$'); } }",
        contract: 'do-not-use-except-for-review',
      },
    }

    // Step 1: before hook 检查通过并放行，私有隐藏 contract
    await hooks['tool.execute.before'](
      { tool: 'js-manager', sessionID, callID },
      beforeOutput,
    )
    assert.equal('contract' in beforeOutput.args, false, 'before hook must strip contract parameter')

    // Step 2: 在执行前注入已接纳评审事实（AcceptedAssessment）
    await injectAcceptedAssessment(runtime, sessionID)

    // Step 3: 工具进入运行时 ToolRegistry 执行门禁，门禁读取最新事实再次拒绝，确保零文件读取
    const execResult = await hooks.tool['js-manager'].execute(
      beforeOutput.args,
      { sessionID, agent: 'manager' },
    )
    assert.match(
      String(execResult),
      /当前不可用|not available right now|denied-task-state/i,
      'ToolRegistry gate must reject execution due to accepted assessment facts',
    )
    assert.doesNotMatch(
      String(execResult),
      /权威确立之前|authority is established/i,
      'Rejection must not be due to unestablished authority',
    )
  })
})

test('WHAT[capability-enforcement-025] P14_readonly_call_admitted_before_review_acceptance_completes_while_subsequent_calls_are_denied', async () => {
  await withExecutablePlugin(async (hooks, _directory, _createdIds, runtime) => {
    const sessionID = 'ses-p14'
    const call1ID = 'call-p14-1'
    const call2ID = 'call-p14-2'
    await openIncumbency(runtime, sessionID)

    // Call 1 启动并在 Review 接纳前获得最终准入与执行
    const call1Output = {
      args: {
        program: "class Js extends JsProgram { async run() { const f = await this.file('src/App.fs'); return f.text('^', '$'); } }",
        contract: 'do-not-use-except-for-review',
      },
    }
    await hooks['tool.execute.before'](
      { tool: 'js-manager', sessionID, callID: call1ID },
      call1Output,
    )
    const call1ExecResult = await hooks.tool['js-manager'].execute(
      call1Output.args,
      { sessionID, agent: 'manager' },
    )
    assert.doesNotMatch(
      String(call1ExecResult),
      /denied-task-state/i,
      'Call 1 must be admitted before assessment is accepted',
    )

    // 在 Call 1 获准后，系统接纳 Review
    await injectAcceptedAssessment(runtime, sessionID)

    // Call 1 照常完成 after 恢复
    await hooks['tool.execute.after'](
      { tool: 'js-manager', sessionID, callID: call1ID, args: call1Output.args },
      { title: 'js-manager', output: call1ExecResult, metadata: {} },
    )
    assert.equal(
      call1Output.args.contract,
      'do-not-use-except-for-review',
      'Call 1 must restore contract upon completion',
    )

    // 后续新调用（Call 2）发起，在入口即被拒绝
    const call2Output = {
      args: {
        program: "class Js extends JsProgram { async run() { const f = await this.file('src/App.fs'); return f.text('^', '$'); } }",
        contract: 'do-not-use-except-for-review',
      },
    }
    await assert.rejects(
      async () => {
        await hooks['tool.execute.before'](
          { tool: 'js-manager', sessionID, callID: call2ID },
          call2Output,
        )
      },
      /not permitted under current manager capability facts/i,
      'Subsequent call must be rejected after review acceptance',
    )
  })
})
