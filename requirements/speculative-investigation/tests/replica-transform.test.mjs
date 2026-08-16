import assert from 'node:assert/strict'
import test from 'node:test'

import * as Transform from '../../../dist/Strength/Replica/Transform.js'
import * as Authority from '../../../dist/Interaction/Authority/Model.js'
import * as Provider from '../../../dist/Participant/Provider/Projection/Model.js'
import { StrengthBudget } from '../../../dist/Strength/Budget.js'
import { ProviderRequestKind } from '../../../dist/Context/Prefix/Candidate.js'
import * as WireCapture from '../../../dist/OpenCode/Codec/ProviderWireCapture.js'
import * as MessageEdit from '../../../dist/OpenCode/Codec/ProjectionMessageEdit.js'
import * as Id from '../../../dist/Foundation/Identity.js'
import { Role } from '../../../dist/Foundation/Roles.js'
import * as Runtime from '../../../dist/Strength/Runtime.js'
import { toList, listItems } from '../../verification-system/tests/support/domain.mjs'

const H = (text) => `H(${text})`
const resultOf = (value) => value.tag === 0
  ? { ok: true, value: value.fields[0] }
  : { ok: false, error: value.fields[0] }
const caseOf = (value) => value.cases()[value.tag]
const tryFindByReplica = (r, id) => {
  const fn = Object.entries(Runtime).find(([k]) => k.startsWith('StrengthRuntime__TryFindByReplica_'))?.[1]
  return fn(r, id)
}
const session = (value) => Id.SessionIdModule_create(value)
const decision = (value) => Id.StrengthDecisionIdModule_create(value)
const run = (value) => Id.ProviderRunIdentityModule_create(value)
const callId = (value) => Id.ToolCallIdModule_create(value)
const text = (value) => new Provider.WirePart(0, [value])
const call = (id, name, args) => new Provider.WirePart(2, [callId(id), name, args])
const result = (id, value) => new Provider.WirePart(3, [callId(id), value])
const message = (role, parts) => ({ Role: role, Parts: toList(parts) })

const mirror = toList([message('user', [text('owner mirror')])])

const rawOutput = (replica, messages) => {
  const rendered = {
    Messages: toList(messages),
    HostMessageIds: toList(messages.map(() => undefined)),
    HostIsPhysical: toList(messages.map(() => false)),
  }
  const applied = resultOf(MessageEdit.tryApplyRenderedMessages(replica, H, rendered))
  assert.equal(applied.ok, true)
  return { messages: listItems(applied.value) }
}

const binding = (replica, budget) => new Runtime.StrengthReplicaBinding(
  session('owner'),
  session(replica),
  decision(`decision-${replica}`),
  run(`target-${replica}`),
  Role.Coder,
  budget,
  65536,
  `semantic-${replica}`,
  mirror,
  Authority.toolCapabilitiesFor(Role.Coder, ProviderRequestKind.StrengthReplica),
)

const registered = (replica, budget) => {
  const runtime = Runtime.StrengthRuntime_$ctor()
  const registerFn = Object.entries(Runtime).find(([k]) => k.startsWith('StrengthRuntime__Register_'))?.[1]
  assert.equal(registerFn(runtime, binding(replica, budget)).tag, 0)
  return runtime
}

const sessions = () => {
  const aborted = []
  return {
    aborted,
    port: {
      AbortSession: async (id) => {
        aborted.push(Id.SessionIdModule_value(id))
        return undefined
      },
    },
  }
}

test('WHAT[SPEC-INV-009] STRENGTH_003_004_replica_initial_transform_replaces_bootstrap_with_frozen_owner_mirror', async () => {
  const runtime = registered('replica-initial', StrengthBudget.K1)
  const host = sessions()
  const output = rawOutput('replica-initial', [message('user', [text('Continue.')])])
  const hostMessagesBinding = output.messages

  const outcome = await Transform.StrengthReplicaTransform_apply(H, runtime, host.port, output)
  assert.equal(caseOf(outcome), 'Ready')
  assert.equal(output.messages, hostMessagesBinding, 'Host keeps its original messages array binding after the hook')
  assert.equal(listItems(outcome.fields[0]).length, 0)
  assert.deepEqual(host.aborted, [])

  const decoded = WireCapture.decodeMessageView(toList(output.messages))
  const messages = listItems(decoded.Messages)
  assert.equal(messages.length, 1)
  assert.equal(messages[0].Parts.head.fields[0], 'owner mirror')
})

