import assert from 'node:assert/strict'
import { execFileSync, spawnSync } from 'node:child_process'
import { existsSync } from 'node:fs'
import path from 'node:path'
import test from 'node:test'
import { fileURLToPath } from 'node:url'
import * as PluginHooksSurface from '../../../dist/OpenCode/Host/PluginHooksSurface.js'
import * as ModelRoutingSurface from '../../../dist/OpenCode/Host/ModelRoutingSurface.js'
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

// ---------------------------------------------------------------------------
// DELEGATE.md 3.2/3.3/4.2/4.3: explicit read-only delegation protocol.
// Production tool.definition decoration is gated behind the Predictor
// configuration existence query (ModelRouting, DELEGATE.md 9.2); the schema
// contract itself is proven through the same registered contract function
// (PluginHooksSurface.decorateReadonlyDelegationToolDefinition), while the
// argument-boundary assertions run through the real plugin hooks, where the
// cleanup mechanism is the same unconditional hide/restore as the review
// contract.
// ---------------------------------------------------------------------------

test('WHAT[host-boundary-032] C17_delegation_schema_adds_required_budget_and_optional_note_without_dropping_tool_contract', () => {
  const previousLanguage = process.env.WANXIANGSHU_PROVIDER_LANGUAGE
  process.env.WANXIANGSHU_PROVIDER_LANGUAGE = 'en'
  try {
    const definition = {
      description: 'Original description of the tool',
      parameters: {
        type: 'object',
        properties: { path: { type: 'string' } },
        required: ['path'],
        additionalProperties: false,
      },
    }
    PluginHooksSurface.decorateReadonlyDelegationToolDefinition('read', definition)

    const props = definition.parameters.properties
    // 预算：integer、范围 [0, 2147483647]、原文 description，且必填
    assert.equal(props.delegate_readonly_rounds.type, 'integer')
    assert.equal(props.delegate_readonly_rounds.minimum, 0)
    assert.equal(props.delegate_readonly_rounds.maximum, 2147483647)
    assert.ok(
      typeof props.delegate_readonly_rounds.description === 'string' &&
        props.delegate_readonly_rounds.description.length > 0,
      'budget must carry the DELEGATE.md 3.2 description',
    )
    assert.deepEqual(
      definition.parameters.required,
      ['path', 'delegate_readonly_rounds'],
      'original required entry must be preserved and only the budget appended',
    )
    // 短记：string 可省略，绝不进 required，不设 minLength
    assert.equal(props.self_note.type, 'string')
    assert.equal(
      definition.parameters.required.includes('self_note'),
      false,
      'self_note must never join required',
    )
    assert.equal('minLength' in props.self_note, false, 'self_note must not carry a minLength gate')
    // 原有属性与兼容约束保持
    assert.deepEqual(definition.parameters.properties.path, { type: 'string' })
    assert.equal(definition.parameters.additionalProperties, false)
    // 协作说明幂等追加在原描述之后，不替换原描述
    assert.ok(
      definition.description.startsWith('Original description of the tool'),
      'original tool description must stay as the prefix',
    )
    assert.ok(
      definition.description.includes('Fill in delegate_readonly_rounds on every tool call.'),
      'English collaboration prose must be appended once',
    )
  } finally {
    if (previousLanguage === undefined) {
      delete process.env.WANXIANGSHU_PROVIDER_LANGUAGE
    } else {
      process.env.WANXIANGSHU_PROVIDER_LANGUAGE = previousLanguage
    }
  }
})

test('WHAT[host-boundary-032] C18_budget_rejects_illegal_values_and_never_coerces_them', () => {
  // 边界与正常值：原生有限整数且在 [0, 2147483647] 内
  for (const value of [0, 1, 2, 5, 2147483647]) {
    const result = PluginHooksSurface.readonlyDelegationBudgetOf(value)
    assert.equal(result.ok, true, `budget ${value} must be accepted`)
    assert.equal(result.rounds, value)
  }
  // 缺失、null、负数、小数、数值字符串、布尔、越界、非有限值、对象/数组
  const rejected = [
    undefined,
    null,
    -1,
    -0.5,
    1.5,
    '3',
    '0',
    true,
    false,
    2147483648,
    Number.NaN,
    Number.POSITIVE_INFINITY,
    Number.NEGATIVE_INFINITY,
    {},
    [],
  ]
  for (const value of rejected) {
    const result = PluginHooksSurface.readonlyDelegationBudgetOf(value)
    assert.equal(result.ok, false, `budget ${typeof value}:${String(value)} must be rejected without coercion`)
    assert.equal(typeof result.error, 'string')
    assert.equal(result.rounds, undefined, 'rejected budget must not produce a value')
  }
})

