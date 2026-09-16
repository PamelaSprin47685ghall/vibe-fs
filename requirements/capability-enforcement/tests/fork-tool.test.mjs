// ENF-009/010/024: fork and commission are exercised through the real plugin
// registrations. Host-built zod schemas are inspected by declared argument name;
// no internal ToolSpec/RuntimeScope representation crosses this test boundary.

import assert from 'node:assert/strict'
import test from 'node:test'

import { acceptAuthorityRoot, grantWorkOwned, withExecutablePlugin, withPlugin } from '../../verification-system/tests/support/plugin-fixture.mjs'

test('WHAT[ENF-009] FORK_specs_expose_expected_names_and_only_manager_fork_carries_keywords', async () => {
  await withPlugin(async (hooks) => {
    assert.equal(hooks.tool.fork !== undefined, true)
    assert.equal(hooks.tool.commission !== undefined, true)
    const forkArgs = ['calling', 'name', 'charge', 'keywords', 'attach', 'expected_tool_calls']
    const commissionArgs = ['calling', 'name', 'charge', 'expected_tool_calls']
    for (const name of forkArgs) assert.equal(typeof hooks.tool.fork.args[name]?.safeParse, 'function', `fork.${name}`)
    for (const name of commissionArgs) assert.equal(typeof hooks.tool.commission.args[name]?.safeParse, 'function', `commission.${name}`)
    assert.equal(hooks.tool.commission.args.keywords, undefined, 'commission must not carry warm-start keywords')
  })
})

test('WHAT[ENF-009] FORK_disposed_or_unbound_execution_surfaces_natural_execution_consequence', async () => {
  await withExecutablePlugin(async (hooks) => {
    const result = await hooks.tool.fork.execute(
      { calling: 'engineer', name: 'Ada', charge: 'do work' },
      { sessionID: '', agent: 'engineer' },
    )
    assert.match(result, /cannot be placed from this execution context|caller's authority is established|调用方权威确立之前/i)
    assert.doesNotMatch(result, /sessionID|\berror\s*=/i)
  })
})

test('WHAT[ENF-010] FORK_orchestrator_missing_authority_is_refused_without_session_identity', async () => {
  await withExecutablePlugin(async (hooks) => {
    const result = await hooks.tool.commission.execute(
      { calling: 'lead', name: 'North Road', charge: 'x' },
      { sessionID: '', agent: 'orchestrator' },
    )
    assert.match(result, /caller's authority is established|调用方权威确立之前/i)
    assert.doesNotMatch(result, /sessionID|\berror\s*=/i)
  })
})

test('WHAT[ENF-024] FORK_manager_fork_rejects_devops_calling', async () => {
  await withExecutablePlugin(async (hooks, _directory, createdIds, runtime) => {
    await acceptAuthorityRoot(runtime, 'ses-fork-devops', 'manager')
    await grantWorkOwned(runtime, 'ses-fork-devops')
    const result = await hooks.tool.fork.execute(
      { calling: 'devops', name: 'Op', charge: 'run tests' },
      { sessionID: 'ses-fork-devops', agent: 'manager' },
    )
    assert.match(result, /cannot fork DevOps|fork only targets Engineer|只能 fork Engineer/i)
    assert.equal(createdIds.length, 0)
  })
})