test('WHAT[SPEC-INV-003] STRENGTH_003_K1_aborts_before_provider_request_2_after_one_complete_batch', async () => {
  const runtime = registered('replica-k1', StrengthBudget.K1)
  const host = sessions()
  const output = rawOutput('replica-k1', [
    message('user', [text('Continue.')]),
    message('assistant', [call('c1', 'read', '{"filePath":"a"}')]),
    message('tool', [result('c1', 'alpha')]),
  ])

  const outcome = await Transform.StrengthReplicaTransform_apply(H, runtime, host.port, output)
  assert.equal(caseOf(outcome), 'Retired')
  assert.equal(outcome.fields[0], 'provider-request-budget-reached')
  assert.equal(listItems(outcome.fields[1]).length, 1)
  assert.deepEqual(host.aborted, ['replica-k1'])
  assert.equal(tryFindByReplica(runtime, session('replica-k1')), undefined)

  // Transform-level K+1 stop, not the live Host nested-session canary: a later
  // transform on the same child cannot re-admit a provider request.
  const again = await Transform.StrengthReplicaTransform_apply(H, runtime, host.port, output)
  assert.equal(caseOf(again), 'NotReplica')
})

test('WHAT[SPEC-INV-003] STRENGTH_003_K1_counts_OpenCode_completed_tool_part_as_one_real_request', async () => {
  const runtime = registered('replica-host-k1', StrengthBudget.K1)
  const host = sessions()
  const output = {
    messages: [
      {
        info: { id: 'u1', role: 'user', sessionID: 'replica-host-k1' },
        parts: [{ id: 'p-u1', type: 'text', text: 'Continue.' }],
      },
      {
        info: {
          id: 'a1',
          role: 'assistant',
          sessionID: 'replica-host-k1',
          parentID: 'u1',
          finish: 'tool-calls',
          time: { created: 1, completed: 2 },
        },
        parts: [{
          id: 'p-tool-1',
          type: 'tool',
          tool: 'read',
          callID: 'c1',
          state: {
            status: 'completed',
            input: { filePath: 'README.md' },
            output: 'alpha',
          },
        }],
      },
      {
        info: {
          id: 'a2',
          role: 'assistant',
          sessionID: 'replica-host-k1',
          parentID: 'u1',
          time: { created: 3 },
        },
        parts: [],
      },
    ],
  }

  const outcome = await Transform.StrengthReplicaTransform_apply(H, runtime, host.port, output)
  assert.equal(caseOf(outcome), 'Retired')
  assert.equal(outcome.fields[0], 'provider-request-budget-reached')
  assert.equal(listItems(outcome.fields[1]).length, 1)
  const [exchange] = listItems(outcome.fields[1].head.Exchanges)
  assert.equal(exchange.ToolName, 'read')
  assert.equal(exchange.CanonicalArguments, '{"filePath":"README.md"}')
  assert.equal(exchange.CanonicalResult, 'alpha')
  assert.deepEqual(host.aborted, ['replica-host-k1'])
})

test('WHAT[SPEC-INV-003] STRENGTH_003_K2_allows_request_2_then_aborts_before_request_3', async () => {
  const runtime = registered('replica-k2', StrengthBudget.K2)
  const host = sessions()
  const afterFirst = rawOutput('replica-k2', [
    message('user', [text('Continue.')]),
    message('assistant', [call('c1', 'grep', '{"pattern":"x"}')]),
    message('tool', [result('c1', 'a:1:x')]),
  ])

  const ready = await Transform.StrengthReplicaTransform_apply(H, runtime, host.port, afterFirst)
  assert.equal(caseOf(ready), 'Ready')
  assert.equal(listItems(ready.fields[0]).length, 1)
  assert.deepEqual(host.aborted, [])

  const afterSecond = rawOutput('replica-k2', [
    message('user', [text('Continue.')]),
    message('assistant', [call('c1', 'grep', '{"pattern":"x"}')]),
    message('tool', [result('c1', 'a:1:x')]),
    message('assistant', [call('c2', 'glob', '{"pattern":"**/*.fs"}')]),
    message('tool', [result('c2', 'a.fs')]),
  ])

  const retired = await Transform.StrengthReplicaTransform_apply(H, runtime, host.port, afterSecond)
  assert.equal(caseOf(retired), 'Retired')
  assert.equal(retired.fields[0], 'provider-request-budget-reached')
  assert.equal(listItems(retired.fields[1]).length, 2)
  assert.deepEqual(host.aborted, ['replica-k2'])
})