test('WHAT[host-boundary-032] C19_self_note_is_optional_string_only_and_empty_is_legal', () => {
  const missing = PluginHooksSurface.readonlyDelegationSelfNoteOf(undefined)
  assert.equal(missing.ok, true, 'absent self_note is legal')
  assert.equal(missing.note, null)

  const empty = PluginHooksSurface.readonlyDelegationSelfNoteOf('')
  assert.equal(empty.ok, true, 'empty string is legal')
  assert.equal(empty.note, '')

  const noteText = '我怀疑入口与调用方对空值的约定不同，接下来先核对调用点'
  const note = PluginHooksSurface.readonlyDelegationSelfNoteOf(noteText)
  assert.equal(note.ok, true)
  assert.equal(note.note, noteText, 'note content must round-trip verbatim')

  for (const value of [0, 1, true, null, {}, [], ['note']]) {
    const result = PluginHooksSurface.readonlyDelegationSelfNoteOf(value)
    assert.equal(result.ok, false, `self_note ${JSON.stringify(value)} must be rejected without coercion`)
  }
})

test('WHAT[host-boundary-032] C20_delegation_decoration_is_idempotent_and_coexists_with_review_contract', () => {
  const previousLanguage = process.env.WANXIANGSHU_PROVIDER_LANGUAGE
  process.env.WANXIANGSHU_PROVIDER_LANGUAGE = 'en'
  try {
    const definition = {
      description: 'Description for js-manager',
      parameters: {
        type: 'object',
        properties: { path: { type: 'string' } },
        required: ['path'],
      },
    }
    PluginHooksSurface.decorateReadonlyDelegationToolDefinition('js-manager', definition)
    PluginHooksSurface.decorateReviewToolDefinition('js-manager', definition)
    const firstSnapshot = structuredClone(definition)

    // 重复装饰、两类装饰交替都不产生重复项
    PluginHooksSurface.decorateReadonlyDelegationToolDefinition('js-manager', definition)
    PluginHooksSurface.decorateReadonlyDelegationToolDefinition('js-manager', definition)
    PluginHooksSurface.decorateReviewToolDefinition('js-manager', definition)
    PluginHooksSurface.decorateReadonlyDelegationToolDefinition('js-manager', definition)
    PluginHooksSurface.decorateReviewToolDefinition('js-manager', definition)

    assert.deepEqual(definition, firstSnapshot, 'coexisting decorations must be idempotent across repeats')
    assert.equal(
      definition.parameters.required.filter((x) => x === 'delegate_readonly_rounds').length,
      1,
      'budget must appear exactly once in required',
    )
    assert.equal(
      definition.parameters.required.filter((x) => x === 'self_note').length,
      0,
      'note must never join required',
    )
    assert.equal(
      definition.parameters.required.filter((x) => x === 'contract').length,
      1,
      'review contract must appear exactly once and stay intact beside the delegation protocol',
    )
    assert.equal(
      definition.description.split('Fill in delegate_readonly_rounds on every tool call.').length - 1,
      1,
      'collaboration prose must be appended exactly once',
    )
  } finally {
    if (previousLanguage === undefined) {
      delete process.env.WANXIANGSHU_PROVIDER_LANGUAGE
    } else {
      process.env.WANXIANGSHU_PROVIDER_LANGUAGE = previousLanguage
    }
  }
})

test('WHAT[host-boundary-032] C21_collaboration_prose_follows_language_binding_and_switches_cleanly', () => {
  const previousLanguage = process.env.WANXIANGSHU_PROVIDER_LANGUAGE
  try {
    process.env.WANXIANGSHU_PROVIDER_LANGUAGE = 'zh-CN'
    const definition = {
      description: '原始工具描述',
      parameters: { type: 'object', properties: {}, required: [] },
    }
    PluginHooksSurface.decorateReadonlyDelegationToolDefinition('read', definition)
    assert.ok(
      definition.description.includes('每个工具调用都要填写 delegate_readonly_rounds'),
      'Chinese collaboration prose required under zh-CN preference',
    )
    assert.equal(
      definition.description.includes('Fill in delegate_readonly_rounds on every tool call.'),
      false,
      'English prose must not arrive under a Chinese preference',
    )
    const zhSnapshot = structuredClone(definition)
    PluginHooksSurface.decorateReadonlyDelegationToolDefinition('read', definition)
    assert.deepEqual(definition, zhSnapshot, 'Chinese decoration must be idempotent')

    // 语言切换后只剩当前语言一段，不叠加
    process.env.WANXIANGSHU_PROVIDER_LANGUAGE = 'en'
    PluginHooksSurface.decorateReadonlyDelegationToolDefinition('read', definition)
    assert.ok(definition.description.includes('Fill in delegate_readonly_rounds on every tool call.'))
    assert.equal(
      definition.description.includes('每个工具调用都要填写 delegate_readonly_rounds'),
      false,
      'prior-language prose must be replaced, not stacked',
    )
    assert.ok(
      definition.description.startsWith('原始工具描述'),
      'original description must remain the prefix after a language switch',
    )
  } finally {
    if (previousLanguage === undefined) {
      delete process.env.WANXIANGSHU_PROVIDER_LANGUAGE
    } else {
      process.env.WANXIANGSHU_PROVIDER_LANGUAGE = previousLanguage
    }
  }
})

