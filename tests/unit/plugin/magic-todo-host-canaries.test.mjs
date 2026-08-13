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
import { buildCarrierEvidence } from '../../e2e/support/magic-todo-host-canary-plugin.mjs'
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
} from './plugin-fixture.mjs'

const SESSION = 'ses_magic_todo_canary'
const CALL = 'call_magic_todo_1'

// ── Canary H — durable call → provider-run/XTrace carrier ───────────────────

test('MAGIC_TODO_CANARY_H_journal_xtrace_uniquely_completes_host_carrier', () => {
  const locate = {
    sessionID: SESSION,
    callID: CALL,
    matchCount: 1,
    unique: true,
    match: {
      messageID: 'msg_provider_run_1',
      partID: 'prt_tool_1',
      ordinal: 3,
      toolOrdinal: 0,
      assistant: { id: 'msg_provider_run_1' },
      part: { id: 'prt_tool_1', sessionID: SESSION, messageID: 'msg_provider_run_1', type: 'tool', callID: CALL },
    },
  }
  const carrier = buildCarrierEvidence(locate, [
    {
      SessionId: ['SessionId', SESSION],
      ToolCallId: ['ToolCallId', CALL],
      HostToolPartId: ['HostToolPartId', 'prt_tool_1'],
      ProviderRun: ['ProviderRunIdentity', 'msg_provider_run_1'],
      CursorSequence: '7',
      Kind: 'tool_call',
    },
    {
      SessionId: ['SessionId', SESSION],
      ToolCallId: ['ToolCallId', CALL],
      HostToolPartId: ['HostToolPartId', 'prt_tool_1'],
      ProviderRun: ['ProviderRunIdentity', 'msg_provider_run_1'],
      CursorSequence: '11',
      Kind: 'tool_result',
    },
  ])

  assert.equal(carrier.journalMappingAvailable, true)
  assert.equal(carrier.journalMappingMatchCount, 2)
  assert.equal(carrier.journalProviderRun, 'msg_provider_run_1')
  assert.deepEqual(carrier.journalXTraceRange, { start: 7, endExclusive: 12 })
  assert.equal(carrier.carrierMappingComplete, true)
})

test('MAGIC_TODO_CANARY_H_journal_mapping_fails_closed_on_host_part_mismatch', () => {
  const locate = {
    sessionID: SESSION,
    callID: CALL,
    matchCount: 1,
    unique: true,
    match: {
      messageID: 'msg_provider_run_1',
      partID: 'prt_tool_1',
      ordinal: 3,
      toolOrdinal: 0,
      assistant: { id: 'msg_provider_run_1' },
      part: { id: 'prt_tool_1', sessionID: SESSION, messageID: 'msg_provider_run_1', type: 'tool', callID: CALL },
    },
  }
  const carrier = buildCarrierEvidence(locate, [
    {
      SessionId: ['SessionId', SESSION],
      ToolCallId: ['ToolCallId', CALL],
      HostToolPartId: ['HostToolPartId', 'prt_other'],
      ProviderRun: ['ProviderRunIdentity', 'msg_provider_run_1'],
      CursorSequence: '7',
      Kind: 'tool_result',
    },
  ])

  assert.equal(carrier.journalMappingAvailable, false)
  assert.equal(carrier.carrierMappingComplete, false)
})

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
  const providerItem = defined.parameters.properties.obligations.items
  assert.deepEqual(providerItem.required, ['name', 'work'])
  assert.equal(providerItem.properties.id, undefined)
  assert.equal(providerItem.properties.kind, undefined)
  assert.equal(providerItem.properties.status, undefined)
  assert.equal(providerItem.properties.priority, undefined)
  assert.deepEqual(defined.jsonSchema.required, ['obligations'])

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
  assert.equal(JSON.stringify(args), JSON.stringify({ obligations: originalObligations }))
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

// ── Canary A preconditions — registration + positional in-place mutation ────

test('MAGIC_TODO_CANARY_A_PRE_before_in_place_mutation_reaches_executor_replacement_does_not', async () => {
  const inPlaceArgs = sampleObligationTodoWriteArgs()
  const inPlace = await runHostV1ToolExecutePath({
    toolID: 'todowrite',
    sessionID: SESSION,
    callID: `${CALL}_inplace`,
    args: inPlaceArgs,
    before: async (_input, output) => {
      // Correct Host contract: mutate fields on the original args object.
      projectObligationsToV1TodoRows(output.args)
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
  assert.equal('obligations' in inPlaceArgs, true, 'A′: provider account remains intact for Host materialization')
  assert.equal(Object.prototype.propertyIsEnumerable.call(inPlaceArgs, 'todos'), false)
  assert.equal(JSON.stringify(inPlaceArgs), JSON.stringify({ obligations: inPlaceArgs.obligations }))

  // Anti-pattern: replacing output.args entirely (Host does not rebind).
  const original = sampleObligationTodoWriteArgs()
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
  assert.equal(replaced.decode.ok, false, 'A′: replacement cannot rescue the original V1 decoder binding')
  assert.equal(replaced.afterRan, false)
  assert.equal('obligations' in replaced.executorSawArgs, true, 'A′: original provider args remain untouched')
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

  const args = sampleObligationTodoWriteArgs()
  const beforeOut = { args }
  await hostTrigger(
    hooks['tool.execute.before'],
    { tool: 'todowrite', sessionID: SESSION, callID: CALL },
    beforeOut,
  )
  assert.equal('obligations' in beforeOut.args, true, 'A′: before preserves provider input bytes')
  assert.equal(Object.prototype.propertyIsEnumerable.call(beforeOut.args, 'todos'), false)
  assert.equal(beforeOut.args.todos[0].status, 'in_progress')

  const afterOut = { title: 't', output: 'o', metadata: {} }
  await hostTrigger(
    hooks['tool.execute.after'],
    { tool: 'todowrite', sessionID: SESSION, callID: CALL, args: beforeOut.args },
    afterOut,
  )
  assert.equal(afterOut.title, 't', 'A′: after accepts Host positional shape without throw')
})
