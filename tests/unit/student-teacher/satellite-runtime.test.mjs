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
import { ofArray } from '../../../dist/fable_modules/fable-library-js.5.13.0/List.js'
import {
  FSharpResult$2_Error$ as error,
  FSharpResult$2_Ok as ok,
} from '../../../dist/fable_modules/fable-library-js.5.13.0/Result.js'

// HOST-015: satellites are physically parented to the family root ('root'),
// never to their logical owner ('student'). Ownership is proven by the
// journal-linked SessionId (spec.restored), never by Host parentID.
const child = (id, parent = 'root', agent = 'fast-teacher', title = 'fast-teacher') =>
  new OpenCodeChildInfo(sessionId(id), sessionId(parent), agent, title)

const spec = ({ restored, linked = [] } = {}) =>
  new SatelliteSpec(
    SatelliteKind.Teacher,
    'fast-teacher',
    'fast-teacher',
    '/workspace',
    restored === undefined ? undefined : sessionId(restored),
    (owner, satellite, agent) => {
      linked.push([owner.fields[0], satellite.fields[0], agent])
      return ok(undefined)
    },
    () => ok(undefined),
  )

const host = (children, created = []) => ({
  ListChildren: async () => ok(ofArray(children)),
  CreateChildSession: async () => {
    const id = sessionId(`created-${created.length + 1}`)
    created.push(id.fields[0])
    return ok(id)
  },
  AbortSession: async () => ok(undefined),
  FamilyRootOf: () => sessionId('root'),
})

test('HOST_015_satellite_recovery_reuses_journal_linked_child_under_flat_root', async () => {
  const linked = []
  const created = []
  // Physical parent is 'root', not the owner — reuse must not depend on parentID.
  const runtime = createRuntime(host([child('teacher-1', 'root')], created))

  const result = await ensure(runtime, sessionId('student'), spec({ restored: 'teacher-1', linked }))

  assert.equal(result.tag, 0)
  assert.equal(result.fields[0].SessionId.fields[0], 'teacher-1')
  assert.equal(result.fields[0].Origin.tag, SatelliteOrigin.Reused.tag)
  assert.deepEqual(created, [])
  assert.deepEqual(linked, [['student', 'teacher-1', 'fast-teacher']])
})

test('HOST_015_satellite_recovery_creates_an_explicit_replacement_when_the_old_child_is_gone', async () => {
  const linked = []
  const created = []
  const runtime = createRuntime(host([], created))

  const result = await ensure(runtime, sessionId('student'), spec({ restored: 'teacher-old', linked }))

  assert.equal(result.tag, 0)
  assert.equal(result.fields[0].SessionId.fields[0], 'created-1')
  assert.equal(result.fields[0].Origin.tag, SatelliteOrigin.Replacement.tag)
  assert.deepEqual(created, ['created-1'])
  assert.deepEqual(linked, [['student', 'created-1', 'fast-teacher']])
})

test('HOST_015_satellite_recovery_fails_closed_when_journal_linked_child_conflicts', async () => {
  const linked = []
  const created = []
  // Journal links teacher-1, but the Host child with that id carries a
  // different agent — ownership conflict, never reuse, never create.
  const runtime = createRuntime(host([child('teacher-1', 'root', 'other-agent')], created))

  const result = await ensure(runtime, sessionId('student'), spec({ restored: 'teacher-1', linked }))

  assert.equal(result.tag, 1)
  assert.match(result.fields[0], /Conflicting teacher satellite recovery/)
  assert.deepEqual(created, [])
  assert.deepEqual(linked, [])
})

test('HOST_015_satellite_recovery_never_adopts_same_agent_sibling_without_journal_link', async () => {
  const linked = []
  const created = []
  // A same-agent/title child sits under the shared flat root (it belongs to
  // another work session). Without a journal link there is no proof of
  // ownership — always create a fresh child.
  const runtime = createRuntime(host([child('teacher-1', 'root')], created))

  const result = await ensure(runtime, sessionId('student'), spec({ linked }))

  assert.equal(result.tag, 0)
  assert.equal(result.fields[0].SessionId.fields[0], 'created-1')
  assert.equal(result.fields[0].Origin.tag, SatelliteOrigin.Created.tag)
  assert.deepEqual(created, ['created-1'])
  assert.deepEqual(linked, [['student', 'created-1', 'fast-teacher']])
})

test('HOST_015_satellite_recovery_replaces_without_adopting_same_agent_sibling', async () => {
  const linked = []
  const created = []
  // Journal links teacher-old (gone from the Host); teacher-other is another
  // owner's satellite under the same flat root. Replacement must create a new
  // child and leave the sibling untouched.
  const runtime = createRuntime(host([child('teacher-other', 'root')], created))

  const result = await ensure(runtime, sessionId('student'), spec({ restored: 'teacher-old', linked }))

  assert.equal(result.tag, 0)
  assert.equal(result.fields[0].SessionId.fields[0], 'created-1')
  assert.equal(result.fields[0].Origin.tag, SatelliteOrigin.Replacement.tag)
  assert.deepEqual(created, ['created-1'])
  assert.deepEqual(linked, [['student', 'created-1', 'fast-teacher']])
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
  const owner = sessionId('student')
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

  const result = await ensure(runtime, sessionId('student'), spec({ restored: 'teacher-old' }))

  assert.equal(result.tag, 1)
  assert.match(result.fields[0], /Cannot recover teacher satellite: children unavailable/)
  assert.deepEqual(created, [])
})
