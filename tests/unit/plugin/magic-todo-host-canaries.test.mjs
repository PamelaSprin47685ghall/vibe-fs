// tests/unit/plugin/magic-todo-host-canaries.test.mjs
//
// Magic Todo Phase 0 — unit Host V1 contract canaries B, C, F plus A
// registration / positional-mutation preconditions.
//
// Real-host A (durable ToolPart.input alias), E, G, H live under the e2e
// real-host harness. This file freezes contracts that isolated helpers can
// prove without a full OpenCode lifetime:
//
//   B  tool.definition updates description + parameters + jsonSchema while
//      the original V1 decoder remains the execute-path decoder
//   C  stripping V2 id/kind lets the original V1 decoder succeed on
//      compatibility rows
//   F  freeze whether tool.execute.after runs when the executor throws
//   A′ before must mutate the original args object in place (replacement of
//      output.args is invisible to the executor) — registration + positional
//      preconditions only
//
// No production Magic Todo membrane is wired here.

import assert from 'node:assert/strict'
import test from 'node:test'
import {
  applyToolDefinitionHook,
  createMagicTodoContractHooks,
  decodeV1TodoWriteArgs,
  hostTrigger,
  runHostV1ToolExecutePath,
  sampleV2TodoWriteAdvertisement,
  sampleV2TodoWriteArgs,
  stripV2TodoIdentityFields,
  v1TodoWriteToolSeed,
  V1_TODOWRITE_PARAMETERS,
} from './plugin-fixture.mjs'

const SESSION = 'ses_magic_todo_canary'
const CALL = 'call_magic_todo_1'

// ── Canary B — definition schema ────────────────────────────────────────────

test('MAGIC_TODO_CANARY_B_definition_replaces_description_parameters_jsonSchema_original_decoder_unchanged', async () => {
  const hooks = createMagicTodoContractHooks()
  const seed = v1TodoWriteToolSeed()
  const advertised = sampleV2TodoWriteAdvertisement()

  const defined = await applyToolDefinitionHook(hooks['tool.definition'], seed)

  assert.equal(defined.description, advertised.description, 'B: description must be replaced')
  // sampleV2TodoWriteAdvertisement() may allocate a fresh structural schema
  // each call; compare structure to the independent expected object, and
  // identity only against the seed (full replacement), not false identity
  // with the independently allocated expected.
  assert.deepEqual(defined.parameters, advertised.parameters, 'B: parameters must be replaced')
  assert.deepEqual(defined.jsonSchema, advertised.jsonSchema, 'B: jsonSchema must be replaced')
  assert.notEqual(defined.parameters, seed.parameters, 'B: parameters identity must change')
  assert.notEqual(defined.jsonSchema, seed.jsonSchema, 'B: jsonSchema identity must change')

  // Host keeps the init-time execute wrapper bound to ORIGINAL parameters.
  assert.equal(defined.execute, seed.execute, 'B: execute wrapper is not replaced by definition')
  assert.equal(
    defined.originalParameters,
    V1_TODOWRITE_PARAMETERS,
    'B: original V1 parameters decoder identity is preserved for execute',
  )

  // Provider-facing advertisement carries V2 fields; original decoder still V1.
  assert.equal(
    defined.parameters.properties.todos.items.properties.id !== undefined,
    true,
    'B: provider parameters advertise id',
  )
  assert.equal(
    defined.parameters.properties.todos.items.properties.kind !== undefined,
    true,
    'B: provider parameters advertise kind',
  )
  assert.equal(
    defined.jsonSchema.properties.todos.items.properties.status.enum.includes('reviewing'),
    true,
    'B: provider jsonSchema advertises reviewing',
  )

  const v1Row = {
    todos: [{ content: 'only-v1', status: 'pending', priority: 'low' }],
  }
  const decoded = decodeV1TodoWriteArgs(v1Row)
  assert.equal(decoded.ok, true, 'B: original V1 decoder still accepts V1 rows after definition update')
  assert.deepEqual(decoded.value.todos[0], v1Row.todos[0])
})

