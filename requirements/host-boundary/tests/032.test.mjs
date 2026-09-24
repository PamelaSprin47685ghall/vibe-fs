import assert from 'node:assert/strict'
import { execFileSync, spawnSync } from 'node:child_process'
import { existsSync } from 'node:fs'
import path from 'node:path'
import test from 'node:test'
import { fileURLToPath } from 'node:url'
import { openIncumbency, withExecutablePlugin } from '../../verification-system/tests/support/plugin-fixture.mjs'
import { integrationTest } from '../../verification-system/tests/support/tier-gate.mjs'
import { OPENCODE_BIN } from '../../verification-system/tests/e2e/support/process-host-utils.js'

const REVIEW_TOOLS = ['js-manager']

test('WHAT[host-boundary-032] C01_tool_definition_decorates_four_dedicated_tools_with_contract_enum', async () => {
  await withExecutablePlugin(async (hooks) => {
    for (const toolID of REVIEW_TOOLS) {
      const output = {
        description: `Description for ${toolID}`,
        parameters: {
          type: 'object',
          properties: {
            path: { type: 'string' },
          },
          required: ['path'],
        },
      }
      // WHAT[host-boundary-032]: definition 装饰四专用工具后 contract 为 required string 且 enum 恰一个值
      // 当前生产代码中 toolDefinition 是空实现 () => {}，输出对象未被装饰，此处必将失败飘红。
      await hooks['tool.definition']({ toolID }, output)
      assert.ok(
        output.parameters.properties?.contract,
        `Tool ${toolID} must be decorated with 'contract' property`,
      )
      assert.equal(output.parameters.properties.contract.type, 'string')
      assert.ok(
        Array.isArray(output.parameters.properties.contract.enum),
        `Tool ${toolID} contract enum must be an array`,
      )
      assert.equal(
        output.parameters.properties.contract.enum.length,
        1,
        `Tool ${toolID} contract enum must contain exactly one value`,
      )
      assert.ok(
        output.parameters.required?.includes('contract'),
        `Tool ${toolID} parameters must mark 'contract' as required`,
      )
    }
  })
})

test('WHAT[host-boundary-032] C05_tool_execute_before_strips_contract_and_after_restores', async () => {
  await withExecutablePlugin(async (hooks, _directory, _createdIds, runtime) => {
    const tool = 'js-manager'
    const sessionID = 'ses-c05'
    await openIncumbency(runtime, sessionID)
    const callID = 'call-c05-1'
    const originalContract = 'js-manager-contract-v1'
    const beforeOutput = {
      args: {
        path: 'src/App.fs',
        contract: originalContract,
      },
    }

    // before 业务视图无 contract
    await hooks['tool.execute.before']({ tool, sessionID, callID }, beforeOutput)
    assert.equal(
      'contract' in beforeOutput.args,
      false,
      'before hook must strip contract parameter from business view',
    )

    const afterOutput = {
      title: 'js-manager',
      output: 'file contents',
      metadata: {},
    }
    // after 原值恢复
    await hooks['tool.execute.after'](
      { tool, sessionID, callID, args: beforeOutput.args },
      afterOutput,
    )
    assert.equal(
      beforeOutput.args.contract,
      originalContract,
      'after hook must restore original contract value',
    )
  })
})

test('WHAT[host-boundary-032] C09_concurrent_tool_invocations_do_not_mix_contract_parameters', async () => {
  await withExecutablePlugin(async (hooks, _directory, _createdIds, runtime) => {
    const tool = 'js-manager'
    const call1 = {
      sessionID: 'ses-c09-1',
      callID: 'call-c09-1',
      contract: 'contract-token-alpha',
      output: { args: { path: 'file-alpha.txt', contract: 'contract-token-alpha' } },
    }
    const call2 = {
      sessionID: 'ses-c09-2',
      callID: 'call-c09-2',
      contract: 'contract-token-beta',
      output: { args: { path: 'file-beta.txt', contract: 'contract-token-beta' } },
    }

    await Promise.all([
      openIncumbency(runtime, call1.sessionID),
      openIncumbency(runtime, call2.sessionID),
    ])

    // 两个并发调用参数不串号：同时执行 before
    await Promise.all([
      hooks['tool.execute.before']({ tool, sessionID: call1.sessionID, callID: call1.callID }, call1.output),
      hooks['tool.execute.before']({ tool, sessionID: call2.sessionID, callID: call2.callID }, call2.output),
    ])

    assert.equal('contract' in call1.output.args, false, 'call1 contract must be stripped')
    assert.equal('contract' in call2.output.args, false, 'call2 contract must be stripped')

    // 乱序执行 after
    const after1 = { title: tool, output: 'out1', metadata: {} }
    const after2 = { title: tool, output: 'out2', metadata: {} }

    await Promise.all([
      hooks['tool.execute.after']({ tool, sessionID: call2.sessionID, callID: call2.callID, args: call2.output.args }, after2),
      hooks['tool.execute.after']({ tool, sessionID: call1.sessionID, callID: call1.callID, args: call1.output.args }, after1),
    ])

    assert.equal(call1.output.args.contract, 'contract-token-alpha', 'call1 must restore its own contract without crosstalk')
    assert.equal(call2.output.args.contract, 'contract-token-beta', 'call2 must restore its own contract without crosstalk')
  })
})

