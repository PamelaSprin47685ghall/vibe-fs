// Split from tests/unit/plugin/magic-todo-host-canaries.test.mjs
// (cutover Wave 2a); owner: obligation-ledger.
//
// Magic Todo Phase 0 — Host V1 contract canaries B, C, F. The Host-side
// definition/compatibility/after-failure contracts this package owns:
//
//   B  tool.definition updates description + parameters + jsonSchema while
//      the original V1 decoder remains the execute-path decoder
//   C  stripping V2 id/kind lets the original V1 decoder succeed on
//      compatibility rows
//   F  freeze whether tool.execute.after runs when the executor throws
//
// Canary H (journal XTrace carrier) and A′ (registration + positional
// in-place mutation preconditions) moved with host-boundary (定位与 carrier).
// No production Magic Todo membrane is wired here.

import assert from 'node:assert/strict'
import test from 'node:test'
import {
  applyToolDefinitionHook,
  createMagicTodoContractHooks,
  decodeV1TodoWriteArgs,
  hostTrigger,
  runHostV1ToolExecutePath,
  sampleObligationTodoWriteAdvertisement,
  sampleObligationTodoWriteArgs,
  projectObligationsToV1TodoRows,
  v1TodoWriteToolSeed,
  V1_TODOWRITE_PARAMETERS,
} from '../../verification-system/tests/support/plugin-fixture.mjs'

const SESSION = 'ses_magic_todo_canary'
const CALL = 'call_magic_todo_1'

// ── Canary B — definition schema ────────────────────────────────────────────

test('MAGIC_TODO_CANARY_B_definition_replaces_description_parameters_jsonSchema_original_decoder_unchanged', async () => {
  const hooks = createMagicTodoContractHooks()
  const seed = v1TodoWriteToolSeed()
  const advertised = sampleObligationTodoWriteAdvertisement()

  const defined = await applyToolDefinitionHook(hooks['tool.definition'], seed)

  assert.equal(defined.description, advertised.description, 'B: description must be replaced')
  // Structural replacement (deep equal); Host may rebuild schema objects without
  // preserving advertisement object identity.
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

  // Provider-facing advertisement is the clean-break account; legacy sink
  // fields do not cross the horizon. The original executor decoder stays V1.
  assert.equal(defined.parameters.properties.planComplete.type, 'boolean')
  const providerItem = defined.parameters.properties.obligations.items
  assert.deepEqual(providerItem.required, ['name', 'work'])
  assert.equal(providerItem.properties.id, undefined)
  assert.equal(providerItem.properties.kind, undefined)
  assert.equal(providerItem.properties.status, undefined)
  assert.equal(providerItem.properties.priority, undefined)
  assert.deepEqual(defined.jsonSchema.required, ['planComplete', 'obligations'])

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

// ── Canary C — obligation account → compatibility sink ─────────────────────

test('MAGIC_TODO_CANARY_C_obligations_project_to_original_v1_decoder_shape', async () => {
  const hooks = createMagicTodoContractHooks()
  const raw = sampleObligationTodoWriteArgs()

  // Clean-break provider args are intentionally not the builtin V1 decoder shape.
  const rawDecoded = decodeV1TodoWriteArgs(structuredClone(raw))
  assert.equal(rawDecoded.ok, false, 'C: raw obligations must require the membrane before V1 execute')

  const args = structuredClone(raw)
  const beforeOutput = { args }
  await hostTrigger(hooks['tool.execute.before'], { tool: 'todowrite', sessionID: SESSION, callID: CALL }, beforeOutput)

  assert.equal('obligations' in beforeOutput.args, true, 'C: provider input remains materializable')
  assert.equal(Object.prototype.propertyIsEnumerable.call(beforeOutput.args, 'todos'), false, 'C: compatibility view stays off JSON persistence')
  assert.equal(JSON.stringify(beforeOutput.args), JSON.stringify(raw), 'C: JSON persistence remains provider obligations only')
  for (const todo of beforeOutput.args.todos) {
    assert.equal(typeof todo.content, 'string')
    assert.equal(todo.status, 'in_progress')
    assert.equal(todo.priority, 'medium')
  }

  const decoded = decodeV1TodoWriteArgs(beforeOutput.args)
  assert.equal(decoded.ok, true, 'C: original V1 decoder succeeds after projection')
  assert.equal(decoded.value.todos.length, raw.obligations.length)
  assert.deepEqual(
    decoded.value.todos.map((t) => t.content),
    raw.obligations.map((t) => `${t.name}: ${t.work}`),
  )
})

test('MAGIC_TODO_CANARY_C_projection_helper_mutates_original_args_in_place', () => {
  const args = sampleObligationTodoWriteArgs()
  const originalArgs = args
  const originalObligations = args.obligations
  const result = projectObligationsToV1TodoRows(args)
  assert.equal(result, originalArgs, 'C: projection mutates the args object in place')
  assert.equal('obligations' in args, true)
  assert.equal(Object.prototype.propertyIsEnumerable.call(args, 'todos'), false)
  assert.equal(JSON.stringify(args), JSON.stringify({ planComplete: true, obligations: originalObligations }))
  assert.equal(args.todos.length, originalObligations.length)
  assert.equal(args.todos.every((t) => t.status === 'in_progress' && t.priority === 'medium'), true)
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