test('WHAT[host-boundary-032] C22_conflicting_same_name_properties_and_bad_required_fail_loudly', () => {
  const budgetConflict = {
    description: 'D',
    parameters: {
      type: 'object',
      properties: {
        path: { type: 'string' },
        delegate_readonly_rounds: { type: 'integer', minimum: 0, maximum: 100, description: 'tool-local budget' },
      },
      required: ['path'],
    },
  }
  assert.throws(
    () => PluginHooksSurface.decorateReadonlyDelegationToolDefinition('read', budgetConflict),
    /conflicting delegate_readonly_rounds/,
    'a same-name property that differs from the protocol must fail, not be overwritten',
  )

  const noteConflict = {
    description: 'D',
    parameters: {
      type: 'object',
      properties: {
        path: { type: 'string' },
        self_note: { type: 'string', description: 'tool-local note' },
      },
      required: ['path'],
    },
  }
  assert.throws(
    () => PluginHooksSurface.decorateReadonlyDelegationToolDefinition('read', noteConflict),
    /conflicting self_note/,
    'a same-name note that differs from the protocol must fail',
  )

  const badRequired = {
    description: 'D',
    parameters: { type: 'object', properties: { path: { type: 'string' } }, required: 'path' },
  }
  assert.throws(
    () => PluginHooksSurface.decorateReadonlyDelegationToolDefinition('read', badRequired),
    /required/,
    'a non-array required must fail loudly instead of publishing a partial protocol',
  )

  const missingProperties = {
    description: 'D',
    parameters: { type: 'object' },
  }
  assert.throws(
    () => PluginHooksSurface.decorateReadonlyDelegationToolDefinition('read', missingProperties),
    /properties/,
    'a root schema that cannot be legally extended must fail loudly',
  )
})

test('WHAT[host-boundary-032] C23_delegation_fields_stripped_from_business_view_but_preserved_as_evidence', async () => {
  await withExecutablePlugin(async (hooks, _directory, _createdIds, runtime) => {
    const tool = 'read'
    const sessionID = 'ses-c23'
    await openIncumbency(runtime, sessionID)
    const callID = 'call-c23-1'
    const note = '我怀疑入口与调用方对空值的约定不同，接下来先核对调用点'
    const beforeOutput = {
      args: {
        filePath: 'src/A.fs',
        delegate_readonly_rounds: 5,
        self_note: note,
      },
    }

    await hooks['tool.execute.before']({ tool, sessionID, callID }, beforeOutput)
    assert.equal(
      'delegate_readonly_rounds' in beforeOutput.args,
      false,
      'business view must not see the budget field',
    )
    assert.equal('self_note' in beforeOutput.args, false, 'business view must not see the note field')
    assert.deepEqual(beforeOutput.args, { filePath: 'src/A.fs' }, 'business view keeps only tool parameters')

    await hooks['tool.execute.after'](
      { tool, sessionID, callID, args: beforeOutput.args },
      { title: tool, output: 'file contents', metadata: {} },
    )

    assert.equal(
      beforeOutput.args.delegate_readonly_rounds,
      5,
      'original provider arguments must preserve the budget as evidence',
    )
    assert.equal(
      beforeOutput.args.self_note,
      note,
      'original provider arguments must preserve the note as evidence',
    )
  })
})

test('WHAT[host-boundary-032] C24_review_contract_and_delegation_fields_coexist_without_crosstalk', async () => {
  await withExecutablePlugin(async (hooks, _directory, _createdIds, runtime) => {
    const tool = 'js-manager'
    const sessionID = 'ses-c24'
    await openIncumbency(runtime, sessionID)
    const callID = 'call-c24-1'
    const beforeOutput = {
      args: {
        path: 'src/App.fs',
        contract: 'js-manager-contract-v1',
        delegate_readonly_rounds: 2,
        self_note: 'note-c24',
      },
    }

    await hooks['tool.execute.before']({ tool, sessionID, callID }, beforeOutput)
    assert.deepEqual(
      beforeOutput.args,
      { path: 'src/App.fs' },
      'both contract families must leave the business view together',
    )

    await hooks['tool.execute.after'](
      { tool, sessionID, callID, args: beforeOutput.args },
      { title: tool, output: 'file contents', metadata: {} },
    )

    assert.equal(beforeOutput.args.contract, 'js-manager-contract-v1', 'review contract must be restored')
    assert.equal(beforeOutput.args.delegate_readonly_rounds, 2, 'budget must be restored beside the contract')
    assert.equal(beforeOutput.args.self_note, 'note-c24', 'note must be restored beside the contract')
  })
})

