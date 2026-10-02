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

integrationTest('WHAT[action-affordance-014] native todowrite keeps its todos schema and gains required retainCheckpoints', async () => {
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

    await hooks['tool.definition']({ toolID: 'todowrite' }, output)

    const schema = output.jsonSchema ?? output.parameters
    const list = schema.properties.obligations
    assert.ok(list, 'the provider-visible list field is obligations')
    assert.equal(
      Object.prototype.hasOwnProperty.call(schema.properties, 'todos'),
      false,
      'the published schema does not name the Host field todos',
    )
    assert.deepEqual(list.items.properties, {
      content: { type: 'string' },
      status: { type: 'string' },
      priority: { type: 'string' },
    })
    assert.deepEqual(list.items.required, ['content', 'status'])
    assert.equal(schema.properties.retainCheckpoints.type, 'integer')
    assert.equal(schema.properties.retainCheckpoints.minimum, 1)
    assert.ok(schema.required.includes('obligations'))
    assert.ok(schema.required.includes('retainCheckpoints'))
    assert.equal(schema.required.includes('todos'), false, 'required names the published field')
    for (const retired of ['planComplete', 'workingOn', 'horizon', 'revision']) {
      assert.equal(Object.prototype.hasOwnProperty.call(schema.properties, retired), false)
    }
  })
})

integrationTest('WHAT[action-affordance-014] repeated todowrite decoration is idempotent while a foreign retainCheckpoints still fails loudly', async () => {
  await withPlugin(async (hooks) => {
    const output = {
      description: 'Native todo writer',
      parameters: {
        type: 'object',
        properties: { todos: { type: 'array', items: { type: 'object' } } },
        required: ['todos'],
      },
    }

    await hooks['tool.definition']({ toolID: 'todowrite' }, output)
    const firstSnapshot = structuredClone(output)

    // The Host re-triggers tool.definition for every provider request; a second
    // and third pass over the very same definition must be a no-op, not a
    // "Tool todowrite already defines retainCheckpoints" fatal.
    await hooks['tool.definition']({ toolID: 'todowrite' }, output)
    await hooks['tool.definition']({ toolID: 'todowrite' }, output)

    assert.deepEqual(output, firstSnapshot, 'repeated todowrite decoration must be idempotent')
    const schema = output.jsonSchema ?? output.parameters
    assert.equal(schema.required.filter((x) => x === 'retainCheckpoints').length, 1)
    assert.equal(schema.required.filter((x) => x === 'obligations').length, 1, 'the published list name is decorated once')

    // A definition that already carries a foreign retainCheckpoints is a real
    // conflict and must fail loudly instead of silently keeping the wrong shape.
    // The plugin hook wrapper surfaces the failure as a plain { message, ... }
    // object rather than an Error instance, so assert on the message field.
    // A definition that already carries a foreign retainCheckpoints is a real
    // conflict and must fail loudly instead of silently keeping the wrong shape.
    // The plugin hook wrapper surfaces the failure as a plain { message, ... }
    // object rather than an Error instance, so catch and read the message.
    let foreignFailure
    try {
      await hooks['tool.definition'](
        { toolID: 'todowrite' },
        {
          description: 'Foreign writer',
          parameters: {
            type: 'object',
            properties: { todos: { type: 'array' }, retainCheckpoints: { type: 'string' } },
            required: ['todos'],
          },
        },
      )
    } catch (error) {
      foreignFailure = error
    }
    assert.ok(foreignFailure, 'a foreign retainCheckpoints definition must fail loudly')
    assert.match(
      String(foreignFailure?.message ?? foreignFailure),
      /already defines retainCheckpoints/,
    )
  })
})

integrationTest('WHAT[action-affordance-014] todowrite hides retainCheckpoints from the native executor and restores it in the after hook', async () => {
  await withExecutablePlugin(async (hooks, _directory, _createdIds, runtime) => {
    const sessionID = 'native-todo-checkpoint'
    const callID = 'todo-call-1'
    await acceptAuthorityRoot(runtime, sessionID, 'engineer')

    const obligations = [{ content: 'Implement checkpoint compression', status: 'in_progress', priority: 'high' }]
    const args = { obligations, retainCheckpoints: 2 }
    const beforeOutput = { args }

    await hooks['tool.execute.before']({ tool: 'todowrite', sessionID, callID }, beforeOutput)
    assert.equal('retainCheckpoints' in beforeOutput.args, false)
    assert.equal('obligations' in beforeOutput.args, false, 'the provider name is hidden from the executor')
    assert.equal(beforeOutput.args.todos, obligations, 'the executor reads the very same array, not a copy')

    await hooks['tool.execute.after'](
      { tool: 'todowrite', sessionID, callID, args: beforeOutput.args },
      { title: 'todowrite', output: 'Todos updated', metadata: {} },
    )

    assert.equal('todos' in beforeOutput.args, false, 'the after hook drops the Host name again')
    assert.equal(beforeOutput.args.obligations, obligations)
    assert.equal(beforeOutput.args.retainCheckpoints, 2)
    assert.deepEqual(beforeOutput.args.obligations, obligations)
  })
})

integrationTest('WHAT[action-affordance-014] a call carrying both the provider and the Host list name is rejected', async () => {
  await withExecutablePlugin(async (hooks, _directory, _createdIds, runtime) => {
    const sessionID = 'native-todo-collision'
    await acceptAuthorityRoot(runtime, sessionID, 'engineer')

    const obligations = [{ content: 'provider view', status: 'pending', priority: 'high' }]
    const todos = [{ content: 'host view', status: 'pending', priority: 'low' }]

    let failure
    try {
      await hooks['tool.execute.before'](
        { tool: 'todowrite', sessionID, callID: 'todo-collision-1' },
        { args: { obligations, todos, retainCheckpoints: 1 } },
      )
    } catch (error) {
      failure = error
    }

    assert.ok(failure, 'one call must not carry two list names')
    assert.match(String(failure?.message ?? failure), /obligations and todos/)
  })
})
