import assert from 'node:assert/strict'
import { integrationTest } from '../../verification-system/tests/support/tier-gate.mjs'
import { acceptAuthorityRoot, withExecutablePlugin } from '../../verification-system/tests/support/plugin-fixture.mjs'

const rows = [
  { content: '重复内容', status: 'pending', priority: 'low' },
  { content: '  前后空格\r\n第二行  ', status: 'completed', priority: 'high' },
]

integrationTest('WHAT[obligation-ledger-001] plugin strips only retainCheckpoints and leaves native todo rows untouched', async () => {
  await withExecutablePlugin(async (hooks, _directory, _createdIds, runtime) => {
    const sessionID = 'obligation-native-rows'
    await acceptAuthorityRoot(runtime, sessionID, 'engineer')
    const todos = structuredClone(rows)
    const args = { todos, retainCheckpoints: 2 }

    await hooks['tool.execute.before']({ tool: 'todowrite', sessionID, callID: 'todo-native-1' }, { args })

    assert.equal('retainCheckpoints' in args, false)
    assert.equal(args.todos, todos, 'the plugin must preserve the exact native todos array object')
    assert.deepEqual(args.todos, rows)
  })
})

integrationTest('WHAT[obligation-ledger-001] provider schema adds no retired planning fields', async () => {
  await withExecutablePlugin(async (hooks) => {
    const output = {
      description: 'native todowrite',
      parameters: { type: 'object', properties: { todos: { type: 'array' } }, required: ['todos'] },
    }
    await hooks['tool.definition']({ toolID: 'todowrite' }, output)
    const schema = output.jsonSchema ?? output.parameters
    assert.ok(schema.properties.todos)
    assert.ok(schema.properties.retainCheckpoints)
    for (const retired of ['planComplete', 'workingOn', 'obligations', 'horizon', 'revision']) {
      assert.equal(Object.prototype.hasOwnProperty.call(schema.properties, retired), false)
    }
  })
})