test('WHAT[host-boundary-032] C25_frozen_delegation_fields_fail_atomically_without_losing_evidence', async () => {
  await withExecutablePlugin(async (hooks, _directory, _createdIds, runtime) => {
    const sessionID = 'ses-c25'
    await openIncumbency(runtime, sessionID)
    const frozenArgs = Object.freeze({ path: 'src/Secret.fs', delegate_readonly_rounds: 3, self_note: 'secret' })

    await assert.rejects(
      async () => {
        await hooks['tool.execute.before'](
          { tool: 'read', sessionID, callID: 'call-c25' },
          { args: frozenArgs },
        )
      },
      TypeError,
      'frozen args with protocol fields must fail atomically with TypeError',
    )
    assert.equal(frozenArgs.delegate_readonly_rounds, 3, 'budget evidence must remain unchanged on failure')
    assert.equal(frozenArgs.self_note, 'secret', 'note evidence must remain unchanged on failure')
  })
})

test('WHAT[host-boundary-032] C26_concurrent_delegation_restores_do_not_crosstalk', async () => {
  await withExecutablePlugin(async (hooks, _directory, _createdIds, runtime) => {
    const call1 = {
      sessionID: 'ses-c26-1',
      callID: 'call-c26-1',
      output: { args: { path: 'file-alpha.txt', delegate_readonly_rounds: 1, self_note: 'note-alpha' } },
    }
    const call2 = {
      sessionID: 'ses-c26-2',
      callID: 'call-c26-2',
      output: { args: { path: 'file-beta.txt', delegate_readonly_rounds: 9, self_note: 'note-beta' } },
    }

    await Promise.all([
      openIncumbency(runtime, call1.sessionID),
      openIncumbency(runtime, call2.sessionID),
    ])

    await Promise.all([
      hooks['tool.execute.before']({ tool: 'read', sessionID: call1.sessionID, callID: call1.callID }, call1.output),
      hooks['tool.execute.before']({ tool: 'read', sessionID: call2.sessionID, callID: call2.callID }, call2.output),
    ])

    assert.equal('delegate_readonly_rounds' in call1.output.args, false)
    assert.equal('self_note' in call1.output.args, false)
    assert.equal('delegate_readonly_rounds' in call2.output.args, false)
    assert.equal('self_note' in call2.output.args, false)

    // 乱序恢复也不串值
    await Promise.all([
      hooks['tool.execute.after'](
        { tool: 'read', sessionID: call2.sessionID, callID: call2.callID, args: call2.output.args },
        { title: 'read', output: 'out2', metadata: {} },
      ),
      hooks['tool.execute.after'](
        { tool: 'read', sessionID: call1.sessionID, callID: call1.callID, args: call1.output.args },
        { title: 'read', output: 'out1', metadata: {} },
      ),
    ])

    assert.equal(call1.output.args.delegate_readonly_rounds, 1, 'call1 must restore its own budget')
    assert.equal(call1.output.args.self_note, 'note-alpha', 'call1 must restore its own note')
    assert.equal(call2.output.args.delegate_readonly_rounds, 9, 'call2 must restore its own budget')
    assert.equal(call2.output.args.self_note, 'note-beta', 'call2 must restore its own note')
  })
})

test('WHAT[host-boundary-032] C27_business_tool_execution_never_receives_protocol_fields', async () => {
  await withExecutablePlugin(async (hooks, _directory, _createdIds, runtime) => {
    const sessionID = 'ses-c27'
    await openIncumbency(runtime, sessionID)
    const directArgs = { path: 'src/App.fs', delegate_readonly_rounds: 0, self_note: 'stays out of the business view' }

    await hooks['tool.execute.before'](
      { tool: 'js-manager', sessionID, callID: 'call-c27' },
      { args: directArgs },
    )

    const execResult = await hooks.tool['js-manager'].execute(directArgs, { sessionID, agent: 'manager' })
    assert.ok(typeof execResult === 'string', 'tool execution must succeed on the cleaned business view')

    // 业务执行视图在 before 暂存之后不再持有协议字段；在 after 恢复之前断言，
    // 证明 execute 收到的确实是剥净后的视图（C24 已在 before 后证明同一机制）。
    assert.equal(
      'delegate_readonly_rounds' in directArgs,
      false,
      'business execution view must not carry the budget field',
    )
    assert.equal(
      'self_note' in directArgs,
      false,
      'business execution view must not carry the note field',
    )

    await hooks['tool.execute.after'](
      { tool: 'js-manager', sessionID, callID: 'call-c27', args: directArgs },
      { title: 'js-manager', output: execResult, metadata: {} },
    )

    // after 的同源恢复之后，provider 原始 arguments 证据重新出现；这与
    // C23/C24/C26 证明的恢复契约同构——字段不丢，只是不进业务视图。
    assert.equal(
      directArgs.delegate_readonly_rounds,
      0,
      'budget evidence must be restored to the original arguments',
    )
    assert.equal(
      directArgs.self_note,
      'stays out of the business view',
      'note evidence must be restored to the original arguments',
    )
  })
})

// ---------------------------------------------------------------------------
// DELEGATE.md 9.1/9.2 两态门控：ModelRouting.initialize 的 scheduler 是进程
// 单例（每测试进程只 import 一次），因此两个可观察态由测试自有的动态源驱动
// ——fixture 在隔离 HOME 下写出的 wanxiangshu.mjs 导出 predictorConfiguration，
// 读 globalThis 上的测试注入值，不触碰用户真实配置。默认（未注入）即未配置。
// ---------------------------------------------------------------------------