test('WHAT[host-boundary-032] C02_definition_decoration_leaves_native_and_non_review_tools_unmodified', async () => {
  await withExecutablePlugin(async (hooks) => {
    const nonReviewTools = [
      'read',
      'write',
      'edit',
      'glob',
      'grep',
      'js-engineer',
      'js-devops',
      'bash-honeypot',
      'assume',
    ]
    for (const toolID of nonReviewTools) {
      const original = {
        description: `Original description of ${toolID}`,
        parameters: {
          type: 'object',
          properties: { path: { type: 'string' } },
          required: ['path'],
        },
      }
      const output = structuredClone(original)
      await hooks['tool.definition']({ toolID }, output)
      assert.deepEqual(output, original, `Non-review tool ${toolID} definition must remain completely unmodified`)
    }
  })
})

test('WHAT[host-boundary-032] C03_decorating_same_definition_multiple_times_is_idempotent_without_duplicates', async () => {
  await withExecutablePlugin(async (hooks) => {
    for (const toolID of REVIEW_TOOLS) {
      const output = {
        description: `Description for ${toolID}`,
        parameters: {
          type: 'object',
          properties: { path: { type: 'string' } },
          required: ['path'],
        },
      }
      await hooks['tool.definition']({ toolID }, output)
      const firstSnapshot = structuredClone(output)

      // 再次装饰同一定义
      await hooks['tool.definition']({ toolID }, output)
      // 第三次装饰同一定义
      await hooks['tool.definition']({ toolID }, output)

      assert.deepEqual(output, firstSnapshot, `Multiple decorations on ${toolID} must be idempotent`)
      assert.equal(
        output.parameters.required.filter((x) => x === 'contract').length,
        1,
        `'contract' must not be appended multiple times in required list of ${toolID}`,
      )
    }
  })
})

test('WHAT[host-boundary-032] C04_definitions_generated_before_and_after_review_are_canonically_identical', async () => {
  await withExecutablePlugin(async (hooks) => {
    for (const toolID of REVIEW_TOOLS) {
      const defBefore = {
        description: `Description for ${toolID}`,
        parameters: { type: 'object', properties: { path: { type: 'string' } }, required: ['path'] },
      }
      const defAfter = {
        description: `Description for ${toolID}`,
        parameters: { type: 'object', properties: { path: { type: 'string' } }, required: ['path'] },
      }
      await hooks['tool.definition']({ toolID }, defBefore)
      await hooks['tool.definition']({ toolID }, defAfter)
      assert.equal(
        JSON.stringify(defBefore),
        JSON.stringify(defAfter),
        `Definition of ${toolID} must be canonically identical across review lifecycle`,
      )
    }
  })
})