test('MAGIC_TODO_CANARY_B_definition_jsonSchema_ternary_keeps_schema_when_both_replaced', async () => {
  // registry.ts ternary:
  //   output.parameters === tool.parameters || output.jsonSchema !== tool.jsonSchema
  //     ? output.jsonSchema : undefined
  // Replacing only parameters (same jsonSchema ref) would drop jsonSchema.
  // Membrane must replace both — freeze that both-replaced keeps jsonSchema.
  const seed = v1TodoWriteToolSeed()
  const onlyParameters = async (_input, output) => {
    output.parameters = { type: 'object', properties: { todos: { type: 'array' } } }
    // deliberately leave jsonSchema identity unchanged
  }
  const dropped = await applyToolDefinitionHook(onlyParameters, seed)
  assert.equal(
    dropped.jsonSchema,
    undefined,
    'B: replacing only parameters drops jsonSchema (Host ternary) — membrane must not do this',
  )

  const both = createMagicTodoContractHooks()['tool.definition']
  const kept = await applyToolDefinitionHook(both, seed)
  assert.equal(typeof kept.jsonSchema, 'object')
  assert.notEqual(kept.jsonSchema, undefined, 'B: replacing parameters+jsonSchema keeps advertised jsonSchema')
  assert.equal(kept.description.length > 0, true)
  assert.notEqual(kept.parameters, seed.parameters)
  assert.notEqual(kept.jsonSchema, seed.jsonSchema)
})

// ── Canary C — unknown id/kind stripping ────────────────────────────────────

test('MAGIC_TODO_CANARY_C_stripping_v2_id_kind_lets_original_v1_decoder_succeed', async () => {
  const hooks = createMagicTodoContractHooks()
  const raw = sampleV2TodoWriteArgs()

  // Precondition observation: raw V2 wire may still structurally decode under
  // current Effect Schema excess-property policy, but the decoder MUST NOT
  // surface id/kind on the typed V1 row (compatibility projection target).
  const rawDecoded = decodeV1TodoWriteArgs(structuredClone(raw))
  assert.equal(rawDecoded.ok, true, 'C: observe current V1 decoder accepts excess keys')
  for (const todo of rawDecoded.value.todos) {
    assert.equal('id' in todo, false, 'C: raw decode must not keep id on V1 row')
    assert.equal('kind' in todo, false, 'C: raw decode must not keep kind on V1 row')
  }

  const args = structuredClone(raw)
  const beforeOutput = { args }
  await hostTrigger(hooks['tool.execute.before'], { tool: 'todowrite', sessionID: SESSION, callID: CALL }, beforeOutput)

  // After strip, every row is a pure V1 compatibility row.
  for (const todo of beforeOutput.args.todos) {
    assert.equal('id' in todo, false, 'C: stripped row has no id')
    assert.equal('kind' in todo, false, 'C: stripped row has no kind')
    assert.equal(typeof todo.content, 'string')
    assert.equal(typeof todo.status, 'string')
    assert.equal(typeof todo.priority, 'string')
  }

  const decoded = decodeV1TodoWriteArgs(beforeOutput.args)
  assert.equal(decoded.ok, true, 'C: original V1 decoder succeeds after id/kind strip')
  assert.equal(decoded.value.todos.length, raw.todos.length)
  assert.deepEqual(
    decoded.value.todos.map((t) => ({ content: t.content, status: t.status, priority: t.priority })),
    raw.todos.map((t) => ({ content: t.content, status: t.status, priority: t.priority })),
  )
})

test('MAGIC_TODO_CANARY_C_strip_helper_is_in_place_on_todos_array', () => {
  const args = sampleV2TodoWriteArgs()
  const originalArgs = args
  const originalTodos = args.todos
  const result = stripV2TodoIdentityFields(args)
  assert.equal(result, originalArgs, 'C: strip mutates the args object in place')
  assert.notEqual(args.todos, originalTodos, 'C: todos array is replaced with V1 rows')
  assert.equal(args.todos.every((t) => !('id' in t) && !('kind' in t)), true)
})

// ── Canary F — after failure path ───────────────────────────────────────────

test('MAGIC_TODO_CANARY_F_after_does_not_run_when_executor_throws', async () => {
  const hooks = createMagicTodoContractHooks()
  let afterCalls = 0
  const after = async () => {
    afterCalls += 1
  }

  const observation = await runHostV1ToolExecutePath({
    toolID: 'todowrite',
    sessionID: SESSION,
    callID: CALL,
    args: {
      todos: [{ content: 'will-throw', status: 'pending', priority: 'high' }],
    },
    before: hooks['tool.execute.before'],
    after,
    execute: async () => {
      throw new Error('executor boom')
    },
  })

  assert.equal(observation.executeThrew, true, 'F: executor throw is observed')
  assert.equal(observation.executeError?.message, 'executor boom')
  assert.equal(observation.afterRan, false, 'F: FREEZE — after does not run when executor throws')
  assert.equal(afterCalls, 0, 'F: after hook body never invoked on throw')
})