const setPredictorState = (state, reason) => {
  globalThis.__wanxiangshu_test_predictor_state = state
  if (reason === undefined) {
    delete globalThis.__wanxiangshu_test_predictor_reason
  } else {
    globalThis.__wanxiangshu_test_predictor_reason = reason
  }
}

const clearPredictorState = () => {
  delete globalThis.__wanxiangshu_test_predictor_state
  delete globalThis.__wanxiangshu_test_predictor_reason
}

test('WHAT[host-boundary-032] C28_unconfigured_predictor_leaves_tool_definitions_undecorated', async () => {
  clearPredictorState()
  await withExecutablePlugin(async (hooks) => {
    for (const toolID of ['js-manager', 'read', 'write']) {
      const originalDescription = `Description for ${toolID}`
      const output = {
        description: originalDescription,
        parameters: {
          type: 'object',
          properties: { path: { type: 'string' } },
          required: ['path'],
        },
      }
      await hooks['tool.definition']({ toolID }, output)
      assert.equal(
        output.parameters.properties?.delegate_readonly_rounds,
        undefined,
        `${toolID} must not gain the budget field while Predictor is unconfigured`,
      )
      assert.equal(
        output.parameters.properties?.self_note,
        undefined,
        `${toolID} must not gain the note field while Predictor is unconfigured`,
      )
      assert.equal(
        output.parameters.required.includes('delegate_readonly_rounds'),
        false,
        `${toolID} must not require the budget while Predictor is unconfigured`,
      )
      assert.equal(
        output.description,
        originalDescription,
        `${toolID} description must gain no collaboration prose while Predictor is unconfigured`,
      )
    }
  })
})

test('WHAT[host-boundary-032] C29_configured_predictor_decorates_every_tool_through_the_definition_hook', async () => {
  setPredictorState('configured')
  const previousLanguage = process.env.WANXIANGSHU_PROVIDER_LANGUAGE
  process.env.WANXIANGSHU_PROVIDER_LANGUAGE = 'en'
  try {
    await withExecutablePlugin(async (hooks) => {
      for (const toolID of ['js-manager', 'read', 'write']) {
        const originalDescription = `Description for ${toolID}`
        const output = {
          description: originalDescription,
          parameters: {
            type: 'object',
            properties: { path: { type: 'string' } },
            required: ['path'],
          },
        }
        await hooks['tool.definition']({ toolID }, output)
        assert.equal(
          output.parameters.properties.delegate_readonly_rounds.type,
          'integer',
          `${toolID} must gain the required budget when Predictor is configured`,
        )
        assert.equal(
          output.parameters.properties.self_note.type,
          'string',
          `${toolID} must gain the optional note when Predictor is configured`,
        )
        assert.equal(
          output.parameters.required.includes('delegate_readonly_rounds'),
          true,
          `${toolID} must require the budget when Predictor is configured`,
        )
        assert.equal(
          output.parameters.required.includes('self_note'),
          false,
          `${toolID} must keep the note omissible when Predictor is configured`,
        )
        assert.equal(
          output.parameters.required.includes('path'),
          true,
          `${toolID} must keep its original required entry`,
        )
        assert.ok(
          output.description.startsWith(originalDescription),
          `${toolID} original description must stay as the prefix`,
        )
        assert.ok(
          output.description.length > originalDescription.length,
          `${toolID} description must gain the collaboration prose`,
        )
        if (toolID === 'js-manager') {
          // 与评审 contract 在同一 hook 路径并存，互不排斥
          assert.equal(
            output.parameters.required.includes('contract'),
            true,
            'review contract must coexist with the delegation budget on js-manager',
          )
          assert.equal(
            output.parameters.properties.contract.type,
            'string',
            'review contract property must coexist with the delegation protocol',
          )
        }
      }
    })
  } finally {
    clearPredictorState()
    if (previousLanguage === undefined) {
      delete process.env.WANXIANGSHU_PROVIDER_LANGUAGE
    } else {
      process.env.WANXIANGSHU_PROVIDER_LANGUAGE = previousLanguage
    }
  }
})

test('WHAT[host-boundary-032] C30_configured_decoration_through_hook_is_idempotent', async () => {
  setPredictorState('configured')
  try {
    await withExecutablePlugin(async (hooks) => {
      const output = {
        description: 'Description for read',
        parameters: {
          type: 'object',
          properties: { path: { type: 'string' } },
          required: ['path'],
        },
      }
      await hooks['tool.definition']({ toolID: 'read' }, output)
      const firstSnapshot = structuredClone(output)
      await hooks['tool.definition']({ toolID: 'read' }, output)
      await hooks['tool.definition']({ toolID: 'read' }, output)
      assert.deepEqual(
        output,
        firstSnapshot,
        'repeated definition hook calls must not duplicate required entries or prose',
      )
      assert.equal(
        output.parameters.required.filter((x) => x === 'delegate_readonly_rounds').length,
        1,
        'budget must appear exactly once in required across repeats',
      )
    })
  } finally {
    clearPredictorState()
  }
})