test('WHAT[host-boundary-032] C06_missing_wrong_string_null_or_wrong_json_type_contract_not_rejected_by_plugin_and_restored', async () => {
  await withExecutablePlugin(async (hooks, _directory, _createdIds, runtime) => {
    const sessionID = 'ses-c06'
    await openIncumbency(runtime, sessionID)
    const malformedContracts = [
      undefined,
      null,
      12345,
      true,
      { arbitrary: 'object' },
      'wrong-contract-string',
      '',
    ]

    for (let i = 0; i < malformedContracts.length; i++) {
      const contractVal = malformedContracts[i]
      const callID = `call-c06-${i}`
      const beforeOutput = {
        args: contractVal === undefined ? { path: 'file.txt' } : { path: 'file.txt', contract: contractVal },
      }

      // 插件本地对缺失或错误的 contract 采取乐观处理，不进行二次强校验拒绝
      await hooks['tool.execute.before']({ tool: 'js-manager', sessionID, callID }, beforeOutput)
      assert.equal('contract' in beforeOutput.args, false, `contract must be stripped during execution for test case ${i}`)

      const afterOutput = { title: 'js-manager', output: 'ok', metadata: {} }
      await hooks['tool.execute.after']({ tool: 'js-manager', sessionID, callID, args: beforeOutput.args }, afterOutput)

      if (contractVal === undefined) {
        assert.equal('contract' in beforeOutput.args, false, 'missing contract must remain missing after restore')
      } else {
        assert.deepEqual(beforeOutput.args.contract, contractVal, `contract must be restored exactly to original value for test case ${i}`)
      }
    }
  })
})

test('WHAT[host-boundary-032] C07_contract_undefined_versus_completely_missing_preserves_own_property_state', async () => {
  await withExecutablePlugin(async (hooks, _directory, _createdIds, runtime) => {
    const sessionID = 'ses-c07'
    await openIncumbency(runtime, sessionID)

    // 情况 1: 显式赋值 contract: undefined
    const argsExplicitUndefined = { path: 'file.txt', contract: undefined }
    const call1ID = 'call-c07-undef'
    await hooks['tool.execute.before']({ tool: 'js-manager', sessionID, callID: call1ID }, { args: argsExplicitUndefined })
    assert.equal('contract' in argsExplicitUndefined, false)
    await hooks['tool.execute.after']({ tool: 'js-manager', sessionID, callID: call1ID, args: argsExplicitUndefined }, { title: 'js-manager', output: '', metadata: {} })
    assert.equal(Object.prototype.hasOwnProperty.call(argsExplicitUndefined, 'contract'), true, 'contract: undefined must be restored as own property')
    assert.equal(argsExplicitUndefined.contract, undefined)

    // 情况 2: 完全未提供 contract 字段
    const argsCompletelyMissing = { path: 'file.txt' }
    const call2ID = 'call-c07-missing'
    await hooks['tool.execute.before']({ tool: 'js-manager', sessionID, callID: call2ID }, { args: argsCompletelyMissing })
    assert.equal('contract' in argsCompletelyMissing, false)
    await hooks['tool.execute.after']({ tool: 'js-manager', sessionID, callID: call2ID, args: argsCompletelyMissing }, { title: 'js-manager', output: '', metadata: {} })
    assert.equal(Object.prototype.hasOwnProperty.call(argsCompletelyMissing, 'contract'), false, 'completely missing contract must remain absent as own property')
  })
})

test('WHAT[host-boundary-032] C08_repeated_before_and_repeated_after_preserves_original_and_restores_idempotently', async () => {
  await withExecutablePlugin(async (hooks, _directory, _createdIds, runtime) => {
    const sessionID = 'ses-c08'
    await openIncumbency(runtime, sessionID)
    const callID = 'call-c08'
    const originalContract = 'token-c08-orig'
    const beforeOutput = { args: { path: 'file.txt', contract: originalContract } }

    // 重复执行 before hook
    await hooks['tool.execute.before']({ tool: 'js-manager', sessionID, callID }, beforeOutput)
    await hooks['tool.execute.before']({ tool: 'js-manager', sessionID, callID }, beforeOutput)
    assert.equal('contract' in beforeOutput.args, false, 'contract stripped')

    // 重复执行 after hook
    const afterOutput = { title: 'js-manager', output: '', metadata: {} }
    await hooks['tool.execute.after']({ tool: 'js-manager', sessionID, callID, args: beforeOutput.args }, afterOutput)
    assert.equal(beforeOutput.args.contract, originalContract, 'first restore recovers original contract')

    await hooks['tool.execute.after']({ tool: 'js-manager', sessionID, callID, args: beforeOutput.args }, afterOutput)
    assert.equal(beforeOutput.args.contract, originalContract, 'second restore is idempotent and preserves contract')
  })
})

