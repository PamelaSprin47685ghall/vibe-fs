import assert from 'node:assert/strict'
import { integrationTest } from '../../verification-system/tests/support/tier-gate.mjs'
import { acceptAuthorityRoot, withExecutablePlugin, withPlugin } from '../../verification-system/tests/support/plugin-fixture.mjs'

integrationTest('WHAT[action-affordance-014] the registered assume schema exposes only assumption', async () => {
  await withPlugin(async (hooks) => {
    const args = hooks.tool.assume.args
    assert.deepEqual(Object.keys(args), ['assumption'])
    assert.equal(args.assumption.safeParse('Use the smaller state machine.').success, true)
    assert.equal(args.assumption.safeParse(undefined).success, false)
    assert.equal(Object.prototype.hasOwnProperty.call(args, 'update'), false)
    assert.equal(Object.prototype.hasOwnProperty.call(args, 'todos'), false)
  })
})

integrationTest('WHAT[action-affordance-014] assume commits without echoing the assumption or writing a canvas', async () => {
  await withExecutablePlugin(async (hooks, _directory, _createdIds, runtime) => {
    const sessionID = 'assume-independent'
    await acceptAuthorityRoot(runtime, sessionID, 'engineer')

    const assumption = 'Use the smaller state machine and remove the canvas.'
    const result = await hooks.tool.assume.execute(
      { assumption },
      { sessionID, agent: 'engineer', callID: 'assume-1', messageID: 'message-assume-1' },
    )

    // The fixed prompt is rendered in the session's bound language; the alternation pins the
    // whole string instead of merely checking that some answer came back.
    assert.match(
      result,
      /^已笃定，不再因为没有信息增量的犹豫反复改判。$|^Committed\. Do not reopen the judgment merely because of hesitation without new information\.$/,
    )
    assert.equal(result.includes(assumption), false, 'the committed prompt must not echo the assumption')
  })
})

integrationTest('WHAT[action-affordance-014] native todowrite keeps the host definition byte-identical', async () => {
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

integrationTest('WHAT[action-affordance-014] native todowrite args reach the executor untouched and stay untouched after', async () => {
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