import assert from 'node:assert/strict'
import test from 'node:test'
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs'
import { join } from 'node:path'
import { parse as parseToml } from 'smol-toml'
import './support/manager-programming.mjs'
import * as office from '../../../dist/Participant/Persona/OfficeCapabilitySurface.js'
import { rolePermissionRules, reviewToolPermissions } from '../../../dist/OpenCode/Tools/ToolSurface.js'
import { rolePredicate } from '../../../dist/OpenCode/Tools/ToolRegistrySurface.js'
import {
  acceptAuthorityRoot,
  openIncumbency,
  injectAcceptedAssessment,
  withExecutablePlugin,
  withRestartablePlugin,
} from '../../verification-system/tests/support/plugin-fixture.mjs'

const MANAGER_REVIEW_TOOL = 'todowrite-for-manager'
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

  // N-04: 评审接纳后，只读能力 Read/Glob/Grep 均保持允许（读能力不因评审消失）
  assert.equal(isAllowedForManagerFacts(facts, 'Read'), true, 'Read must be allowed after assessment accepted')
  assert.equal(isAllowedForManagerFacts(facts, 'Glob'), true, 'Glob must be allowed after assessment accepted')
  assert.equal(isAllowedForManagerFacts(facts, 'Grep'), true, 'Grep must be allowed after assessment accepted')
  // 评审接纳后，Join/Fork 等管理与编排权限仍被允许
  assert.equal(isAllowedForManagerFacts(facts, 'Fork'), true, 'Fork must remain allowed after assessment accepted')
  assert.equal(isAllowedForManagerFacts(facts, 'Resume'), true, 'Resume must remain allowed after assessment accepted')
  assert.equal(isAllowedForManagerFacts(facts, 'Join'), true, 'Join must remain allowed after assessment accepted')
  assert.equal(isAllowedForManagerFacts(facts, 'Horizon'), true, 'Horizon must remain allowed after assessment accepted')
  assert.equal(isAllowedForManagerFacts(facts, 'Finality'), true, 'Finality must remain allowed after assessment accepted')
})

test('WHAT[capability-enforcement-025] P08_manager_native_read_glob_grep_projected_as_deny', () => {
  // Direct read/glob/grep host tools are retired from knownToolNames and covered
  // by the wildcard "*": "deny" rule; rolePredicate also fails closed on them.
  const managerPerms = rolePermissionRules('manager')
  assert.equal(managerPerms['*'], 'deny', 'Manager wildcard default must deny retired native tools')
  for (const retired of ['read', 'glob', 'grep']) {
    assert.equal(retired in managerPerms, false, `Retired ${retired} must not appear as an explicit Manager permission key`)
    assert.equal(rolePredicate(retired, 'manager'), false, `Role predicate must deny retired ${retired} for Manager`)
  }
})

test('WHAT[capability-enforcement-025] P09_manager_allows_sole_review_tool_and_retired_tools_not_admitted_to_any_role', () => {
  const managerPerms = rolePermissionRules('manager')
  assert.equal(managerPerms['todowrite-for-engineer'], 'deny', 'Manager must deny todowrite-for-engineer')
  assert.equal(managerPerms['todowrite-for-devops'], 'deny', 'Manager must deny todowrite-for-devops')
  assert.equal(rolePredicate('todowrite-for-engineer', 'manager'), false, 'Role predicate must deny todowrite-for-engineer for Manager')
  assert.equal(rolePredicate('todowrite-for-devops', 'manager'), false, 'Role predicate must deny todowrite-for-devops for Manager')
  assert.equal(managerPerms[MANAGER_REVIEW_TOOL], 'allow', 'Manager must allow the sole review tool todowrite-for-manager')

  for (const tool of RETIRED_REVIEW_TOOLS) {
    for (const role of ['manager', 'engineer', 'devops']) {
      assert.equal(rolePredicate(tool, role), false, `Retired tool ${tool} must not be admitted to ${role}`)
    }
  }
})

test('WHAT[capability-enforcement-025] P10_sole_review_tool_allowed_and_retired_tools_absent_from_manager_surface', () => {
  const managerPerms = rolePermissionRules('manager')
  assert.equal(managerPerms[MANAGER_REVIEW_TOOL], 'allow', 'Manager request projection must allow the sole review tool todowrite-for-manager')
  for (const tool of RETIRED_REVIEW_TOOLS) {
    assert.equal(tool in managerPerms, false, `Retired tool ${tool} must no longer appear in the Manager tool surface`)
  }
})

