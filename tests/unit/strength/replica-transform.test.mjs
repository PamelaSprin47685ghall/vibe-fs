import assert from 'node:assert/strict'
import test from 'node:test'

import * as Transform from '../../../dist/Application/Strength/StrengthReplicaTransform.js'
import * as Authority from '../../../dist/Domain/PromptAuthority.js'
import * as Provider from '../../../dist/Domain/ProviderProjection.js'
import { StrengthBudget } from '../../../dist/Domain/StrengthBudget.js'
import { ProviderRequestKind } from '../../../dist/Domain/PrefixCandidate.js'
import * as Projection from '../../../dist/Infrastructure/OpenCode/Codec/Projection.js'
import * as Id from '../../../dist/Kernel/Identity.js'
import { Role } from '../../../dist/Kernel/Roles.js'
import * as Runtime from '../../../dist/Session/StrengthRuntime.js'
import { ofArray as toList, toArray as listItems } from '../../../dist/fable_modules/fable-library-js.5.13.0/List.js'

const H = (text) => `H(${text})`
const resultOf = (value) => value.tag === 0
  ? { ok: true, value: value.fields[0] }
  : { ok: false, error: value.fields[0] }
const caseOf = (value) => value.cases()[value.tag]
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
  const applied = resultOf(Projection.tryApplyRenderedMessages(replica, H, rendered))
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
  assert.equal(Runtime.StrengthRuntime__Register_Z18AE9AF0(runtime, binding(replica, budget)).tag, 0)
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

test('STRENGTH_003_004_replica_initial_transform_replaces_bootstrap_with_frozen_owner_mirror', async () => {
  const runtime = registered('replica-initial', StrengthBudget.K1)
  const host = sessions()
  const output = rawOutput('replica-initial', [message('user', [text('Continue.')])])

  const outcome = await Transform.StrengthReplicaTransform_apply(H, runtime, host.port, output)
  assert.equal(caseOf(outcome), 'Ready')
  assert.equal(listItems(outcome.fields[0]).length, 0)
  assert.deepEqual(host.aborted, [])

  const decoded = Projection.decodeMessageView(toList(output.messages))
  const messages = listItems(decoded.Messages)
  assert.equal(messages.length, 1)
  assert.equal(messages[0].Parts.head.fields[0], 'owner mirror')
})

test('STRENGTH_003_K1_aborts_before_provider_request_2_after_one_complete_batch', async () => {
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
  assert.equal(Runtime.StrengthRuntime__TryFindByReplica_Z31B28506(runtime, session('replica-k1')), undefined)

  // Transform-level K+1 stop, not the live Host nested-session canary: a later
  // transform on the same child cannot re-admit a provider request.
  const again = await Transform.StrengthReplicaTransform_apply(H, runtime, host.port, output)
  assert.equal(caseOf(again), 'NotReplica')
})

test('STRENGTH_003_K2_allows_request_2_then_aborts_before_request_3', async () => {
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

test('STRENGTH_003_005_transform_discards_incomplete_and_invalid_batches_then_stops_the_replica', async () => {
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
  assert.equal(Runtime.StrengthRuntime__TryFindByReplica_Z31B28506(runtime, session('replica-discard')), undefined)
})
