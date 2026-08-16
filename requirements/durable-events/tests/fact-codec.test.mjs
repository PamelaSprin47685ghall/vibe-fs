// PERSIST-005 fact codec laws use JS-native fact descriptors only.

import assert from 'node:assert/strict'
import test from 'node:test'

import * as factCodec from '../../../dist/Persistence/Journal/FactCodecSurface.js'

const runtimeStarted = (startedAt = '2026-01-01T00:00:00Z') => ({
  family: 'Runtime',
  case: 'RuntimeStarted',
  payload: { RuntimeId: 'rt_fact', ProcessId: 42, StartedAt: startedAt },
})

const handleAbandoned = (abandonedAt = '2026-01-01T00:00:00Z') => ({
  family: 'Execution',
  case: 'HandleAbandoned',
  payload: {
    ParentSessionId: 'ses_pin',
    Handle: 'h-pin',
    Reason: 'ParentCancelled',
    AbandonedAt: abandonedAt,
  },
})

const handleCompleted = (overrides = {}) => ({
  family: 'Execution',
  case: 'HandleCompleted',
  payload: {
    ParentSessionId: 'ses_hc',
    Handle: 'h-hc',
    Kind: 'Terminal',
    CompletionRef: null,
    CompletionDigest: null,
    ...overrides,
  },
})

const handleLinked = (overrides = {}) => ({
  family: 'Execution',
  case: 'HandleLinked',
  payload: {
    ParentSessionId: 'ses_hl',
    ChildSessionId: 'ses_hl_child',
    Handle: 'h-hl',
    TargetAgent: 'fast-reviewer',
    Byname: 'Rhea',
    CanonicalRole: 'Reviewer',
    Ownership: 'DurableParentHandle',
    ...overrides,
  },
})

test('WHAT[DURABLE-EVENTS-009] PERSIST_005_modern_json_has_no_legacy_markers', () => {
  assert.equal(factCodec.containsLegacyFallbackFields('{"RuntimeStarted":{"Runtime":"rt"}}'), false)
})

test('WHAT[DURABLE-EVENTS-003] PERSIST_001_runtime_started_pins_offset_on_serialize_and_deserialize', () => {
  const line = factCodec.encode(runtimeStarted())
  assert.match(line, /StartedAt[^Z]*Z|StartedAt.*\+00:00/, `offset must be pinned to UTC: ${line}`)

  const shifted = line.replace('T00:00:00.000+00:00', 'T08:00:00.000+08:00')
  const decoded = factCodec.decode(shifted)
  assert.equal(decoded.ok, true, decoded.ok ? '' : decoded.error)
  assert.equal(decoded.line, line)
})

test('WHAT[DURABLE-EVENTS-003] PERSIST_001_handle_abandoned_pins_abandoned_at_offset', () => {
  const line = factCodec.encode(handleAbandoned('2026-01-01T08:00:00+08:00'))
  const decoded = factCodec.decode(line)
  assert.equal(decoded.ok, true, decoded.ok ? '' : decoded.error)
  assert.equal(decoded.line, line, 'offset must normalise to +00:00')
})

test('WHAT[DURABLE-EVENTS-002] MIGRATION_handle_completed_without_completion_ref_gets_nulls_injected', () => {
  const line = factCodec.encode(handleCompleted())
  const stripped = line
    .replace(/,"CompletionRef":null/g, '')
    .replace(/,"CompletionDigest":null/g, '')
    .replace(/"CompletionRef":null,/g, '')
    .replace(/"CompletionDigest":null,/g, '')
  assert.equal(stripped.includes('CompletionRef'), false)
  assert.equal(stripped.includes('CompletionDigest'), false)

  const decoded = factCodec.decode(stripped)
  assert.equal(decoded.ok, true, decoded.ok ? '' : decoded.error)
  assert.equal(decoded.case, 'HandleCompleted')
})

test('WHAT[DURABLE-EVENTS-002] MIGRATION_handle_completed_with_completion_ref_passes_through', () => {
  const line = factCodec.encode(handleCompleted({
    ParentSessionId: 'ses_hc2',
    Handle: 'h-hc2',
    CompletionRef: 'blobs/ref-1',
    CompletionDigest: 'digest-1',
  }))
  assert.equal(line.includes('CompletionRef'), true)

  const decoded = factCodec.decode(line)
  assert.equal(decoded.ok, true, decoded.ok ? '' : decoded.error)
  assert.equal(decoded.line, line)
})

test('WHAT[DURABLE-EVENTS-002] MIGRATION_handle_linked_without_ownership_defaults_to_durable_parent', () => {
  const line = factCodec.encode(handleLinked())
  assert.equal(line.includes('Ownership'), true)
  const stripped = line.replace(/"Ownership":"DurableParentHandle",?/, '')
  assert.equal(stripped.includes('Ownership'), false)

  const decoded = factCodec.decode(stripped)
  assert.equal(decoded.ok, true, decoded.ok ? '' : decoded.error)
  assert.equal(decoded.line, line)
})

test('WHAT[DURABLE-EVENTS-002] MIGRATION_handle_linked_without_byname_replays_with_machine_name_fallback_marker', () => {
  const line = factCodec.encode(handleLinked({
    ParentSessionId: 'ses_hl_legacy',
    ChildSessionId: 'ses_hl_legacy_child',
    Handle: 'h-hl-legacy',
    TargetAgent: 'fast-coder',
    Byname: 'Ada',
    CanonicalRole: 'Coder',
  }))
  const stripped = line.replace(/"Byname":"Ada",?/, '')
  assert.equal(stripped.includes('Byname'), false)

  const decoded = factCodec.decode(stripped)
  assert.equal(decoded.ok, true, decoded.ok ? '' : decoded.error)
  assert.match(decoded.line, /"Byname":""/)
  assert.match(decoded.line, /"TargetAgent":"fast-coder"/)
})

test('WHAT[DURABLE-EVENTS-007] PERSIST_005_unparseable_json_is_a_decode_error_not_a_throw', () => {
  const decoded = factCodec.decode('{not json')
  assert.equal(decoded.ok, false)
  assert.equal(typeof decoded.error, 'string')
})

test('WHAT[DURABLE-EVENTS-007] PERSIST_005_unknown_case_is_a_decode_error', () => {
  const decoded = factCodec.decode('{"NoSuchFactCase":{"X":1}}')
  assert.equal(decoded.ok, false)
})
