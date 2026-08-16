// Split from tests/unit/session/satellite-runtime.test.mjs (cutover Wave 2a); owner: managed-session-lifecycle.
//
// HOST-015 companion satellite recovery/reuse/replacement/fail-closed + HOST-014
// single-flight/query-failure。HOST_014_SatelliteKind_is_Companion_only 已随
// SPLIT@cutover 迁 requirements/session-ontology/tests/satellite-kind.test.mjs。

import assert from 'node:assert/strict'
import test from 'node:test'

const satelliteModule = await import('../../../dist/Execution/Session/Attachment/SatelliteRuntime.js')
const { SatelliteRuntime, SatelliteOrigin, SatelliteSpec } = satelliteModule
const createRuntime = Object.entries(satelliteModule).find(([k]) => k.startsWith('SatelliteRuntime_$ctor'))?.[1]
const ensure = Object.entries(satelliteModule).find(([k]) => k.startsWith('SatelliteRuntime__Ensure_'))?.[1]
import { OpenCodeChildInfo } from '../../../dist/OpenCode/Host/OpenCodePort.js'
import { SatelliteKind } from '../../../dist/Execution/Session/Association.js'
import { SessionIdModule_create as sessionId, SessionIdModule_value as sessionValue } from '../../../dist/Foundation/Identity.js'
import { errorResult, okResult, toList } from '../../verification-system/tests/support/domain.mjs'
import {
  directCompanionRepointFatal,
  durableCompanionReplacement,
  failedCompanionEnsureRetry,
} from './support/satellite-recovery.mjs'

// HOST-015: satellites are physically parented to the family root ('root'),
// never to their logical owner ('work'). Ownership is proven by the
// journal-linked SessionId (spec.restored), never by Host parentID.
const COMPANION_AGENT = 'fast-blogger'

const child = (id, parent = 'root', agent = COMPANION_AGENT, title = COMPANION_AGENT) =>
  new OpenCodeChildInfo(sessionId(id), sessionId(parent), agent, title)

const spec = ({ restored, linked = [], closed = [] } = {}) =>
  new SatelliteSpec(
    SatelliteKind.Companion,
    COMPANION_AGENT,
    COMPANION_AGENT,
    '/workspace',
    restored === undefined ? undefined : sessionId(restored),
    (owner, satellite, agent) => {
      linked.push([sessionValue(owner), sessionValue(satellite), agent])
      return (async () => okResult(undefined))()
    },
    async (owner) => {
      closed.push(sessionValue(owner))
      return okResult(undefined)
    },
  )

const host = (children, created = []) => ({
  ListChildren: async () => okResult(toList(children)),
  CreateChildSession: async () => {
    const id = sessionId(`created-${created.length + 1}`)
    created.push(sessionValue(id))
    return okResult(id)
  },
  AbortSession: async () => okResult(undefined),
  FamilyRootOf: () => sessionId('root'),
})

test('WHAT[MANAGED-SESSION-003] HOST_015_companion_satellite_recovery_reuses_journal_linked_child_under_flat_root', async () => {
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

test('WHAT[MANAGED-SESSION-003] HOST_015_companion_satellite_recovery_closes_old_durable_link_before_linking_replacement', async () => {
  const linked = []
  const closed = []
  const created = []
  const runtime = createRuntime(host([], created))

  const result = await ensure(runtime, sessionId('work'), spec({ restored: 'blogger-old', linked, closed }))

  assert.equal(result.tag, 0)
  assert.equal(result.fields[0].SessionId.fields[0], 'created-1')
  assert.equal(result.fields[0].Origin.tag, SatelliteOrigin.Replacement.tag)
  assert.deepEqual(created, ['created-1'])
  assert.deepEqual(closed, ['work'], 'replacement must close the vanished durable association first')
  assert.deepEqual(linked, [['work', 'created-1', COMPANION_AGENT]])
})

test('WHAT[MANAGED-SESSION-011] HOST_015_direct_companion_repoint_trips_process_fatal_on_semantic_cut', async () => {
  const observed = await directCompanionRepointFatal()
  assert.equal(observed.result, 'Error', 'node:test suppresses the physical kill so the typed cut receipt remains assertable')
  assert.ok(observed.recorded.some((line) => line.includes('journal-semantic-cut')), 'semantic cut must trip process fatal')
  assert.ok(observed.recorded.some((line) => line.includes('COMPANION-002')), 'fatal record must preserve the invariant reason')
})

test('WHAT[MANAGED-SESSION-011] HOST_015_companion_replacement_transitions_real_durable_link_without_semantic_cut', async () => {
  const observed = await durableCompanionReplacement()
  assert.equal(observed.ok, true, observed.error ?? '')
  assert.equal(observed.origin, 'Replacement')
  assert.deepEqual(observed.created, ['created-1'])
  assert.equal(observed.bloggerId, 'created-1')
})

test('WHAT[MANAGED-SESSION-003] HOST_015_companion_satellite_recovery_fails_closed_when_journal_linked_child_conflicts', async () => {
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

test('WHAT[MANAGED-SESSION-003] HOST_015_companion_satellite_recovery_never_adopts_same_agent_sibling_without_journal_link', async () => {
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

test('WHAT[MANAGED-SESSION-011] HOST_015_companion_satellite_recovery_replaces_without_adopting_same_agent_sibling', async () => {
  const linked = []
  const closed = []
  const created = []
  // Journal links blogger-old (gone from the Host); blogger-other is another
  // owner's satellite under the same flat root. Replacement must create a new
  // child and leave the sibling untouched.
  const runtime = createRuntime(host([child('blogger-other', 'root')], created))

  const result = await ensure(runtime, sessionId('work'), spec({ restored: 'blogger-old', linked, closed }))

  assert.equal(result.tag, 0)
  assert.equal(result.fields[0].SessionId.fields[0], 'created-1')
  assert.equal(result.fields[0].Origin.tag, SatelliteOrigin.Replacement.tag)
  assert.deepEqual(created, ['created-1'])
  assert.deepEqual(closed, ['work'])
  assert.deepEqual(linked, [['work', 'created-1', COMPANION_AGENT]])
})

test('WHAT[MANAGED-SESSION-011] HOST_014_failed_companion_ensure_invalidates_satellite_flight_before_retry', async () => {
  const observed = await failedCompanionEnsureRetry()
  assert.match(observed.firstError, /temporary host snapshot failure/)
  assert.equal(observed.recoveredId, 'retry-created-1')
  assert.equal(observed.listCalls, 2, 'second ensure must re-observe Host instead of replaying the cached failed flight')
  assert.equal(observed.createCalls, 1)
})

test('WHAT[MANAGED-SESSION-002] HOST_014_concurrent_first_ensure_is_single_flight_and_creates_one_child', async () => {
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
    created.push(sessionValue(id))
    return okResult(id)
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

test('WHAT[MANAGED-SESSION-011] HOST_014_children_query_failure_does_not_guess_or_create', async () => {
  const created = []
  const sessions = host([], created)
  sessions.ListChildren = async () => errorResult('children unavailable')
  const runtime = createRuntime(sessions)

  const result = await ensure(runtime, sessionId('work'), spec({ restored: 'blogger-old' }))

  assert.equal(result.tag, 1)
  assert.match(result.fields[0], /Cannot recover companion satellite: children unavailable/)
  assert.deepEqual(created, [])
})