test('MAGIC_TODO_CANARY_F_after_runs_when_executor_succeeds', async () => {
  // Control: same path with success must invoke after (proves the freeze is path-sensitive).
  const hooks = createMagicTodoContractHooks()
  let afterCalls = 0
  const after = async (_input, output) => {
    afterCalls += 1
    output.output = 'enriched'
  }

  const observation = await runHostV1ToolExecutePath({
    toolID: 'todowrite',
    sessionID: SESSION,
    callID: CALL,
    args: {
      todos: [{ content: 'ok', status: 'pending', priority: 'high' }],
    },
    before: hooks['tool.execute.before'],
    after,
    execute: async (params) => ({
      title: '1 todos',
      output: JSON.stringify(params.todos),
      metadata: { todos: params.todos },
    }),
  })

  assert.equal(observation.executeThrew, false)
  assert.equal(observation.afterRan, true, 'F control: after runs on executor success')
  assert.equal(afterCalls, 1)
  assert.equal(observation.afterOutput.output, 'enriched')
})

// ── Canary A preconditions — registration + positional in-place mutation ────

test('MAGIC_TODO_CANARY_A_PRE_before_in_place_mutation_reaches_executor_replacement_does_not', async () => {
  const inPlaceArgs = sampleV2TodoWriteArgs()
  const inPlace = await runHostV1ToolExecutePath({
    toolID: 'todowrite',
    sessionID: SESSION,
    callID: `${CALL}_inplace`,
    args: inPlaceArgs,
    before: async (_input, output) => {
      // Correct Host contract: mutate fields on the original args object.
      stripV2TodoIdentityFields(output.args)
      output.args.todos[0].status = 'in_progress'
    },
    after: async () => {},
    execute: async (params) => ({
      title: 'ok',
      output: JSON.stringify(params.todos),
      metadata: { todos: params.todos },
    }),
  })

  assert.equal(inPlace.argsIdentityUnchanged, true, 'A′: local args binding identity preserved')
  assert.equal(inPlace.replacedArgsObject, false)
  assert.equal(inPlace.decode.ok, true)
  assert.equal(inPlace.executorSawArgs, inPlaceArgs, 'A′: executor receives original args reference')
  assert.equal(inPlaceArgs.todos[0].status, 'in_progress', 'A′: in-place field writes are visible')
  assert.equal('id' in inPlaceArgs.todos[0], false, 'A′: in-place strip visible on original object')

  // Anti-pattern: replacing output.args entirely (Host does not rebind).
  const original = sampleV2TodoWriteArgs()
  const replaced = await runHostV1ToolExecutePath({
    toolID: 'todowrite',
    sessionID: SESSION,
    callID: `${CALL}_replace`,
    args: original,
    before: async (_input, output) => {
      output.args = {
        todos: [{ content: 'replaced-never-seen', status: 'completed', priority: 'low' }],
      }
    },
    after: async () => {},
    execute: async (params) => ({
      title: 'ok',
      output: JSON.stringify(params.todos),
      metadata: { todos: params.todos },
    }),
  })

  assert.equal(replaced.replacedArgsObject, true, 'A′: before replaced the output.args object')
  assert.equal(
    replaced.executorSawArgs,
    original,
    'A′: FREEZE — executor still sees the pre-before args reference',
  )
  assert.notDeepEqual(
    replaced.executorSawArgs.todos[0].content,
    'replaced-never-seen',
    'A′: full args replacement does not reach executor',
  )
  // Original still carries V2 identity fields because replacement never mutated it.
  assert.equal('id' in original.todos[0], true, 'A′: unmutated original retains provider id')
})

test('MAGIC_TODO_CANARY_A_PRE_definition_before_after_accept_host_positional_trigger', async () => {
  const hooks = createMagicTodoContractHooks()

  const defOut = {
    description: 'seed',
    parameters: V1_TODOWRITE_PARAMETERS,
    jsonSchema: { type: 'object' },
  }
  await hostTrigger(hooks['tool.definition'], { toolID: 'todowrite' }, defOut)
  assert.notEqual(defOut.description, 'seed', 'A′: definition is positional (input, output)')

  const args = sampleV2TodoWriteArgs()
  const beforeOut = { args }
  await hostTrigger(
    hooks['tool.execute.before'],
    { tool: 'todowrite', sessionID: SESSION, callID: CALL },
    beforeOut,
  )
  assert.equal('id' in beforeOut.args.todos[0], false, 'A′: before is positional (input, output)')

  const afterOut = { title: 't', output: 'o', metadata: {} }
  await hostTrigger(
    hooks['tool.execute.after'],
    { tool: 'todowrite', sessionID: SESSION, callID: CALL, args: beforeOut.args },
    afterOut,
  )
  assert.equal(afterOut.title, 't', 'A′: after accepts Host positional shape without throw')
})
