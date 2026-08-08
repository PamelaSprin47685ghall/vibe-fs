// tests/unit/tools/verdict-tool.test.mjs — VERIFY-008/009: reviewer verdict tool contract.

import assert from 'node:assert/strict'
import test from 'node:test'
import { acceptAuthorityRoot, withExecutablePlugin } from '../plugin/plugin-fixture.mjs'
import { listItems, payloadOf } from '../support/domain.mjs'

const {
  HostToolArguments_$ctor_4E60E31B: makeArgs,
  HostToolContext,
  ToolHostCodec_factory,
} = await import('../../../dist/Infrastructure/OpenCode/Codec/ToolHostCodec.js')
const { spec } = await import('../../../dist/Infrastructure/OpenCode/Tools/VerdictTool.js')
const { ToolRuntimeScope } = await import('../../../dist/Infrastructure/OpenCode/Tools/ToolRuntimeScope.js')

const fakeSchema = {
  enum: (values) => ({ values }),
}
const factory = ToolHostCodec_factory({ tool: { schema: fakeSchema } })

const emptyScope = () =>
  new ToolRuntimeScope(
    undefined,
    undefined,
    undefined,
    undefined,
    new Map(),
    () => undefined,
    new Set(),
    new Map(),
    undefined,
    undefined,
    undefined,
    undefined,
    undefined,
  )

const context = ({ sessionId = 'ses-reviewer', toolCallId, providerRunId } = {}) =>
  new HostToolContext(sessionId, undefined, toolCallId, providerRunId, undefined, () => () => {})

const hostContext = ({ sessionId = 'ses-reviewer', toolCallId, providerRunId } = {}) => ({
  sessionID: sessionId,
  agent: 'fast-reviewer',
  ...(toolCallId === undefined ? {} : { callID: toolCallId }),
  ...(providerRunId === undefined ? {} : { messageID: providerRunId }),
})

const parseToml = (text) =>
  Object.fromEntries(
    text
      .split('\n')
      .filter((line) => line.includes(' = '))
      .map((line) => {
        const [name, ...rest] = line.split(' = ')
        const raw = rest.join(' = ')
        return [name, raw.startsWith('"') ? JSON.parse(raw) : raw]
      }),
  )

test('VERDICT_spec_exposes_the_verdict_input_and_public_tool_identity', () => {
  const tool = spec(factory, emptyScope())

  assert.equal(tool.Name, 'verdict')
  assert.equal(tool.Description, 'Submit the review verdict')
  const args = listItems(tool.Arguments)
  assert.deepEqual(args[0][0], 'verdict')
  assert.deepEqual(payloadOf(args[0][1]).values, ['PERFECT', 'REVISE'])
})

test('VERDICT_invalid_input_is_rejected_as_a_public_error_result', async () => {
  await withExecutablePlugin(async (hooks, _directory, _createdIds, runtime) => {
    acceptAuthorityRoot(runtime, 'ses-reviewer', 'fast-reviewer')

    const result = parseToml(await hooks.tool.verdict.execute({ verdict: 'APPROVE' }, hostContext()))

    assert.equal(result.error, 'Verdict rejected: verdict must be exactly PERFECT or REVISE.')
  })
})

test('VERDICT_missing_input_is_rejected_as_a_public_error_result', async () => {
  await withExecutablePlugin(async (hooks, _directory, _createdIds, runtime) => {
    acceptAuthorityRoot(runtime, 'ses-reviewer', 'fast-reviewer')

    const result = parseToml(await hooks.tool.verdict.execute({}, hostContext()))

    assert.equal(result.error, 'Verdict rejected: verdict must be exactly PERFECT or REVISE.')
  })
})

test('VERDICT_is_unavailable_to_non_reviewer_sessions', async () => {
  const result = parseToml(
    await spec(factory, emptyScope()).Execute(
      makeArgs({ verdict: 'REVISE' }),
      context({ sessionId: 'ses-manager' }),
    ),
  )

  assert.equal(result.error, 'Verdict rejected: the verdict tool is available only to reviewer sessions.')
})

test('VERDICT_empty_session_is_rejected_before_role_resolution', async () => {
  await withExecutablePlugin(async (hooks, _directory, _createdIds, runtime) => {
    acceptAuthorityRoot(runtime, 'ses-reviewer', 'fast-reviewer')

    const result = parseToml(
      await hooks.tool.verdict.execute({ verdict: 'REVISE' }, hostContext({ sessionId: '' })),
    )

    // The registry's role gate fires before the tool runs: an empty session has no
    // role, so the denial is the fail-closed registry message, not the tool's own.
    assert.equal(result.error, "Tool 'verdict' rejected: no Authority Root fixes this session's role")
  })
})

test('VERDICT_reviewer_requires_a_tool_call_id_before_review_submission', async () => {
  await withExecutablePlugin(async (hooks, _directory, _createdIds, runtime) => {
    acceptAuthorityRoot(runtime, 'ses-reviewer', 'fast-reviewer')

    const result = parseToml(
      await hooks.tool.verdict.execute({ verdict: 'REVISE' }, hostContext({ providerRunId: 'run-1' })),
    )

    assert.equal(result.error, 'Verdict rejected: missing tool call id.')
  })
})