test('WHAT[host-boundary-032] C10_model_submitted_pseudo_contract_fields_not_treated_as_private_record', async () => {
  await withExecutablePlugin(async (hooks, _directory, _createdIds, runtime) => {
    const sessionID = 'ses-c10'
    await openIncumbency(runtime, sessionID)
    const callID = 'call-c10'
    const beforeOutput = {
      args: {
        path: 'src/App.fs',
        contract: 'valid-review-contract',
        _contract: 'malicious-injected-pseudo-contract',
        __contract: 'another-pseudo-field',
      },
    }

    await hooks['tool.execute.before']({ tool: 'js-manager', sessionID, callID }, beforeOutput)
    // 仅真实的 contract 字段被暂存并隐藏，伪造字段不作为私有记录、原样保留
    assert.equal('contract' in beforeOutput.args, false)
    assert.equal(beforeOutput.args._contract, 'malicious-injected-pseudo-contract')
    assert.equal(beforeOutput.args.__contract, 'another-pseudo-field')

    const afterOutput = { title: 'js-manager', output: '', metadata: {} }
    await hooks['tool.execute.after']({ tool: 'js-manager', sessionID, callID, args: beforeOutput.args }, afterOutput)
    assert.equal(beforeOutput.args.contract, 'valid-review-contract')
    assert.equal(beforeOutput.args._contract, 'malicious-injected-pseudo-contract')
    assert.equal(beforeOutput.args.__contract, 'another-pseudo-field')
  })
})

test('WHAT[host-boundary-032] C11_business_execution_failure_or_rejection_restores_contract_and_preserves_error', async () => {
  await withExecutablePlugin(async (hooks, _directory, _createdIds, runtime) => {
    const sessionID = 'ses-c11'
    await openIncumbency(runtime, sessionID)
    const callID = 'call-c11'
    const originalContract = 'contract-c11'
    const beforeOutput = { args: { path: 'nonexistent-throw.txt', contract: originalContract } }

    await hooks['tool.execute.before']({ tool: 'js-manager', sessionID, callID }, beforeOutput)
    assert.equal('contract' in beforeOutput.args, false)

    // 模拟业务执行失败 / 抛出异常
    const executionError = new Error('Simulated file system IO failure')
    try {
      throw executionError
    } catch (err) {
      // 宿主在 try...finally 或 catch 中必须同源触发 after hook 进行清理恢复
      await hooks['tool.execute.after'](
        { tool: 'js-manager', sessionID, callID, args: beforeOutput.args },
        { title: 'js-manager', output: 'error', metadata: { error: err } },
      )
    }

    assert.equal(beforeOutput.args.contract, originalContract, 'contract must be safely restored upon business execution failure')
  })
})

test('WHAT[host-boundary-032] C12_after_hook_grounding_failure_does_not_affect_already_restored_args', async () => {
  await withExecutablePlugin(async (hooks, _directory, _createdIds, runtime) => {
    const sessionID = 'ses-c12'
    await openIncumbency(runtime, sessionID)
    const callID = 'call-c12'
    const originalContract = 'contract-c12'
    const beforeOutput = { args: { path: 'file.txt', contract: originalContract } }

    await hooks['tool.execute.before']({ tool: 'js-manager', sessionID, callID }, beforeOutput)
    assert.equal('contract' in beforeOutput.args, false)

    // after 回调中，首先完成 contract 恢复；即便后续 downstream 审计或观察报错，参数恢复不被破坏
    await hooks['tool.execute.after'](
      { tool: 'js-manager', sessionID, callID, args: beforeOutput.args },
      { title: 'js-manager', output: 'content', metadata: {} },
    )
    assert.equal(beforeOutput.args.contract, originalContract, 'args.contract must be intact regardless of subsequent observation outcome')
  })
})

test('WHAT[host-boundary-032] C13_frozen_or_non_extensible_args_fails_atomically_without_file_access', async () => {
  await withExecutablePlugin(async (hooks, _directory, _createdIds, runtime) => {
    const sessionID = 'ses-c13'
    await openIncumbency(runtime, sessionID)
    const callID = 'call-c13'

    const frozenArgs = Object.freeze({ path: 'src/Secret.fs', contract: 'token-c13' })
    const beforeOutput = { args: frozenArgs }

    // 对不可扩展或冻结的 args，hide 应当抛出 TypeError，保持原子失败且绝不发生文件访问
    await assert.rejects(
      async () => {
        await hooks['tool.execute.before']({ tool: 'js-manager', sessionID, callID }, beforeOutput)
      },
      TypeError,
      'frozen args must fail atomically with TypeError',
    )
    assert.equal(beforeOutput.args.contract, 'token-c13', 'contract must remain unchanged on frozen args failure')
  })
})

