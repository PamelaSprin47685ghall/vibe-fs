import assert from 'node:assert/strict'
import { integrationTest } from '../../verification-system/tests/support/tier-gate.mjs'
import { acceptAuthorityRoot, withExecutablePlugin, withPlugin } from '../../verification-system/tests/support/plugin-fixture.mjs'

integrationTest('WHAT[action-affordance-015] native todowrite keeps the host definition byte-identical', async () => {
  await withPlugin(async (hooks) => {
    const output = {
      description: 'Native todo writer',
      parameters: {
        type: 'object',
        properties: {
          todos: {
            type: 'array',
            items: {
              type: 'object',
              properties: {
                content: { type: 'string' },
                status: { type: 'string' },
                priority: { type: 'string' },
              },
              required: ['content', 'status'],
            },
          },
        },
        required: ['todos'],
      },
    }
    const before = JSON.stringify(output)

    await hooks['tool.definition']({ toolID: 'todowrite' }, output)
    await hooks['tool.definition']({ toolID: 'todowrite' }, output)

    const schema = output.jsonSchema ?? output.parameters
    assert.equal(output.jsonSchema, undefined, 'no provider schema is rewritten onto the definition')
    assert.equal(JSON.stringify(output), before, 'repeated definition passes leave the host shape untouched')
    assert.ok(schema.properties.todos, 'the list field keeps the Host name todos')
    assert.deepEqual(schema.required, ['todos'])
    for (const injected of ['obligations', 'retainCheckpoints', 'planComplete', 'workingOn', 'horizon', 'revision']) {
      assert.equal(Object.prototype.hasOwnProperty.call(schema.properties, injected), false)
    }
  })
})

integrationTest('WHAT[action-affordance-015] native todowrite args reach the executor untouched and stay untouched after', async () => {
  await withExecutablePlugin(async (hooks, _directory, _createdIds, runtime) => {
    const sessionID = 'native-todo-checkpoint'
    const callID = 'todo-call-1'
    await acceptAuthorityRoot(runtime, sessionID, 'engineer')

    const todos = [{ content: 'Implement checkpoint compression', status: 'in_progress', priority: 'high' }]
    const args = { todos }
    const beforeOutput = { args }

    await hooks['tool.execute.before']({ tool: 'todowrite', sessionID, callID }, beforeOutput)
    assert.equal(beforeOutput.args.todos, todos, 'the executor reads the very same array, not a copy')

    await hooks['tool.execute.after'](
      { tool: 'todowrite', sessionID, callID, args: beforeOutput.args },
      { title: 'todowrite', output: 'Todos updated', metadata: {} },
    )

    assert.equal(beforeOutput.args.todos, todos, 'the after hook leaves the Host field in place')
    assert.deepEqual(beforeOutput.args.todos, todos)
  })
})