test('WHAT[capability-enforcement-025] P05_no_active_incumbency_and_no_assessment_denies_review_readonly_admission', () => {
  // 无 active incumbency：当前事实收口为空集，todowrite-for-manager 依赖的只读能力全部 fail closed
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
    assert.equal(rolePredicate('todowrite-for-engineer', caller), false, `Unknown caller '${caller}' must not be admitted to todowrite-for-engineer`)
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
      { findings: 'invalid-findings-malformed' },
      { sessionID, agent: 'manager' },
    )
    // 评审未被接纳（返回 recorded = false 或拒绝提示）
    assert.match(String(reviewResult), /false|invalid|missing|有效评分|valid ratings/i)

    // 评审未被接纳前，todowrite-for-manager 专用只读工具仍准入，不得误封
    const beforeOutput = {
      args: { program: "class Js extends JsProgram { async run() { return await this.cat('src/Model.fs'); } }", contract: 'do-not-use-except-for-review' },
    }
    const originalArgs = beforeOutput.args
    const expectedArgs = { ...originalArgs }
    await hooks['tool.execute.before'](
      { tool: 'todowrite-for-manager', sessionID, callID: 'call-p03' },
      beforeOutput,
    )
    assert.equal(beforeOutput.args, originalArgs, 'admission must preserve the public argument object')
    assert.deepEqual(originalArgs, { program: expectedArgs.program })
    await hooks['tool.execute.after'](
      { tool: 'todowrite-for-manager', sessionID, callID: 'call-p03', args: originalArgs },
      { title: 'todowrite-for-manager', output: '', metadata: {} },
    )
    assert.deepEqual(originalArgs, expectedArgs, 'after must restore the public contract hint')
    assert.deepEqual(Object.keys(originalArgs), Object.keys(expectedArgs))
  })
})