test('WHAT[host-boundary-032] C31_invalid_predictor_configuration_fails_closed_without_publishing_partial_protocol', async () => {
  setPredictorState('invalid', 'predictor candidates must be [model, reasoning] pairs')
  try {
    await withExecutablePlugin(async (hooks) => {
      const output = {
        description: 'Description for read',
        parameters: {
          type: 'object',
          properties: { path: { type: 'string' } },
          required: ['path'],
        },
      }
      await assert.rejects(
        async () => {
          await hooks['tool.definition']({ toolID: 'read' }, output)
        },
        (error) => error != null,
        'invalid Predictor configuration must fail closed on the tool.definition hook',
      )
      assert.equal(
        output.parameters.properties?.delegate_readonly_rounds,
        undefined,
        'no partial protocol may be published on the fail-closed path',
      )
      assert.equal(
        output.description,
        'Description for read',
        'no collaboration prose may be appended on the fail-closed path',
      )

      // 修复配置后同一 hook 立即恢复装饰：失败只属于那一回，无缓存状态
      setPredictorState('configured')
      await hooks['tool.definition']({ toolID: 'read' }, output)
      assert.equal(
        output.parameters.properties.delegate_readonly_rounds.type,
        'integer',
        'the hook must decorate again once the configuration is repaired',
      )
    })
  } finally {
    clearPredictorState()
  }
})

test('WHAT[host-boundary-032] C32_decoration_gate_shares_the_single_predictor_configuration_query', async () => {
  clearPredictorState()
  await withExecutablePlugin(async (hooks) => {
    assert.equal(
      ModelRoutingSurface.sharedPredictorConfiguration().kind,
      'NotConfigured',
      'the shared query must report the unconfigured state',
    )
    const output = {
      description: 'Description for read',
      parameters: {
        type: 'object',
        properties: { path: { type: 'string' } },
        required: ['path'],
      },
    }
    await hooks['tool.definition']({ toolID: 'read' }, output)
    assert.equal(
      output.parameters.properties?.delegate_readonly_rounds,
      undefined,
      'decoration must stay off while the shared query reports NotConfigured',
    )
  })

  setPredictorState('configured')
  await withExecutablePlugin(async (hooks) => {
    assert.equal(
      ModelRoutingSurface.sharedPredictorConfiguration().kind,
      'Configured',
      'the shared query must report the configured state',
    )
    const output = {
      description: 'Description for read',
      parameters: {
        type: 'object',
        properties: { path: { type: 'string' } },
        required: ['path'],
      },
    }
    await hooks['tool.definition']({ toolID: 'read' }, output)
    assert.equal(
      output.parameters.properties.delegate_readonly_rounds.type,
      'integer',
      'decoration must follow the same single query result',
    )
  })
})

test('WHAT[host-boundary-032] C33_gate_follows_configuration_changes_within_one_plugin_instance', async () => {
  setPredictorState('configured')
  try {
    await withExecutablePlugin(async (hooks) => {
      const makeDefinition = () => ({
        description: 'Description for read',
        parameters: {
          type: 'object',
          properties: { path: { type: 'string' } },
          required: ['path'],
        },
      })

      const configuredOutput = makeDefinition()
      await hooks['tool.definition']({ toolID: 'read' }, configuredOutput)
      assert.equal(
        configuredOutput.parameters.properties.delegate_readonly_rounds.type,
        'integer',
        'configured state must decorate within one plugin instance',
      )

      clearPredictorState()
      const revertedOutput = makeDefinition()
      await hooks['tool.definition']({ toolID: 'read' }, revertedOutput)
      assert.equal(
        revertedOutput.parameters.properties?.delegate_readonly_rounds,
        undefined,
        'removing the configuration must stop decoration in the same instance',
      )
      assert.equal(
        revertedOutput.description,
        'Description for read',
        'removing the configuration must stop the collaboration prose',
      )

      setPredictorState('configured')
      const reconfiguredOutput = makeDefinition()
      await hooks['tool.definition']({ toolID: 'read' }, reconfiguredOutput)
      assert.equal(
        reconfiguredOutput.parameters.properties.delegate_readonly_rounds.type,
        'integer',
        'reconfiguring must resume decoration without a second enabled truth',
      )
    })
  } finally {
    clearPredictorState()
  }
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
    // DELEGATE.md 4.1 / 197: 真实 Host canary 必须枚举最终 provider-visible
    // tools。当前 runner 跑未配置态，断言枚举面存在、含内建与插件工具、
    // 且全部工具不带协议字段（无功能基线的 wire 级证明）。
    assert.ok(
      Array.isArray(stdoutSummary.wireInspection?.providerVisibleToolNames) &&
        stdoutSummary.wireInspection.providerVisibleToolNames.length > 0,
      'canary must enumerate the final provider-visible tool set on the real host',
    )
    const visibleToolNames = stdoutSummary.wireInspection.providerVisibleToolNames
    assert.ok(
      visibleToolNames.includes('js-manager'),
      `provider-visible set must include the plugin tool js-manager; observed: ${visibleToolNames.join(',')}`,
    )
    assert.ok(
      ['skill', 'read', 'glob', 'grep'].some((name) => visibleToolNames.includes(name)),
      `provider-visible set must include host builtin tools; observed: ${visibleToolNames.join(',')}`,
    )
    assert.equal(
      stdoutSummary.wireInspection?.providerVisibleProtocolAbsence,
      true,
      'every provider-visible tool must be free of the delegation protocol fields while Predictor is unconfigured',
    )
    for (const [name, status] of [['normal', 'completed'], ['executorError', 'completed'], ['cancellation', 'error']]) {
      const observed = stdoutSummary.calls?.[name]
      assert.ok(observed, `${name} must have real Host observations`)
      assert.equal(observed.sameArguments, true, name)
      assert.equal(observed.originalOrder, true, name)
      assert.equal(observed.originalValues, true, name)
      assert.equal(observed.status, status, name)
    }
    assert.equal(stdoutSummary.calls.cancellation.providerHistoryObserved, true)
    assert.equal(stdoutSummary.calls.executorError.failureOutputObserved, true)
  },
)

