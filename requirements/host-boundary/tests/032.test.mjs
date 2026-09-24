import assert from 'node:assert/strict'
import { execFileSync, spawnSync } from 'node:child_process'
import { existsSync } from 'node:fs'
import path from 'node:path'
import test from 'node:test'
import { fileURLToPath } from 'node:url'
import { openIncumbency, withExecutablePlugin } from '../../verification-system/tests/support/plugin-fixture.mjs'
import { integrationTest } from '../../verification-system/tests/support/tier-gate.mjs'
import { OPENCODE_BIN } from '../../verification-system/tests/e2e/support/process-host-utils.js'

const FOUR_DEDICATED_TOOLS = ['read-manager', 'glob-manager', 'grep-manager', 'js-manager']

test('WHAT[host-boundary-032] C01_tool_definition_decorates_four_dedicated_tools_with_contract_enum', async () => {
  await withExecutablePlugin(async (hooks) => {
    for (const toolID of FOUR_DEDICATED_TOOLS) {
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
    const tool = 'read-manager'
    const sessionID = 'ses-c05'
    await openIncumbency(runtime, sessionID)
    const callID = 'call-c05-1'
    const originalContract = 'read-manager-contract-v1'
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
      title: 'read-manager',
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
    const tool = 'read-manager'
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
      stdoutSummary.fourTools,
      ['glob-manager', 'grep-manager', 'js-manager', 'read-manager'],
      'fourTools must contain exactly the 4 review tools sorted',
    )
    for (const tool of stdoutSummary.fourTools) {
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

