// Split from tests/unit/plugin/magic-todo-host-canaries.test.mjs
// (cutover Wave 2a); owner: host-boundary.
//
// Magic Todo Phase 0 — Host V1 contract canaries H and A′: the durable-call →
// provider-run/XTrace carrier (journal mapping) and the registration /
// positional in-place mutation preconditions. Both pin the Host boundary
// shape, not the obligation-ledger domain (canaries B/C/F moved with
// obligation-ledger).
//
// Real-host A (durable ToolPart.input alias), E, G, H live under the e2e
// real-host harness. This file freezes contracts that isolated helpers can
// prove without a full OpenCode lifetime.
//
// No production Magic Todo membrane is wired here.

import assert from 'node:assert/strict'
import test from 'node:test'
import { buildCarrierEvidence } from '../../verification-system/tests/e2e/support/magic-todo-host-canary-plugin.mjs'
import {
  createMagicTodoContractHooks,
  hostTrigger,
  runHostV1ToolExecutePath,
  sampleObligationTodoWriteArgs,
  projectObligationsToV1TodoRows,
  V1_TODOWRITE_PARAMETERS,
} from '../../verification-system/tests/support/plugin-fixture.mjs'

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