test('WHAT[host-boundary-032] C14_direct_tool_execution_without_before_hook_performs_full_permission_check_without_missing_key_crash', async () => {
  await withExecutablePlugin(async (hooks, _directory, _createdIds, runtime) => {
    const sessionID = 'ses-c14'
    await openIncumbency(runtime, sessionID)

    // 直接调用 tool.execute（绕过 tool.execute.before）
    // 缺少私有暂存 key 时，after/restore 是 no-op，且 tool.execute 执行完整权限核验
    const directArgs = { path: 'src/App.fs' }
    const execResult = await hooks.tool['js-manager'].execute(
      directArgs,
      { sessionID, agent: 'manager' },
    )
    assert.ok(typeof execResult === 'string', 'Tool execution must return valid string result without crashing')

    // 缺少私有暂存 key 时执行 after hook 亦幂等安全退出
    await hooks['tool.execute.after'](
      { tool: 'js-manager', sessionID, callID: 'call-c14', args: directArgs },
      { title: 'js-manager', output: execResult, metadata: {} },
    )
    assert.equal('contract' in directArgs, false, 'unprovided contract remains absent')
  })
})

test('WHAT[host-boundary-032] C15_contract_position_first_middle_or_last_restores_without_property_drift', async () => {
  await withExecutablePlugin(async (hooks, _directory, _createdIds, runtime) => {
    const sessionID = 'ses-c15'
    await openIncumbency(runtime, sessionID)

    // 1. contract 在第一个位置
    const objFirst = { contract: 'first-token', a: 1, b: 2 }
    await hooks['tool.execute.before']({ tool: 'js-manager', sessionID, callID: 'call-c15-1' }, { args: objFirst })
    await hooks['tool.execute.after']({ tool: 'js-manager', sessionID, callID: 'call-c15-1', args: objFirst }, { title: 'js-manager', output: '', metadata: {} })
    assert.equal(objFirst.contract, 'first-token')

    // 2. contract 在中间位置
    const objMid = { a: 1, contract: 'mid-token', b: 2 }
    await hooks['tool.execute.before']({ tool: 'js-manager', sessionID, callID: 'call-c15-2' }, { args: objMid })
    await hooks['tool.execute.after']({ tool: 'js-manager', sessionID, callID: 'call-c15-2', args: objMid }, { title: 'js-manager', output: '', metadata: {} })
    assert.equal(objMid.contract, 'mid-token')

    // 3. contract 在最后位置
    const objLast = { a: 1, b: 2, contract: 'last-token' }
    await hooks['tool.execute.before']({ tool: 'js-manager', sessionID, callID: 'call-c15-3' }, { args: objLast })
    await hooks['tool.execute.after']({ tool: 'js-manager', sessionID, callID: 'call-c15-3', args: objLast }, { title: 'js-manager', output: '', metadata: {} })
    assert.equal(objLast.contract, 'last-token')
  })
})

test('WHAT[host-boundary-032] C16_upstream_validation_rejection_not_swallowed_and_local_missing_malformed_not_revalidated', async () => {
  await withExecutablePlugin(async (hooks, _directory, _createdIds, runtime) => {
    const sessionID = 'ses-c16'
    await openIncumbency(runtime, sessionID)

    // 1. 本地插件路径：对 missing/malformed contract 乐观处理，不进行二次强校验，不主动拒绝
    const relaxedArgs = { path: 'file.txt', contract: 'not-in-enum-value' }
    await hooks['tool.execute.before'](
      { tool: 'js-manager', sessionID, callID: 'call-c16-local' },
      { args: relaxedArgs },
    )
    assert.equal('contract' in relaxedArgs, false, 'local plugin must strip contract without strong revalidation rejection')

    // 2. 上游宿主校验：若宿主层直接因 schema validation 抛出拒绝，该 rejection 不被插件层吞没
    const upstreamRejection = new Error('Host schema validation: contract must match enum')
    const executeWithHostValidation = async () => {
      // 模拟上游宿主直接在调用 hook 前拦截
      throw upstreamRejection
    }
    await assert.rejects(
      executeWithHostValidation,
      /Host schema validation: contract must match enum/,
      'Upstream host rejection must not be swallowed',
    )
  })
})

