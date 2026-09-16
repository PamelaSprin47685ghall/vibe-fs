import assert from 'node:assert/strict'
import test from 'node:test'
import {
  acceptAuthorityRoot,
  grantWorkOwned,
  withExecutablePlugin,
  withPlugin,
} from '../../../../verification-system/tests/support/plugin-fixture.mjs'

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
      { calling: 'coder', name: 'Ada', charge: 'do work' },
      { sessionID: '', agent: 'coder' },
    )
    assert.match(result, /cannot be placed from this execution context|caller's authority is established|调用方权威确立之前/i)
    assert.doesNotMatch(result, /sessionID|\berror\s*=/i)
  })
})

test('WHAT[ENF-009] FORK_non_repository_target_rejects_nonempty_warm_start_keywords_before_creation', async () => {
  await withExecutablePlugin(async (hooks, _directory, createdIds, runtime) => {
    await acceptAuthorityRoot(runtime, 'ses-fork', 'manager')
    await grantWorkOwned(runtime, 'ses-fork')
    const result = await hooks.tool.fork.execute(
      { calling: 'researcher', name: 'Web Road', charge: 'browse', keywords: 'repository clue' },
      { sessionID: 'ses-fork', agent: 'manager' },
    )
    assert.match(result, /only available when fork targets Coder, Inspector, or DevOps|仅当 fork 目标为 Coder、Inspector 或 DevOps/i)
    assert.doesNotMatch(result, /\berror\s*=/i)
    assert.equal(createdIds.length, 0)
  })
})

test('WHAT[ENF-009] MANAGER_non_repository_fork_keywords_are_rejected_before_child_creation', async () => {
  await withExecutablePlugin(async (hooks, _directory, createdIds, runtime) => {
    await acceptAuthorityRoot(runtime, 'ses-manager-contract', 'manager')
    await grantWorkOwned(runtime, 'ses-manager-contract')
    const result = await hooks.tool.fork.execute(
      { calling: 'researcher', name: 'Web Road', charge: 'browse', keywords: 'repository clue' },
      { sessionID: 'ses-manager-contract', agent: 'manager' },
    )
    assert.match(result, /fork targets Coder, Inspector, or DevOps|fork 目标为 Coder、Inspector 或 DevOps/i)
    assert.equal(createdIds.length, 0)
  })
})