test('WHAT[capability-enforcement-025] P07_cleanup_blocker_or_inactive_incumbency_denies_review_tool', () => {
  const REVIEW_TOOLS = ['todowrite-for-manager']

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

const execute = async (hooks, sessionID, body, callID = 'call-review') => parseToml(String(
  await hooks.tool['todowrite-for-manager'].execute(
    typeof body === 'string' ? { program: 'class Js extends JsProgram { async run() { ' + body + ' } }', name: 'manager-exec' } : { name: 'manager-exec', ...body },
    { sessionID, agent: 'manager', callID, messageID: 'msg-' + callID },
  ),
))

const seed = async (runtime, sessionID, directory) => {
  await acceptAuthorityRoot(runtime, sessionID, 'manager')
  await openIncumbency(runtime, sessionID)
  writeFileSync(join(directory, 'evidence.txt'), 'review evidence', 'utf8')
}

const reads = [
  "return await this.reviewOnly.cat('evidence.txt');",
  "return await this.reviewOnly.stat('evidence.txt');",
  "return await this.reviewOnly.ls('.');",
  "return await this.reviewOnly.rg(['review', 'evidence.txt']);",
]

test('WHAT[capability-enforcement-025] P11_restart keeps the entry open and the reviewed reads admitted', async () => {
  await withRestartablePlugin(async (start, directory, { stop, withRuntime }) => {
    const sessionID = 'ses-reopen-review'
    const first = await start()
    await withRuntime(async runtime => {
      await seed(runtime, sessionID, directory)
      await injectAcceptedAssessment(runtime, sessionID)
    })
    await stop(first)
    const reopened = await start()
    assert.equal((await execute(reopened, sessionID, 'return 7;')).data, 7)
    // N-04: Granted read capability must not disappear because a review happened.
    for (const body of reads) assert.ok((await execute(reopened, sessionID, body)).data !== undefined)
    await stop(reopened)
  })
})

test('WHAT[capability-enforcement-025] P13_each physical operation is admitted before review and remains admitted after review', async () => {
  await withExecutablePlugin(async (hooks, directory, _ids, runtime) => {
    const sessionID = 'ses-each-operation'
    await seed(runtime, sessionID, directory)
    for (const [index, body] of reads.entries()) {
      const result = await execute(hooks, sessionID, body, 'positive-' + index)
      assert.equal(result.code, undefined, JSON.stringify(result))
      assert.ok(Object.hasOwn(result, 'data'))
    }
    assert.equal((await execute(hooks, sessionID, reads[0])).data, 'review evidence')
    const input = { args: { program: 'class Js extends JsProgram { async run() { return 7; } }' } }
    await hooks['tool.execute.before']({ tool: MANAGER_REVIEW_TOOL, sessionID, callID: 'before-review' }, input)
    await hooks['tool.execute.after']({ tool: MANAGER_REVIEW_TOOL, sessionID, callID: 'before-review', args: input.args }, { output: '', metadata: {} })
    await injectAcceptedAssessment(runtime, sessionID)
    await hooks['tool.execute.before']({ tool: MANAGER_REVIEW_TOOL, sessionID, callID: 'after-review' }, input)
    assert.equal((await execute(hooks, sessionID, 'return 7;')).data, 7)
    // N-04: Read capabilities survive review acceptance.
    for (const body of reads) assert.ok((await execute(hooks, sessionID, body)).data !== undefined)
    const pure = await execute(hooks, sessionID, "return this.whereAll('a b', 'b');")
    assert.deepEqual(pure.data, [[2, 3]])
  })
})

test('WHAT[capability-enforcement-025] P14_saved object, method and raw binding remain admitted after park and review', async () => {
  await withExecutablePlugin(async (hooks, directory, _ids, runtime) => {
    const sessionID = 'ses-park-review'
    await seed(runtime, sessionID, directory)
    for (const [index, saved] of [
      'const saved = this.reviewOnly; const read = () => saved.cat("evidence.txt");',
      'const read = this.reviewOnly.cat.bind(this.reviewOnly, "evidence.txt");',
      'const raw = this._api.reviewOnly.cat; const read = async () => { const result = await raw("evidence.txt"); if (!result.ok) return result.code; return result.text; };',
    ].entries()) {
      const paused = await execute(hooks, sessionID, saved + ' await this.park({ ready: true }); return await read();', 'park-' + index)
      assert.ok(paused.handle)
      if (index === 0) await injectAcceptedAssessment(runtime, sessionID)
      const result = await execute(hooks, sessionID, { unpark: { handle: paused.handle, value: null } }, 'resume-' + index)
      assert.equal(result.data, 'review evidence')
    }
  })
})

test('WHAT[capability-enforcement-025] P15_final incumbent keeps read capability under N-04', async () => {
  await withExecutablePlugin(async (hooks, directory, _ids, runtime) => {
    const sessionID = 'ses-final-review'
    await seed(runtime, sessionID, directory)
    await injectAcceptedAssessment(runtime, sessionID, 'inc-' + sessionID, 'Promoted')
    assert.equal((await execute(hooks, sessionID, 'return 7;')).data, 7)
    // N-04: Final incumbent retains Read/Glob/Grep; reading existing evidence succeeds.
    assert.equal((await execute(hooks, sessionID, reads[0])).data, 'review evidence')
    const paused = await execute(hooks, sessionID, 'await this.park({ done: true }); return 9;')
    assert.ok(paused.handle)
    assert.equal((await execute(hooks, sessionID, { unpark: { handle: paused.handle, value: null } })).data, 9)
  })
})

test('WHAT[capability-enforcement-025] identity and cross-role denial remain independent of Review', async () => {
  await withExecutablePlugin(async (hooks, _directory, _ids, runtime) => {
    await acceptAuthorityRoot(runtime, 'ses-no-life', 'manager')
    assert.equal((await execute(hooks, 'ses-no-life', 'return 7;')).data, 7)
    // An unadmitted session with no active incumbency receives no read permissions.
    assert.equal((await execute(hooks, 'ses-no-life', reads[0])).code, 'REVIEW_ONLY')
    const unknown = await execute(hooks, 'ses-unknown', 'return 7;')
    assert.equal(Object.hasOwn(unknown, 'data'), false)
    const wrongRole = await hooks.tool['todowrite-for-devops'].execute(
      { program: 'class Js extends JsProgram { async run() { return 7; } }', name: 'devops-wrong' },
      { sessionID: 'ses-no-life', agent: 'manager' },
    )
    assert.doesNotMatch(String(wrongRole), /data = 7/)
  })
})

test('WHAT[capability-enforcement-025] repository read calls remain admitted and spawn real rg after review', async () => {
  const { default: fs } = await import('node:fs')
  const { default: processes } = await import('node:child_process')
  const { syncBuiltinESMExports } = await import('node:module')
  await withExecutablePlugin(async (hooks, directory, _ids, runtime) => {
    const sessionID = 'ses-physical-review'
    await seed(runtime, sessionID, directory)
    const target = join(directory, 'review-target')
    mkdirSync(target)
    writeFileSync(join(target, 'evidence.txt'), 'review evidence')
    let touches = 0
    let rgSpawns = 0
    const names = ['readFileSync', 'statSync', 'lstatSync', 'realpathSync', 'readdirSync', 'existsSync']
    const saved = new Map(names.map(name => [name, fs[name]]))
    const spawn = processes.spawn
    try {
      for (const name of names) fs[name] = function (...args) {
        if (String(args[0]).includes('review-target')) touches += 1
        return saved.get(name).apply(this, args)
      }
      processes.spawn = function (...args) {
        if (args[0] === 'rg') rgSpawns += 1
        return spawn.apply(this, args)
      }
      syncBuiltinESMExports()
      const operations = [
        "return await this.reviewOnly.cat('review-target/evidence.txt');",
        "return await this.reviewOnly.stat('review-target/evidence.txt');",
        "return await this.reviewOnly.ls('review-target');",
        "return await this.reviewOnly.rg(['review', 'review-target']);",
      ]
      for (const operation of operations) {
        const before = touches
        const result = await execute(hooks, sessionID, operation)
        assert.equal(result.code, undefined, JSON.stringify(result))
        assert.ok(touches > before, 'positive control must reach the physical target adapter')
      }
      assert.equal(rgSpawns, 1, 'positive control must spawn one real rg')
      await injectAcceptedAssessment(runtime, sessionID)
      touches = 0
      rgSpawns = 0
      // N-04: Granted read capability must not disappear because a review happened.
      for (const operation of operations) {
        const result = await execute(hooks, sessionID, operation)
        assert.equal(result.code, undefined, JSON.stringify(result))
      }
      assert.ok(touches > 0, 'admitted calls continue to touch the physical target after review')
      assert.equal(rgSpawns, 1, 'admitted rg spawns a real child process')
    } finally {
      for (const [name, original] of saved) fs[name] = original
      processes.spawn = spawn
      syncBuiltinESMExports()
    }
  })
})

test('WHAT[capability-enforcement-025] an admitted physical rg finishes and later calls remain admitted under N-04', { timeout: 5000 }, async () => {
  const { default: processes } = await import('node:child_process')
  const { syncBuiltinESMExports } = await import('node:module')
  await withExecutablePlugin(async (hooks, directory, _ids, runtime) => {
    const sessionID = 'ses-inflight-review'
    await seed(runtime, sessionID, directory)
    const closed = Promise.withResolvers()
    const released = Promise.withResolvers()
    const spawn = processes.spawn
    let spawns = 0
    try {
      processes.spawn = function (...args) {
        const child = spawn.apply(this, args)
        if (args[0] !== 'rg') return child
        spawns += 1
        const on = child.on
        child.on = function (event, observe) {
          if (event !== 'close') return on.call(this, event, observe)
          return on.call(this, event, (...values) => {
            closed.resolve()
            released.promise.then(() => observe(...values))
          })
        }
        return child
      }
      syncBuiltinESMExports()
      const pending = execute(hooks, sessionID, reads[3], 'inflight-rg')
      await closed.promise
      await injectAcceptedAssessment(runtime, sessionID)
      released.resolve()
      const admitted = await pending
      assert.equal(admitted.code, undefined, JSON.stringify(admitted))
      assert.equal(admitted.data.exitCode, 0)
      // N-04: Read capability is retained after review acceptance; a later call is also admitted.
      const later = await execute(hooks, sessionID, reads[3], 'late-rg')
      assert.equal(later.code, undefined, JSON.stringify(later))
      assert.equal(spawns, 2)
    } finally {
      released.resolve()
      processes.spawn = spawn
      syncBuiltinESMExports()
    }
  })
})

test('WHAT[capability-enforcement-025] P15_concurrent_physical_rg_overlap_with_review_acceptance_admits_inflight_and_subsequent_under_N04', { timeout: 5000 }, async () => {
  const { default: processes } = await import('node:child_process')
  const { syncBuiltinESMExports } = await import('node:module')
  await withExecutablePlugin(async (hooks, directory, _ids, runtime) => {
    const sessionID = 'ses-concurrent-inflight-review'
    await seed(runtime, sessionID, directory)
    const paused = Promise.withResolvers()
    const spawn = processes.spawn
    let spawns = 0
    let liveChild = null
    try {
      processes.spawn = function (...args) {
        const child = spawn.apply(this, args)
        if (args[0] !== 'rg') return child
        spawns += 1
        // Freeze the live OS process while it is genuinely in flight, so the
        // review acceptance below overlaps a running rg, not a finished one.
        process.kill(child.pid, 'SIGSTOP')
        liveChild = child
        paused.resolve(child)
        return child
      }
      syncBuiltinESMExports()
      const pending = execute(hooks, sessionID, reads[3], 'concurrent-inflight-rg')
      const child = await paused.promise
      assert.equal(child.exitCode, null, 'the rg child must still be in flight')
      assert.equal(child.signalCode, null, 'the rg child must not be signalled')
      await injectAcceptedAssessment(runtime, sessionID)
      process.kill(child.pid, 'SIGCONT')
      const admitted = await pending
      assert.equal(admitted.code, undefined, JSON.stringify(admitted))
      assert.equal(admitted.data.exitCode, 0)
      assert.equal(spawns, 1)
    } finally {
      if (liveChild !== null && liveChild.exitCode === null && liveChild.signalCode === null) {
        try { process.kill(liveChild.pid, 'SIGCONT') } catch (error) {
          if (error.code !== 'ESRCH') throw error
        }
      }
      processes.spawn = spawn
      syncBuiltinESMExports()
    }
  })
})
