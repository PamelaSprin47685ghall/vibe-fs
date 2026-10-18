import assert from 'node:assert/strict'
import { performance } from 'node:perf_hooks'
import { createHash } from 'node:crypto'
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs'
import { join } from 'node:path'

// Keep the original retained journal bytes. Only this child owns the wall clock.
const NativeDate = Date
const started = performance.now()
const anchor = NativeDate.parse('2026-10-18T19:10:00Z')
globalThis.Date = class extends NativeDate {
  constructor(...args) {
    super(...(args.length === 0 ? [anchor + performance.now() - started] : args))
  }
  static now() {
    return Math.floor(anchor + performance.now() - started)
  }
}

const { default: plugin } = await import('wanxiangshu')
const { withExecutablePlugin, observeAuthority } = await import(
  '../../../verification-system/tests/support/plugin-fixture.mjs'
)
const fixtures = {
  mixed: { file: './old667-mixed-journal.ndjson',
    digest: 'a9709c18a6a1a6256f04f0acf384945d002b5904a90dc8f2c4577fc1a8c36ef5',
    session: 'gap159-cold-mixed-predictor-predictor', healthy: ['engineer', 'manager'], roles: ['engineer', 'manager'] },
  'with-cut': { file: './old667-predictor-later-cut.ndjson',
    digest: '7a6ca9b312e0bb1b38546b521dc06390a7716bd56c4adf71903318105a3f0008',
    session: 'supported667-predictor-session', healthy: [], roles: [] },
  'same-event': { file: './old667-same-event-cut.ndjson',
    digest: '51577bb621c510efd797db610b352071bb4aa00ddb8a40a8cb8d3cc8327d94fb',
    session: 'supported667-same-event-session', healthy: [], roles: ['engineer'] },
}
const fixture = fixtures[process.argv[2] ?? 'mixed']
assert.ok(fixture)
const sameEvent = process.argv[2] === 'same-event'
const original = readFileSync(new URL(fixture.file, import.meta.url))
const digest = createHash('sha256').update(original).digest('hex')
assert.equal(digest, fixture.digest)
const session = fixture.session
const healthyAgents = fixture.healthy
let result

await withExecutablePlugin(async (hooks, directory, ids, runtime) => {
  const source = join(directory, '.git/wanxiangshu/events/6501f2cb85a44e099ae21008cb87f83f-0000.ndjson')
  const healthy = healthyAgents.map(agent =>
    observeAuthority(runtime, 'gap159-cold-mixed-predictor-' + agent).activeLogicalRun,
  )
  assert.deepEqual(healthy.map(profile => profile.participantIdentity.role), healthyAgents)
  const before = observeAuthority(runtime, session).activeLogicalRun
  if (sameEvent) assert.equal(before.participantIdentity.role, 'engineer')
  else assert.equal(before, null)
  assert.deepEqual(readFileSync(source), original)

  const output = {
    message: { id: sameEvent ? 'supported667-good-engineer-root' : 'new-engineer-root-over-unknown', sessionID: session, role: 'user', agent: 'engineer',
      model: { providerID: 'host', modelID: 'placeholder' } },
    parts: [{ type: 'text', text: 'Attempt a fresh identity on an unclosed old Predictor run.' }],
  }
  if (sameEvent) await hooks['chat.message']({ sessionID: session, agent: 'engineer' }, output)
  else await assert.rejects(
      hooks['chat.message']({ sessionID: session, agent: 'engineer' }, output),
      /Journal cut requires a decodable failed event/,
    )
  assert.deepEqual(observeAuthority(runtime, session).activeLogicalRun, before)
  assert.deepEqual(healthyAgents.map(agent =>
    observeAuthority(runtime, 'gap159-cold-mixed-predictor-' + agent).activeLogicalRun,
  ), healthy)
  assert.deepEqual(ids, [])
  assert.deepEqual(runtime.prompts, [])
  assert.deepEqual(runtime.abortedIds, [])
  assert.deepEqual(readFileSync(source), original)
  result = { ok: true, originalDigest: digest, healthyRoles: fixture.roles }
}, {}, async input => {
  const events = join(input.directory, '.git/wanxiangshu/events')
  mkdirSync(events, { recursive: true })
  writeFileSync(join(events, '6501f2cb85a44e099ae21008cb87f83f-0000.ndjson'), original)
  return plugin.server(input)
})

process.stdout.write(JSON.stringify(result) + '\n')
