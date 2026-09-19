import assert from 'node:assert/strict'
import test from 'node:test'
import { acceptAuthorityRoot, grantWorkOwned, withExecutablePlugin, withPlugin } from '../../verification-system/tests/support/plugin-fixture.mjs'



test('WHAT[capability-enforcement-024] FORK_manager_fork_rejects_devops_calling', async () => {
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
