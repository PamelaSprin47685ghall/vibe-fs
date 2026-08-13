// tests/unit/session/satellite-runtime.test.mjs — HOST-014 / HOST-015
//
// Companion-only SatelliteRuntime proofs. Teacher is not a SatelliteKind;
// SessionOwnership / SyncDelegate association helpers live in kernel +
// context/session-association tests.

import assert from 'node:assert/strict'
import test from 'node:test'

import {
  SatelliteRuntime_$ctor_Z39272A43 as createRuntime,
  SatelliteRuntime__Ensure_73F925B0 as ensure,
  SatelliteOrigin,
  SatelliteSpec,
} from '../../../dist/Session/SatelliteRuntime.js'
import { OpenCodeChildInfo } from '../../../dist/Infrastructure/OpenCode/Host/OpenCodePort.js'
import { SatelliteKind } from '../../../dist/Journal/SessionAssociation.js'
import { SessionIdModule_create as sessionId } from '../../../dist/Kernel/Identity.js'
import { toList } from '../support/domain.mjs'
import {
  FSharpResult$2_Error$ as error,
  FSharpResult$2_Ok as ok,
} from '../../../dist/fable_modules/fable-library-js.5.13.0/Result.js'

// HOST-015: satellites are physically parented to the family root ('root'),
// never to their logical owner ('work'). Ownership is proven by the
// journal-linked SessionId (spec.restored), never by Host parentID.
const COMPANION_AGENT = 'fast-blogger'

const child = (id, parent = 'root', agent = COMPANION_AGENT, title = COMPANION_AGENT) =>
  new OpenCodeChildInfo(sessionId(id), sessionId(parent), agent, title)

const spec = ({ restored, linked = [] } = {}) =>
  new SatelliteSpec(
    SatelliteKind.Companion,
    COMPANION_AGENT,
    COMPANION_AGENT,
    '/workspace',
    restored === undefined ? undefined : sessionId(restored),
    (owner, satellite, agent) => {
      linked.push([owner.fields[0], satellite.fields[0], agent])
      return (async () => ok(undefined))()
    },
    async () => ok(undefined),
  )

const host = (children, created = []) => ({
  ListChildren: async () => ok(toList(children)),
  CreateChildSession: async () => {
    const id = sessionId(`created-${created.length + 1}`)
    created.push(id.fields[0])
    return ok(id)
  },
  AbortSession: async () => ok(undefined),
  FamilyRootOf: () => sessionId('root'),
})

test('HOST_014_SatelliteKind_is_Companion_only', () => {
  assert.deepEqual(SatelliteKind.Companion.cases(), ['Companion'])
  assert.equal('Teacher' in SatelliteKind, false)
})

test('HOST_015_companion_satellite_recovery_reuses_journal_linked_child_under_flat_root', async () => {
  const linked = []
  const created = []
  // Physical parent is 'root', not the owner — reuse must not depend on parentID.
  const runtime = createRuntime(host([child('blogger-1', 'root')], created))

  const result = await ensure(runtime, sessionId('work'), spec({ restored: 'blogger-1', linked }))

  assert.equal(result.tag, 0)
  assert.equal(result.fields[0].SessionId.fields[0], 'blogger-1')
  assert.equal(result.fields[0].Origin.tag, SatelliteOrigin.Reused.tag)
  assert.deepEqual(created, [])
  assert.deepEqual(linked, [['work', 'blogger-1', COMPANION_AGENT]])
})

test('HOST_015_companion_satellite_recovery_creates_an_explicit_replacement_when_the_old_child_is_gone', async () => {
  const linked = []
  const created = []
  const runtime = createRuntime(host([], created))

  const result = await ensure(runtime, sessionId('work'), spec({ restored: 'blogger-old', linked }))

  assert.equal(result.tag, 0)
  assert.equal(result.fields[0].SessionId.fields[0], 'created-1')
  assert.equal(result.fields[0].Origin.tag, SatelliteOrigin.Replacement.tag)
  assert.deepEqual(created, ['created-1'])
  assert.deepEqual(linked, [['work', 'created-1', COMPANION_AGENT]])
})