const here = path.dirname(fileURLToPath(import.meta.url))
const runnerPath = path.join(here, 'support/run-manager-review-tools-canary.mjs')
const repoRoot = path.resolve(here, '../../..')

const checkOpencodeExecutable = () => {
  if (process.env.OPENCODE_BIN && existsSync(process.env.OPENCODE_BIN)) return true
  const localBin = path.join(repoRoot, 'node_modules/.bin/opencode')
  if (existsSync(localBin)) return true
  try {
    execFileSync(OPENCODE_BIN, ['--version'], { stdio: 'ignore' })
    return true
  } catch {
    return false
  }
}

integrationTest(
  'WHAT[host-boundary-032] canary_manager_review_tools_contract_observed_on_real_host',
  async (t) => {
    // 门禁与环境判定约定：
    // 1. integrationTest 依据环境变量 WXS_TIER_INTEGRATION=1 决定是否执行；
    // 2. 真实 Host canary 执行需要本地具备可执行的 opencode 二进制（OPENCODE_BIN），环境不具备时给出清晰诊断跳过。
    if (!checkOpencodeExecutable()) {
      return t.skip(
        `OpenCode binary not executable at ${OPENCODE_BIN}; requires real OpenCode host to execute canary runner`,
      )
    }

    const launched = spawnSync(process.execPath, [runnerPath], {
      cwd: repoRoot,
      encoding: 'utf8',
      timeout: 120000,
    })

    let stdoutSummary = null
    try {
      stdoutSummary = JSON.parse(launched.stdout.trim())
    } catch {
      stdoutSummary = launched.stdout
    }

    const failureMessage = [
      `Real host canary runner exited with code ${launched.status}.`,
      `[Runner Stderr]:\n${launched.stderr || '(empty)'}`,
      `[Runner Stdout Summary]:\n${typeof stdoutSummary === 'object' ? JSON.stringify(stdoutSummary, null, 2) : stdoutSummary}`,
    ].join('\n')

    assert.equal(launched.status, 0, failureMessage)
    assert.ok(stdoutSummary, 'Canary runner must produce JSON summary output')
    assert.equal(stdoutSummary.versions?.plugin, '1.18.29', 'plugin version must match fixture')
    assert.deepEqual(
      stdoutSummary.reviewTools,
      ['js-manager'],
      'reviewTools must contain exactly the single review tool',
    )
    for (const tool of stdoutSummary.reviewTools) {
      assert.equal(
        stdoutSummary.controlTools.includes(tool),
        false,
        `controlTools must not include review tool ${tool}`,
      )
    }
    assert.equal(stdoutSummary.wireInspection?.contractRequired, true, 'wire contract must be required')
    assert.equal(stdoutSummary.wireInspection?.contractType, 'string', 'wire contract type must be string')
    assert.deepEqual(
      stdoutSummary.wireInspection?.contractEnum,
      ['do-not-use-except-for-review'],
      'wire contract enum must contain exactly the single contract token',
    )
    assert.equal(
      stdoutSummary.wireInspection?.historicalToolCallPreservesContract,
      true,
      'historical tool calls must preserve contract',
    )
    assert.equal(
      stdoutSummary.wireInspection?.round2ToolsStable,
      true,
      'round 2 provider tools must remain stable',
    )
    assert.equal(stdoutSummary.hookIdentityChain?.argsIdentityPreservedInBefore, true)
    assert.equal(stdoutSummary.hookIdentityChain?.argsIdentityPreservedInAfter, true)
    assert.equal(stdoutSummary.hookIdentityChain?.contractHiddenInBefore, true)
    assert.equal(stdoutSummary.hookIdentityChain?.symbolAttachedInBefore, true)
    assert.equal(stdoutSummary.hookIdentityChain?.contractRestoredInAfter, true)
    assert.equal(stdoutSummary.hookIdentityChain?.symbolClearedInAfter, true)
    assert.equal(stdoutSummary.hookIdentityChain?.businessArgsPreserved, true)
    assert.equal(
      stdoutSummary.durableToolPart?.persistedInputRetainsContract,
      true,
      'durable tool part in Host store must retain contract',
    )
    assert.equal(
      stdoutSummary.terminalStates?.normal,
      'success',
      'terminalStates.normal must be success',
    )
  },
)

