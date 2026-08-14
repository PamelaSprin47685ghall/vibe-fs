// managed-session-lifecycle: AttachedSessionRuntime binding contract (HOST-008 /
// EXEC-026). The runtime is the in-process owner of (OwnerReuseScopeId,
// SyncDelegateRole) → at most one live dedicated Work+Attached session:
// GetOrCreate reuses an existing compatible binding instead of spawning a second
// child, the bound agent survives later calls, and Remove / RemoveByDelegateSession
// are the only unbind paths.

import assert from 'node:assert/strict'
import test from 'node:test'

import { caseOf, sessionId, syncDelegate } from '../../verification-system/tests/support/domain.mjs'

const {
  AttachedSessionRuntime,
  AttachedSessionRuntime__GetOrCreate_Z39C7657D: getOrCreate,
  AttachedSessionRuntime__TryFind_636E3F87: tryFind,
  AttachedSessionRuntime__TryFindByScope_15D6D21F: tryFindByScope,
  AttachedSessionRuntime__Remove_636E3F87: remove,
  AttachedSessionRuntime__RemoveByDelegateSession_Z31B28506: removeByDelegateSession,
} = await import('../../../dist/Session/AttachedSessionRuntime.js')

const { ofSession, compatible, sameScope } = await import('../../../dist/Session/ReuseScope.js')

const ok = (value) => ({ tag: 0, fields: [value] })

const live = () => {
  const creates = []
  const childSeq = { n: 0 }
  const createChild = async (_owner, agent, directory) => {
    creates.push({ agent, directory })
    childSeq.n += 1
    return ok(sessionId(`ses_child_${childSeq.n}`))
  }
  return { creates, createChild }
}

test('EXEC_026_get_or_create_creates_and_binds_a_work_child_once', async () => {
  const { creates, createChild } = live()
  const runtime = new AttachedSessionRuntime()
  const ready = []
  const onReady = (child, agent) => ready.push([child.fields[0], agent])

  const owner = sessionId('ses_owner')
  const result = await getOrCreate(runtime, owner, syncDelegate.role('Inspector'), 'deep-inspector', 'dir-x', createChild, onReady)

  assert.equal(result.tag, 0, result.tag === 1 ? result.fields[0] : '')
  assert.equal(result.fields[0][0].fields[0], 'ses_child_1')
  assert.equal(result.fields[0][1], 'deep-inspector')
  assert.deepEqual(creates, [{ agent: 'deep-inspector', directory: 'dir-x' }])
  assert.deepEqual(ready, [['ses_child_1', 'deep-inspector']])

  // The binding is visible through both lookup keys.
  assert.equal(tryFind(runtime, owner, syncDelegate.role('Inspector')).fields[0], 'ses_child_1')
  const scope = ofSession(owner)
  assert.equal(tryFindByScope(runtime, scope, syncDelegate.role('Inspector')).fields[0], 'ses_child_1')
})

test('EXEC_026_get_or_create_reuses_the_existing_binding_and_keeps_the_bound_agent', async () => {
  const { creates, createChild } = live()
  const runtime = new AttachedSessionRuntime()
  const owner = sessionId('ses_owner')
  const role = syncDelegate.role('Coder')

  const first = await getOrCreate(runtime, owner, role, 'deep-coder', undefined, createChild, () => {})
  assert.equal(first.tag, 0)
  assert.equal(first.fields[0][1], 'deep-coder')

  // A later call passes a fast agent name: reuse must NOT rebind — the stored
  // binding wins and no second child is created (EXEC-028 reuse keeps the
  // already-bound managed agent).
  const second = await getOrCreate(runtime, owner, role, 'fast-coder', undefined, createChild, () => {})
  assert.equal(second.tag, 0)
  assert.equal(second.fields[0][0].fields[0], first.fields[0][0].fields[0], 'same child session')
  assert.equal(second.fields[0][1], 'deep-coder', 'bound agent is preserved')
  assert.equal(creates.length, 1, 'no second spawn')
})

test('EXEC_026_reuse_scope_is_the_serialization_key_across_sessions', async () => {
  const a = ofSession(sessionId('ses_owner_a'))
  const b = ofSession(sessionId('ses_owner_b'))
  const same = ofSession(sessionId('ses_owner_a'))

  assert.equal(compatible(a, a), true)
  assert.equal(sameScope(a, a), true)
  assert.equal(compatible(a, same), true)
  assert.equal(compatible(a, b), false, 'different owner scope ids do not share a binding')

  // Same scope, different roles → different bindings.
  const { creates, createChild } = live()
  const runtime = new AttachedSessionRuntime()
  const owner = sessionId('ses_owner_a')
  const inspector = await getOrCreate(runtime, owner, syncDelegate.role('Inspector'), 'deep-inspector', undefined, createChild, () => {})
  const coder = await getOrCreate(runtime, owner, syncDelegate.role('Coder'), 'deep-coder', undefined, createChild, () => {})
  assert.equal(creates.length, 2, 'role is part of the binding key')
  assert.notEqual(inspector.fields[0][0].fields[0], coder.fields[0][0].fields[0])
})

test('EXEC_026_remove_and_remove_by_delegate_session_are_the_only_unbind_paths', async () => {
  const { createChild } = live()
  const runtime = new AttachedSessionRuntime()
  const owner = sessionId('ses_owner')
  const role = syncDelegate.role('Inspector')

  const created = await getOrCreate(runtime, owner, role, 'deep-inspector', undefined, createChild, () => {})
  const childId = created.fields[0][0]

  assert.equal(remove(runtime, owner, role), true)
  assert.equal(tryFind(runtime, owner, role), undefined, 'removed binding is gone')

  const recreated = await getOrCreate(runtime, owner, role, 'deep-inspector', undefined, createChild, () => {})
  const newChildId = recreated.fields[0][0]
  assert.equal(removeByDelegateSession(runtime, newChildId), true)
  assert.equal(tryFind(runtime, owner, role), undefined)

  // Remove on a missing binding answers false (idempotent no-op, not an error).
  assert.equal(remove(runtime, owner, role), false)
  assert.equal(removeByDelegateSession(runtime, sessionId('ses_never')), false)
})

test('EXEC_026_unusable_binding_is_treated_as_absent_and_recreated', async () => {
  const { creates, createChild } = live()
  const runtime = new AttachedSessionRuntime(undefined, () => false)
  const owner = sessionId('ses_owner')
  const role = syncDelegate.role('Inspector')

  await getOrCreate(runtime, owner, role, 'deep-inspector', undefined, createChild, () => {})
  await getOrCreate(runtime, owner, role, 'deep-inspector', undefined, createChild, () => {})

  assert.equal(creates.length, 2, 'an unusable (e.g. deleted) child is not reused')
  // The binding exists but the child is unusable → lookups answer absent, so the
  // next GetOrCreate spawns a fresh child (safe-side: no reuse of a dead session).
  assert.equal(tryFind(runtime, owner, role), undefined)
})
