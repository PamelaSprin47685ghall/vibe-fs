import assert from 'node:assert/strict'
import { integrationTest } from '../../verification-system/tests/support/tier-gate.mjs'
import { acceptAuthorityRoot, withExecutablePlugin } from '../../verification-system/tests/support/plugin-fixture.mjs'

const rows = [
  { content: '重复内容', status: 'pending', priority: 'low' },
  { content: '  前后空格\r\n第二行  ', status: 'completed', priority: 'high' },
]

integrationTest('WHAT[obligation-ledger-001] plugin leaves native todo rows untouched', async () => {
  await withExecutablePlugin(async (hooks, _directory, _createdIds, runtime) => {
    const sessionID = 'obligation-native-rows'
    await acceptAuthorityRoot(runtime, sessionID, 'engineer')
    const todos = structuredClone(rows)
    const args = { todos }

    await hooks['tool.execute.before']({ tool: 'todowrite', sessionID, callID: 'todo-native-1' }, { args })

    assert.equal(args.todos, todos, 'the executor receives the exact same array object, not a copy')
    assert.deepEqual(args.todos, rows)
  })
})

integrationTest('WHAT[obligation-ledger-001] provider definition stays host-shaped with no retired planning fields', async () => {
  await withExecutablePlugin(async (hooks) => {
    const output = {
      description: 'native todowrite',
      parameters: { type: 'object', properties: { todos: { type: 'array' } }, required: ['todos'] },
    }
    const before = JSON.stringify(output)
    await hooks['tool.definition']({ toolID: 'todowrite' }, output)
    const schema = output.jsonSchema ?? output.parameters
    assert.equal(output.jsonSchema, undefined, 'no provider schema is rewritten onto the definition')
    assert.equal(JSON.stringify(output), before, 'the host definition is passed through byte-identical')
    assert.ok(schema.properties.todos, 'the list field keeps the Host name todos')
    for (const injected of ['obligations', 'retainCheckpoints', 'planComplete', 'workingOn', 'horizon', 'revision']) {
      assert.equal(Object.prototype.hasOwnProperty.call(schema.properties, injected), false)
    }
  })
})