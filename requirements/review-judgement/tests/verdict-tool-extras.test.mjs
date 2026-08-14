// Judge fail-closed branches past the role gate.
// The scope's fake journal serves an authority profile (RoleFor = Reviewer),
// so spec.Execute reaches the owner/tree/barrier resolution.

import assert from 'node:assert/strict'
import test from 'node:test'
import { mapOfEntries, sessionId, structuralComparer, toList } from '../../verification-system/tests/support/domain.mjs'

const {
  HostToolArguments_$ctor_4E60E31B: makeArgs,
  HostToolContext,
  ToolHostCodec_factory,
} = await import('../../../dist/Infrastructure/OpenCode/Codec/ToolHostCodec.js')
const { spec } = await import('../../../dist/Infrastructure/OpenCode/Tools/JudgeTool.js')
const { ToolRuntimeScope } = await import('../../../dist/Infrastructure/OpenCode/Tools/ToolRuntimeScope.js')
const { SessionAgentProjection, AgentProjectionSet, AgentProjection_empty: emptyAgentProjection } = await import('../../../dist/Composition/Durable/Projection.js')
const { ProjectionSet } = await import('../../../dist/Composition/Durable/ProjectionState.js')
const { Role } = await import('../../../dist/Kernel/Roles.js')

const fakeSchema = {
  enum: (values) => ({ values }),
}
const factory = ToolHostCodec_factory({ tool: { schema: fakeSchema } })

const context = ({ sessionId: sid = 'ses-reviewer', toolCallId = 'call-1', providerRunId = 'run-1' } = {}) =>
  new HostToolContext(sid, undefined, toolCallId, providerRunId, undefined, () => () => {})

const REVIEWER = sessionId('ses-reviewer')

const sessionMap = (entries) => mapOfEntries(entries, structuralComparer)

const reviewerSession = (guard) =>
  new SessionAgentProjection(
    undefined,
    undefined,
    undefined,
    undefined,
    undefined,
    guard,
    undefined,
    undefined,
    { ActiveLogicalRun: { CanonicalRole: Role.Reviewer, SelectedAgent: 'fast-reviewer' }, LastAuthorityProfile: undefined },
    undefined,
    undefined,
    undefined,
  )

const fakeJournal = (guard, extraSessions = []) => {
  const sessions = sessionMap([[REVIEWER, reviewerSession(guard)], ...extraSessions])
  const agentProjections = new AgentProjectionSet(
    sessions,
    emptyAgentProjection.Associations,
    emptyAgentProjection.Orchestrator,
    emptyAgentProjection.HandleByChildSession,
    emptyAgentProjection.Fission,
    emptyAgentProjection.MagicTodo,
    0,
  )
  const projection = new ProjectionSet(agentProjections, undefined)
  return {
    gate: { Enter: () => ({ Exit: () => {} }) },
    derivedFallbackSuccesses: new Set(),
    writer: {
      TryCurrent: () => undefined,
      LastCommittedLocalSeq: 0n,
      Release: () => {},
      ReleaseAsync: () => Promise.resolve(),
    },
    initialProjection: projection,
  }
}

const scopeFor = ({ journal, sessionParents = sessionMap([]), gitTreePort } = {}) =>
  new ToolRuntimeScope(
    {},
    journal,
    gitTreePort,
    undefined,
    sessionParents,
    () => undefined,
    new Set(),
    new Map(),
    undefined,
    undefined,
    undefined,
    undefined,
    undefined,
  )

const run = async (tool, args, ctx) => tool.Execute(makeArgs(args), ctx)

test('JUDGE_unknown_owner_fails_closed_without_internal_vocabulary', async () => {
  const tool = spec(factory, scopeFor({ journal: fakeJournal({ CurrentBarrierId: 'bar-1' }) }))
  const result = await run(tool, { verdict: 'REVISE' }, context())
  assert.match(result, /review context is incomplete/i)
  assert.doesNotMatch(result, /\berror\s*=/)
})

test('JUDGE_missing_tree_fails_closed_without_internal_vocabulary', async () => {
  const parents = sessionMap([['ses-reviewer', 'ses-manager']])
  const tool = spec(factory, scopeFor({ journal: fakeJournal({ CurrentBarrierId: 'bar-1' }), sessionParents: parents }))
  const result = await run(tool, { verdict: 'REVISE' }, context())
  assert.match(result, /review context is incomplete/i)
  assert.doesNotMatch(result, /\berror\s*=/)
})

test('JUDGE_no_open_review_barrier_fails_closed_without_internal_vocabulary', async () => {
  const parents = sessionMap([['ses-reviewer', 'ses-manager']])
  const tool = spec(
    factory,
    scopeFor({
      journal: fakeJournal({ CurrentBarrierId: undefined }),
      sessionParents: parents,
      gitTreePort: { GetTreeHash: () => 'tree-hash-1' },
    }),
  )
  const result = await run(tool, { verdict: 'PERFECT' }, context())
  assert.match(result, /review context is incomplete/i)
  assert.doesNotMatch(result, /\berror\s*=/)
})

test('JUDGE_non_reviewer_role_is_refused_before_identity_checks', async () => {
  const tool = spec(factory, scopeFor())
  const result = await run(tool, { verdict: 'PERFECT' }, context({ sessionId: 'ses-coder' }))
  assert.match(result, /did not come from a Reviewer/i)
  assert.doesNotMatch(result, /\berror\s*=/)
})