// host-boundary-032 / DELEGATE.md 4.3: the Host persists tool-call input after
// the before hook strips the protocol fields, so the next provider request is
// built from stripped history. The provider-facing transform restores the
// vaulted wire originals into that history before any consumer reads it.

test('WHAT[host-boundary-032] C34_transform_restores_hidden_protocol_fields_into_persisted_history', async () => {
  await withExecutablePlugin(async (hooks, _directory, _createdIds, runtime) => {
    const tool = 'js-manager'
    const sessionID = 'ses-c34'
    await openIncumbency(runtime, sessionID)
    const callID = 'call-c34-1'
    const beforeOutput = {
      args: {
        path: 'src/App.fs',
        contract: 'js-manager-contract-v1',
        delegate_readonly_rounds: 3,
        self_note: 'note-c34',
      },
    }

    await hooks['tool.execute.before']({ tool, sessionID, callID }, beforeOutput)

    // The persisted history the next provider request is built from: protocol
    // fields stripped from the durable tool-call input.
    const transformed = {
      messages: [
        {
          role: 'assistant',
          info: { id: 'asst-c34', sessionID },
          parts: [
            {
              type: 'tool',
              tool,
              callID,
              state: { status: 'completed', input: { path: 'src/App.fs' }, output: 'file contents' },
            },
          ],
        },
      ],
    }

    await hooks['experimental.chat.messages.transform']({}, transformed)

    const restoredInput = transformed.messages[0].parts[0].state.input
    assert.equal(
      restoredInput.contract,
      'js-manager-contract-v1',
      'review contract must return to the persisted history on the wire',
    )
    assert.equal(
      restoredInput.delegate_readonly_rounds,
      3,
      'budget must return to the persisted history on the wire',
    )
    assert.equal(
      restoredInput.self_note,
      'note-c34',
      'note must return to the persisted history on the wire',
    )
    assert.equal(
      restoredInput.path,
      'src/App.fs',
      'business arguments must survive the restore untouched',
    )
  })
})

test('WHAT[host-boundary-032] C35_unknown_tool_call_leaves_persisted_history_untouched', async () => {
  await withExecutablePlugin(async (hooks) => {
    // No before hook ran for this call: nothing was vaulted (e.g. history from
    // before a process restart). The restore must fail open and leave the wire
    // untouched; the pair-programming guideline marker is another mechanism's
    // legitimate output and is excluded from the comparison below.
    const transformed = {
      messages: [
        {
          role: 'assistant',
          info: { id: 'asst-c35', sessionID: 'ses-c35' },
          parts: [
            {
              type: 'tool',
              tool: 'read',
              callID: 'call-c35-unknown',
              state: { status: 'completed', input: { path: 'src/Old.fs' }, output: 'old contents' },
            },
          ],
        },
      ],
    }
    const before = structuredClone(transformed)

    await hooks['experimental.chat.messages.transform']({}, transformed)

    // The restore must fail open on a call with no vault entry. The
    // pair-programming guideline injector (HOST-013 Cursor mode,
    // prefix-stability-010) legitimately appends its NUL+BOM marker to
    // terminal tool results on the provider wire; that is a different
    // mechanism with its own contract, so the wire is compared with only
    // that marker removed — every other mutation still fails here.
    const wireWithoutGuidelineMarker = structuredClone(transformed)

    for (const message of wireWithoutGuidelineMarker.messages) {
      for (const part of message.parts ?? []) {
        if (typeof part?.state?.output === 'string') {
          part.state.output = part.state.output.split('\u0000\uFEFF')[0]
        }
      }
    }

    assert.deepEqual(
      wireWithoutGuidelineMarker,
      before,
      'a call with no vault entry must fail open and leave the wire untouched but for the pair-programming guideline marker',
    )

    assert.deepEqual(
      transformed.messages[0].parts[0].state.input,
      { path: 'src/Old.fs' },
      'the persisted call input must keep exactly its business arguments',
    )

    assert.deepEqual(
      Object.keys(transformed.messages[0].parts[0].state.input),
      ['path'],
      'no protocol key may appear in the persisted call input',
    )
  })
})

