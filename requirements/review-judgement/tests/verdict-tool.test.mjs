// tests/unit/tools/verdict-tool.test.mjs — VERIFY-008/009: reviewer judge tool contract.

import assert from 'node:assert/strict'
import test from 'node:test'
import { acceptAuthorityRoot, withExecutablePlugin } from '../plugin/plugin-fixture.mjs'
import { listItems, payloadOf } from '../support/domain.mjs'

const {
  HostToolArguments_$ctor_4E60E31B: makeArgs,
  HostToolContext,
  ToolHostCodec_factory,
} = await import('../../../dist/Infrastructure/OpenCode/Codec/ToolHostCodec.js')
const { spec } = await import('../../../dist/Infrastructure/OpenCode/Tools/JudgeTool.js')
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

test('JUDGE_spec_exposes_the_verdict_input_and_public_tool_identity', () => {
  const tool = spec(factory, emptyScope())

  assert.equal(tool.Name, 'judge')
  assert.match(tool.Description, /PERFECT or REVISE/)
  assert.match(tool.Description, /does not mutate source/)
  const args = listItems(tool.Arguments)
  assert.deepEqual(args[0][0], 'verdict')
  assert.deepEqual(payloadOf(args[0][1]).values, ['PERFECT', 'REVISE'])
})

test('JUDGE_invalid_input_is_rejected_as_a_natural_consequence', async () => {
  await withExecutablePlugin(async (hooks, _directory, _createdIds, runtime) => {
    await acceptAuthorityRoot(runtime, 'ses-reviewer', 'fast-reviewer')

    const result = await hooks.tool.judge.execute({ verdict: 'APPROVE' }, hostContext())

    assert.match(result, /judgment was not received/i)
    assert.match(result, /PERFECT or REVISE/)
    assert.doesNotMatch(result, /\berror\s*=/)
  })
})

test('JUDGE_missing_input_is_rejected_as_a_natural_consequence', async () => {
  await withExecutablePlugin(async (hooks, _directory, _createdIds, runtime) => {
    await acceptAuthorityRoot(runtime, 'ses-reviewer', 'fast-reviewer')

    const result = await hooks.tool.judge.execute({}, hostContext())

    assert.match(result, /judgment was not received/i)
    assert.match(result, /PERFECT or REVISE/)
    assert.doesNotMatch(result, /\berror\s*=/)
  })
})

test('JUDGE_is_unavailable_to_non_reviewer_sessions', async () => {
  const result = await spec(factory, emptyScope()).Execute(
    makeArgs({ verdict: 'REVISE' }),
    context({ sessionId: 'ses-manager' }),
  )

  assert.match(result, /did not come from a Reviewer/i)
  assert.doesNotMatch(result, /\berror\s*=/)
})

test('JUDGE_empty_session_is_rejected_before_role_resolution', async () => {
  await withExecutablePlugin(async (hooks, _directory, _createdIds, runtime) => {
    await acceptAuthorityRoot(runtime, 'ses-reviewer', 'fast-reviewer')

    const result = await hooks.tool.judge.execute(
      { verdict: 'REVISE' },
      hostContext({ sessionId: '' }),
    )

    assert.match(result, /authority is established/i)
    assert.doesNotMatch(result, /\berror\s*=/)
  })
})

test('JUDGE_reviewer_requires_a_tool_call_id_before_review_submission', async () => {
  await withExecutablePlugin(async (hooks, _directory, _createdIds, runtime) => {
    await acceptAuthorityRoot(runtime, 'ses-reviewer', 'fast-reviewer')

    const result = await hooks.tool.judge.execute(
      { verdict: 'REVISE' },
      hostContext({ providerRunId: 'run-1' }),
    )

    assert.match(result, /could not be bound to the current review turn/i)
    assert.doesNotMatch(result, /\berror\s*=/)
  })
})