test('WHAT[SPEC-INV-003] STRENGTH_003_K2_counts_parallel_OpenCode_tool_parts_as_one_request_then_stops_before_request_3', async () => {
  const runtime = registered('replica-host-k2', StrengthBudget.K2)
  const host = sessions()
  const output = {
    messages: [
      {
        info: { id: 'u1', role: 'user', sessionID: 'replica-host-k2' },
        parts: [{ id: 'p-u1', type: 'text', text: 'Continue.' }],
      },
      {
        info: {
          id: 'a1', role: 'assistant', sessionID: 'replica-host-k2', parentID: 'u1',
          finish: 'tool-calls', time: { created: 1, completed: 2 },
        },
        parts: [
          {
            id: 'p-tool-1', type: 'tool', tool: 'read', callID: 'c1',
            state: { status: 'completed', input: { filePath: 'README.md' }, output: 'alpha' },
          },
          {
            id: 'p-tool-2', type: 'tool', tool: 'grep', callID: 'c2',
            state: { status: 'completed', input: { pattern: 'Strength' }, output: 'README.md:1:Strength' },
          },
        ],
      },
      {
        info: {
          id: 'a2', role: 'assistant', sessionID: 'replica-host-k2', parentID: 'u1',
          finish: 'tool-calls', time: { created: 3, completed: 4 },
        },
        parts: [{
          id: 'p-tool-3', type: 'tool', tool: 'glob', callID: 'c3',
          state: { status: 'completed', input: { pattern: '**/*.md' }, output: 'README.md' },
        }],
      },
      {
        info: { id: 'a3', role: 'assistant', sessionID: 'replica-host-k2', parentID: 'u1', time: { created: 5 } },
        parts: [],
      },
    ],
  }

  const retired = await Transform.StrengthReplicaTransform_apply(H, runtime, host.port, output)
  assert.equal(caseOf(retired), 'Retired')
  assert.equal(retired.fields[0], 'provider-request-budget-reached')
  const batches = listItems(retired.fields[1])
  assert.equal(batches.length, 2, 'two provider messages spend K2 even though request #1 has two tools')
  assert.deepEqual(listItems(batches[0].Exchanges).map((exchange) => exchange.ToolName), ['read', 'grep'])
  assert.equal(listItems(batches[1].Exchanges)[0].ToolName, 'glob')
  assert.deepEqual(host.aborted, ['replica-host-k2'])
})

test('WHAT[SPEC-INV-003] STRENGTH_003_005_transform_discards_incomplete_and_invalid_batches_then_stops_the_replica', async () => {
  // Discard-on-error at the transform program. Not a live Host provider-failure canary.
  // K2 so one complete illegal batch is below requestLimit and hits tryBuild,
  // not the K1 budget-reached shortcut.
  const runtime = registered('replica-discard', StrengthBudget.K2)
  const host = sessions()
  const incomplete = rawOutput('replica-discard', [
    message('user', [text('Continue.')]),
    message('assistant', [call('c1', 'read', '{"filePath":"a"}')]),
  ])

  const ready = await Transform.StrengthReplicaTransform_apply(H, runtime, host.port, incomplete)
  assert.equal(caseOf(ready), 'Ready')
  assert.equal(listItems(ready.fields[0]).length, 0)
  assert.deepEqual(host.aborted, [])

  const forgedWrite = rawOutput('replica-discard', [
    message('user', [text('Continue.')]),
    message('assistant', [call('w1', 'write', '{"filePath":"a","content":"no"}')]),
    message('tool', [result('w1', 'ok')]),
  ])
  const retired = await Transform.StrengthReplicaTransform_apply(H, runtime, host.port, forgedWrite)
  assert.equal(caseOf(retired), 'Retired')
  assert.match(retired.fields[0], /invalid-replica-frame/)
  assert.equal(listItems(retired.fields[1]).length, 1)
  assert.deepEqual(host.aborted, ['replica-discard'])
  assert.equal(tryFindByReplica(runtime, session('replica-discard')), undefined)
})