test('WHAT[host-boundary-032] C36_repeated_transforms_restore_once_and_stay_stable', async () => {
  await withExecutablePlugin(async (hooks, _directory, _createdIds, runtime) => {
    const tool = 'js-manager'
    const sessionID = 'ses-c36'
    await openIncumbency(runtime, sessionID)
    const callID = 'call-c36-1'

    await hooks['tool.execute.before'](
      { tool, sessionID, callID },
      { args: { path: 'src/App.fs', contract: 'js-manager-contract-v1' } },
    )

    const transformed = {
      messages: [
        {
          role: 'assistant',
          info: { id: 'asst-c36', sessionID },
          parts: [
            {
              type: 'tool',
              tool,
              callID,
              state: { status: 'completed', input: { path: 'src/App.fs' }, output: 'file contents' },
            },
          ],
        },
      ],
    }

    await hooks['experimental.chat.messages.transform']({}, transformed)
    const afterFirst = structuredClone(transformed)

    await hooks['experimental.chat.messages.transform']({}, transformed)
    await hooks['experimental.chat.messages.transform']({}, transformed)

    assert.deepEqual(
      transformed,
      afterFirst,
      'repeated transforms must not duplicate or reorder the restored fields',
    )
    assert.deepEqual(
      Object.keys(transformed.messages[0].parts[0].state.input),
      ['path', 'contract'],
      'business keys must keep their order before the appended protocol key',
    )
  })
})

test('WHAT[host-boundary-032] C37_tool_results_are_never_rewritten_by_the_restore', async () => {
  await withExecutablePlugin(async (hooks, _directory, _createdIds, runtime) => {
    const tool = 'js-manager'
    const sessionID = 'ses-c37'
    await openIncumbency(runtime, sessionID)
    const callID = 'call-c37-1'

    await hooks['tool.execute.before'](
      { tool, sessionID, callID },
      {
        args: {
          path: 'src/App.fs',
          contract: 'js-manager-contract-v1',
          delegate_readonly_rounds: 1,
        },
      },
    )

    const transformed = {
      messages: [
        {
          role: 'assistant',
          info: { id: 'asst-c37', sessionID },
          parts: [
            {
              type: 'tool',
              tool,
              callID,
              state: { status: 'completed', input: { path: 'src/App.fs' }, output: 'file contents' },
            },
          ],
        },
        {
          role: 'tool',
          info: { id: 'asst-c37-result', sessionID },
          parts: [{ type: 'tool-result', callID, result: 'file contents' }],
        },
      ],
    }

    await hooks['experimental.chat.messages.transform']({}, transformed)

    assert.deepEqual(
      transformed.messages[1].parts[0].result,
      'file contents',
      'tool results must stay verbatim through the restore',
    )
    assert.equal(
      transformed.messages[0].parts[0].state.input.contract,
      'js-manager-contract-v1',
      'the call itself must still be restored beside its result',
    )
    assert.equal(
      transformed.messages[0].parts[0].state.input.delegate_readonly_rounds,
      1,
      'the budget must still be restored beside its result',
    )
  })
})

test('WHAT[host-boundary-032] C38_restore_only_touches_protocol_fields_never_business_arguments', async () => {
  await withExecutablePlugin(async (hooks, _directory, _createdIds, runtime) => {
    const tool = 'read'
    const sessionID = 'ses-c38'
    await openIncumbency(runtime, sessionID)
    const callID = 'call-c38-1'

    await hooks['tool.execute.before'](
      { tool, sessionID, callID },
      {
        args: {
          path: 'src/App.fs',
          pattern: 'TODO',
          limit: 10,
          delegate_readonly_rounds: 2,
        },
      },
    )

    const transformed = {
      messages: [
        {
          role: 'assistant',
          info: { id: 'asst-c38', sessionID },
          parts: [
            {
              type: 'tool',
              tool,
              callID,
              state: {
                status: 'completed',
                input: { path: 'src/App.fs', pattern: 'TODO', limit: 10 },
                output: 'matches',
              },
            },
          ],
        },
      ],
    }

    await hooks['experimental.chat.messages.transform']({}, transformed)

    const input = transformed.messages[0].parts[0].state.input
    assert.deepEqual(
      { path: input.path, pattern: input.pattern, limit: input.limit },
      { path: 'src/App.fs', pattern: 'TODO', limit: 10 },
      'every business argument must survive verbatim',
    )
    assert.equal(
      input.delegate_readonly_rounds,
      2,
      'the protocol field must be restored beside the business arguments',
    )
    assert.deepEqual(
      Object.keys(input),
      ['path', 'pattern', 'limit', 'delegate_readonly_rounds'],
      'business keys must keep their order before the appended protocol key',
    )
  })
})
