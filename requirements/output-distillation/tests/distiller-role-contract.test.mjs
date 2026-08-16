// requirements/output-distillation/tests/distiller-role-contract.test.mjs
//
// Owner: output-distillation (Wave 2a split leftovers; trace migration NEW).
//
// DISTILL-009: the Distiller mapping sub-session is a private runtime. Its
// managed agent identity lives in the internal role vocabulary (AGENT-008) and
// must never surface as a public `fork` / `horizon` target.
//
// DISTILL-010: the Distiller role does not execute, does not change the world,
// and does not judge acceptance. The role's permission catalog is empty; the
// provider tool surface for execution is the `run` tool only — distillation is
// invoked inside it, never exposed as a separate provider tool.
//
// Contract tests read the normative surfaces directly (Roles catalog +
// ManagedAgent catalog + ExecutorTool registry); no production code touched.

// Language-sensitive prose is only read by `runSpec` at call time, but pin the
// provider language before module load for determinism (HOST-026 binding).
process.env.WANXIANGSHU_PROVIDER_LANGUAGE = 'en'

import assert from 'node:assert/strict'
import test from 'node:test'

import { AgentTier, Role, Roles_isAllowed, Roles_permissions, Roles_roleLabel, ToolPermission } from '../../../dist/Foundation/Roles.js'
import { allInternalRoles, allPublicRoles, nameOf } from '../../../dist/Participant/Persona/ManagedCatalog.js'
import { listItems } from '../../verification-system/tests/support/domain.mjs'

const { runSpec } = await import('../../../dist/OpenCode/Tools/ExecutorTool.js')
const { ToolHostCodec_factory } = await import('../../../dist/OpenCode/Codec/ToolHostCodec.js')

const chain = (kind) => ({
  kind,
  describe: () => chain(`${kind}-described`),
  optional: () => chain(`${kind}-optional`),
})
const factory = ToolHostCodec_factory({
  tool: {
    schema: {
      string: () => chain('string'),
      number: () => chain('number'),
      enum: (values) => chain('enum', { values }),
      boolean: () => chain('boolean'),
    },
  },
})

test('WHAT[DISTILL-009] distiller_is_private_internal_runtime_not_a_public_fork_or_horizon_target', () => {
  // AGENT-008 public vocabulary = the only roles a caller may fork to or put on
  // the horizon; the Distiller must never appear there.
  const publicLabels = listItems(allPublicRoles).map((role) => Roles_roleLabel(role))
  assert.ok(
    !publicLabels.includes('distiller'),
    `Distiller must not be a public fork/horizon target; public roles are: ${publicLabels.join(', ')}`,
  )

  const internalLabels = listItems(allInternalRoles).map((role) => Roles_roleLabel(role))
  assert.ok(
    internalLabels.includes('distiller'),
    'Distiller must be an internal role (private runtime), not part of the public vocabulary',
  )

  // The machine Assignment identity of the map/reduce sub-session is the
  // internal `fast-distiller` handle — a Host-owned private agent, not a
  // provider-visible persona.
  assert.equal(
    nameOf(AgentTier.Fast, Role.Distiller),
    'fast-distiller',
    'Distiller map/reduce forks use the internal fast-distiller managed agent name',
  )
})

test('WHAT[DISTILL-010] distiller_carries_no_execution_or_judgement_permissions_and_run_is_the_only_execution_surface', () => {
  // The role's tool-permission catalog is empty: it cannot run commands
  // (Exec/Pty), cannot change the world (Write/Edit/Move/Remove), and cannot
  // judge acceptance (Judge/Behavior).
  const perms = Roles_permissions(Role.Distiller)
  assert.equal(perms.size, 0, 'Distiller must carry zero tool permissions')

  const executionAndJudgement = [
    ToolPermission.Exec,
    ToolPermission.Pty,
    ToolPermission.Write,
    ToolPermission.Judge,
    ToolPermission.Fork,
    ToolPermission.Join,
    ToolPermission.Horizon,
  ]
  for (const permission of executionAndJudgement) {
    assert.equal(
      Roles_isAllowed(Role.Distiller, permission),
      false,
      `Distiller must not hold permission ${permission.tag}`,
    )
  }

  // Execution and distillation are different office surfaces: the provider
  // tool registry exposes the `run` tool; distillation is orchestrated inside
  // it and is not a separate provider tool.
  const tool = runSpec(factory, undefined)
  assert.equal(tool.Name, 'run', 'the execution tool surface is `run`; distill is not a separate provider tool')
})
