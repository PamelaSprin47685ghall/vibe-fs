import assert from 'node:assert/strict'
import test from 'node:test'
import { integrationTest } from '../../verification-system/tests/support/tier-gate.mjs'
import { acceptAuthorityRoot, withExecutablePlugin } from '../../verification-system/tests/support/plugin-fixture.mjs'

integrationTest('WHAT[obligation-ledger-002] empty and duplicate native todo lists pass through unchanged', async () => {
  await withExecutablePlugin(async (hooks, _directory, _createdIds, runtime) => {
    const sessionID = 'obligation-native-replacement'
    await acceptAuthorityRoot(runtime, sessionID, 'engineer')

    const examples = [
      [],
      [
        { content: '重复内容', status: 'pending', priority: 'low' },
        { content: '重复内容', status: 'completed', priority: 'high' },
      ],
    ]

    for (let index = 0; index < examples.length; index += 1) {
      const todos = examples[index]
      const args = { todos, retainCheckpoints: 1 }
      await hooks['tool.execute.before'](
        { tool: 'todowrite', sessionID, callID: 'todo-native-' + index },
        { args },
      )
      assert.equal(args.todos, todos)
      assert.deepEqual(args.todos, todos)
    }
  })
})

test.todo('WHAT[obligation-ledger-002] installed OpenCode native executor replaces and clears only the current session TodoTable')