test('HOST_015_companion_satellite_recovery_fails_closed_when_journal_linked_child_conflicts', async () => {
  const linked = []
  const created = []
  // Journal links blogger-1, but the Host child with that id carries a
  // different agent — ownership conflict, never reuse, never create.
  const runtime = createRuntime(host([child('blogger-1', 'root', 'other-agent')], created))

  const result = await ensure(runtime, sessionId('work'), spec({ restored: 'blogger-1', linked }))

  assert.equal(result.tag, 1)
  assert.match(result.fields[0], /Conflicting companion satellite recovery/)
  assert.deepEqual(created, [])
  assert.deepEqual(linked, [])
})

test('HOST_015_companion_satellite_recovery_never_adopts_same_agent_sibling_without_journal_link', async () => {
  const linked = []
  const created = []
  // A same-agent/title child sits under the shared flat root (it belongs to
  // another work session). Without a journal link there is no proof of
  // ownership — always create a fresh child.
  const runtime = createRuntime(host([child('blogger-1', 'root')], created))

  const result = await ensure(runtime, sessionId('work'), spec({ linked }))

  assert.equal(result.tag, 0)
  assert.equal(result.fields[0].SessionId.fields[0], 'created-1')
  assert.equal(result.fields[0].Origin.tag, SatelliteOrigin.Created.tag)
  assert.deepEqual(created, ['created-1'])
  assert.deepEqual(linked, [['work', 'created-1', COMPANION_AGENT]])
})

test('HOST_015_companion_satellite_recovery_replaces_without_adopting_same_agent_sibling', async () => {
  const linked = []
  const created = []
  // Journal links blogger-old (gone from the Host); blogger-other is another
  // owner's satellite under the same flat root. Replacement must create a new
  // child and leave the sibling untouched.
  const runtime = createRuntime(host([child('blogger-other', 'root')], created))

  const result = await ensure(runtime, sessionId('work'), spec({ restored: 'blogger-old', linked }))

  assert.equal(result.tag, 0)
  assert.equal(result.fields[0].SessionId.fields[0], 'created-1')
  assert.equal(result.fields[0].Origin.tag, SatelliteOrigin.Replacement.tag)
  assert.deepEqual(created, ['created-1'])
  assert.deepEqual(linked, [['work', 'created-1', COMPANION_AGENT]])
})

test('HOST_014_concurrent_first_ensure_is_single_flight_and_creates_one_child', async () => {
  const created = []
  let createCalls = 0
  let releaseCreate
  const createBarrier = new Promise((resolve) => {
    releaseCreate = resolve
  })
  const sessions = host([], created)
  sessions.CreateChildSession = async () => {
    createCalls += 1
    await createBarrier
    const id = sessionId(`created-${created.length + 1}`)
    created.push(id.fields[0])
    return ok(id)
  }
  const runtime = createRuntime(sessions)
  const owner = sessionId('work')
  const satelliteSpec = spec()

  const first = ensure(runtime, owner, satelliteSpec)
  const second = ensure(runtime, owner, satelliteSpec)
  releaseCreate()
  const [a, b] = await Promise.all([first, second])

  assert.equal(a.tag, 0)
  assert.equal(b.tag, 0)
  assert.equal(a.fields[0].SessionId.fields[0], b.fields[0].SessionId.fields[0])
  assert.equal(createCalls, 1)
  assert.deepEqual(created, ['created-1'])
})

test('HOST_014_children_query_failure_does_not_guess_or_create', async () => {
  const created = []
  const sessions = host([], created)
  sessions.ListChildren = async () => error('children unavailable')
  const runtime = createRuntime(sessions)

  const result = await ensure(runtime, sessionId('work'), spec({ restored: 'blogger-old' }))

  assert.equal(result.tag, 1)
  assert.match(result.fields[0], /Cannot recover companion satellite: children unavailable/)
  assert.deepEqual(created, [])
})
