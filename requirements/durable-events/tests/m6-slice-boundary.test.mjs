import assert from 'node:assert/strict'
import test from 'node:test'
import { existsSync, readdirSync, readFileSync } from 'node:fs'
import { join, resolve } from 'node:path'
import * as eventCodec from '../../../dist/Persistence/EventStore/CodecSurface.js'
import { readCompileShardInventory } from '../../../scripts/lib/compile-shards.mjs'
import { buildSubsystemInventory } from '../../../scripts/checks/subsystems.mjs'
import { assertEffectIsInjected, assertFatalBoundary, assertPureContract } from '../../structured-workflow/tests/support/m6-boundary-proof.mjs'

const ROOT = resolve(import.meta.dirname, '../../..')
const SOURCE_ROOT = join(ROOT, 'src/Wanxiangshu')

const event = ({
  id = '1111111111111111111111111111111111111111',
  payload = { state: 'open' },
} = {}) => ({
  id,
  stream: 'proof/canonical-codec-slice',
  type: 'JobRequested',
  parents: [],
  payload,
  payloadRefs: [],
})

test('WHAT[DURABLE-EVENTS-023] canonical codec surface keeps encode decode UTF-8 identity and merge in one fail-closed protocol', () => {
  const left = event()
  const same = event()
  const collision = event({ payload: { state: 'closed' } })
  const distinct = event({ id: '2222222222222222222222222222222222222222' })
  const canonical = eventCodec.encode(left)

  assert.deepEqual(eventCodec.decode(canonical), { ok: true, event: left })
  assert.deepEqual(eventCodec.decodeUtf8Text(Buffer.from(canonical)), { ok: true, text: canonical })
  assert.deepEqual(eventCodec.decodeUtf8(Buffer.from(canonical)), { ok: true, event: left })

  assert.deepEqual(eventCodec.checkIdentity(left, same), { ok: true })
  assert.deepEqual(eventCodec.checkIdentity(left, collision), {
    ok: false,
    error: { code: 'IdentityCollision', eventId: left.id },
  })
  assert.deepEqual(eventCodec.checkIdentity(left, distinct), { ok: true })

  assert.deepEqual(eventCodec.mergeByIdentity([left, same, distinct]), {
    ok: true,
    events: [left, distinct],
  })
  assert.deepEqual(eventCodec.mergeByIdentity([left, collision, distinct]), {
    ok: false,
    error: { code: 'IdentityCollision', eventId: left.id },
  })

  const nonCanonical = canonical.replace('"event_id"', '"stream_id"').replace(/,"stream_id":"[^"]+"/, `,"event_id":"${left.id}"`)
  assert.deepEqual(eventCodec.decode(nonCanonical), {
    ok: false,
    error: { code: 'NonCanonical', reason: 'event bytes are not §5.0 canonical' },
  })
  const invalidUtf8 = Buffer.from([0xc3, 0x20])
  const invalidUtf8Error = {
    ok: false,
    error: { code: 'NonCanonical', reason: 'event bytes are not valid UTF-8' },
  }
  assert.deepEqual(eventCodec.decodeUtf8Text(invalidUtf8), invalidUtf8Error)
  assert.deepEqual(eventCodec.decodeUtf8(invalidUtf8), invalidUtf8Error)
})

test('WHAT[DURABLE-EVENTS-023] canonical codec and owner folds reject physical store and outer-union authority', () => {
  assertPureContract()
  assertEffectIsInjected('file-system')
})

test('WHAT[DURABLE-EVENTS-024] semantic cut fatal requires settlement and one injected physical fuse', () => {
  assertFatalBoundary('durable-events')
})

test('WHAT[DURABLE-EVENTS-023] single-field family folds own their slice and declare no aggregate dependency', () => {
  const shardInventory = readCompileShardInventory({ repositoryRoot: ROOT })
  const subsystemInventory = buildSubsystemInventory({ compileInventory: shardInventory })
  assert.ok(subsystemInventory.ok, subsystemInventory.violations.join('\n'))
  const projects = [...subsystemInventory.projects.values()]

  // These four families only ever wrote their own top-level field, so their
  // `AgentProjectionSet -> ... -> AgentProjectionSet` wrappers are gone and the
  // slice write lives in composition (`ProjectionUpdate.apply*`).
  for (const source of [
    'Execution/Fission/Fold.fs',
    'Interaction/Concern/Fold.fs',
    'Interaction/Attention/Fold.fs',
    'Enforcer/InstitutionalLearning/Fold.fs',
  ])
    assert.equal(
      existsSync(join(SOURCE_ROOT, source)),
      false,
      `${source} must not come back as an aggregate-typed wrapper`,
    )

  // The Change family keeps the fold but reads its own slice: the shard declares
  // no reference to the aggregate projection and the fold names neither the
  // aggregate nor the durable fail-closed report.
  const changeFold = projects.filter((project) => project.shard === 'change-fold')
  assert.equal(changeFold.length, 1, 'change-fold must resolve to exactly one compile shard')
  assert.ok(
    !changeFold[0].references.some((reference) => reference.endsWith('composition-durable-projection.fsproj')),
    'change-fold must not declare composition-durable-projection',
  )
  const changeFoldSources = ['Change/Fold.fs', 'Change/Fold.fsi']
    .map((source) => readFileSync(join(SOURCE_ROOT, source), 'utf8'))
    .join('\n')
  assert.doesNotMatch(changeFoldSources, /\bAgentProjectionSet\b|\bFoldRejection\b/)
})

test('WHAT[DURABLE-EVENTS-023] prompt provider and companion folds decide on their own slices while composition owns the aggregate write', () => {
  // These three families span more than one slice, so the fold stays and returns a
  // change list over the slices it owns; the bridge writes it back. The Context
  // (Blogger) fold still writes six slices and is not part of this boundary yet.
  for (const source of [
    'Interaction/Authority/Fold.fs',
    'Participant/Provider/Attempt/Fallback/ProviderFailureFactFold.fs',
    'Context/Companion/CompanionFactFold.fs',
  ]) {
    const text = readFileSync(join(SOURCE_ROOT, source), 'utf8')
    assert.doesNotMatch(
      text,
      /\bAgentProjection(?:Set|s)?\b|\bAgentProjection\.|\bFoldRejection\b|\bProjectionUpdate\b|\bComposition\.Durable\b/,
      `${source} is a domain fold and must not name the aggregate projection, the write algebra, or the spine's rejection`,
    )
  }

  // The session-scoped write helpers are composition's; a domain fold that calls
  // them again would re-invert the dependency this boundary exists to prevent.
  const writeHelper = /ProjectionUpdate\.(?:updateSession|updateAuthority|updateCompanion)\b/
  for (const file of collectSourceFiles(SOURCE_ROOT)) {
    const relative = file.slice(SOURCE_ROOT.length + 1)
    if (relative.startsWith('Composition/')) continue
    assert.doesNotMatch(
      readFileSync(file, 'utf8'),
      writeHelper,
      `${relative} is outside composition and must not write the aggregate through ProjectionUpdate`,
    )
  }
})

function collectSourceFiles(directory) {
  const found = []
  for (const entry of readdirSync(directory, { withFileTypes: true })) {
    const path = join(directory, entry.name)
    if (entry.isDirectory()) found.push(...collectSourceFiles(path))
    else if (/\.[fs]i?$/.test(entry.name) || entry.name.endsWith('.fs')) found.push(path)
  }
  return found
}
