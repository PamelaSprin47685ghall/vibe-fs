import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { execFileSync } = await import("node:child_process");
const { existsSync, mkdirSync, mkdtempSync, rmSync, writeFileSync } = await import("node:fs");
const { tmpdir } = await import("node:os");
const { join } = await import("node:path");
const { default: test } = await import("node:test");
const journal = await import("../../../dist/Persistence/Journal/Surface.js");

const CLOSED = {
  family: 'Companion',
  case: 'CompanionBloggerClosed',
  payload: { SessionId: 'ses_es_boot' },
}
const mustOk = (result, label) => {
  assert.equal(result.ok, true, `${label}: ${JSON.stringify(result.error)}`)
  return result
}
const withRepo = (fn) => {
  const repo = mkdtempSync(join(tmpdir(), 'wxs-journal-boot-'))
  execFileSync('git', ['init', '--quiet', repo])
  const commonDir = join(repo, '.git')
  return fn(commonDir)
    .finally(() => rmSync(repo, { recursive: true, force: true }))
}

test('WHAT[DURABLE-EVENTS-020] empty_boot_is_read_only_and_keeps_RuntimeStarted_in_memory_until_activation', async () => {
  await withRepo(async (commonDir) => {
    const booted = mustOk(await journal.JournalSurface_bootWithWriterId(commonDir, 'boot-empty', 'rt_empty', 6001, '2026-05-01T00:00:00Z'), 'empty boot')
    assert.equal(Number(booted.localSeq), 1)
    assert.equal(existsSync(join(commonDir, 'wanxiang', 'events', 'boot-empty.ndjson')), false)
    journal.JournalSurface_dispose(booted.journal)
  })
})
test('WHAT[DURABLE-EVENTS-020] plugin host does not pre-scan canonical history before EventStore activation', async () => {
  const { readFile } = await import('node:fs/promises')
  const pluginHost = await readFile(new URL('../../../src/Wanxiangshu/OpenCode/Host/PluginHost.fs', import.meta.url), 'utf8')

  assert.doesNotMatch(pluginHost, /ProcessEventLog\.readStreams/,
    'PluginHost must not add a second full history scan before CanonicalIntegrator owns replay')
})
test('WHAT[DURABLE-EVENTS-020] plugin load defers EventStore replay and durable-session seeding until activation', async () => {
  const { readFile } = await import('node:fs/promises')
  const workspaceStore = await readFile(new URL('../../../src/Wanxiangshu/OpenCode/Host/WorkspaceEventStore.fs', import.meta.url), 'utf8')
  const writer = await readFile(new URL('../../../src/Wanxiangshu/Persistence/Journal/EventStoreJournalWriter.fs', import.meta.url), 'utf8')
  const sessionWiring = await readFile(new URL('../../../src/Wanxiangshu/OpenCode/Plugin/PluginSessionWiring.fs', import.meta.url), 'utf8')
  const signals = await readFile(new URL('../../../src/Wanxiangshu/OpenCode/Host/HostSignalBootstrap.fs', import.meta.url), 'utf8')

  assert.match(workspaceStore, /lazy\s*\(/, 'workspace store must defer CanonicalIntegrator replay')
  assert.doesNotMatch(writer, /resumeOrCreate[\s\S]{0,1600}currentJournalProjection/,
    'resumeOrCreate must not force Current during plugin load')
  assert.match(sessionWiring, /AttachDurabilityActivation/,
    'durable projection seeding must attach to the activation boundary')
  assert.match(signals, /ActivateDurability\(\)/,
    'the first durable Host admission must activate deferred projection state')
})
test('WHAT[DURABLE-EVENTS-020] parsed Journal payload replay does not stringify every envelope for legacy detection', async () => {
  const { readFile } = await import('node:fs/promises')
  const envelope = await readFile(new URL('../../../src/Wanxiangshu/Persistence/Journal/Envelope.fs', import.meta.url), 'utf8')
  const factCodec = await readFile(new URL('../../../src/Wanxiangshu/Persistence/Journal/FactCodec.fs', import.meta.url), 'utf8')
  const integrator = await readFile(new URL('../../../src/Wanxiangshu/Composition/Durable/JournalIntegration.fs', import.meta.url), 'utf8')

  assert.doesNotMatch(
    envelope,
    /let\s+deserializeValue[\s\S]{0,700}JS\.JSON\.stringify\s+value/,
    'activation already owns a parsed payload; legacy detection must not serialize it again',
  )
  assert.doesNotMatch(envelope, /legacyDecodeErrorForValue|isIgnoredLegacyValue|isIgnoredLegacyDecodeError/,
    'Envelope decode must not perform legacy work on the hot path')
  assert.match(factCodec, /let\s+isIgnoredLegacyDecodeError/)
  assert.doesNotMatch(factCodec, /legacyDecodeErrorCode|const stack = \[root\]/,
    'activation must not scan parsed Journal payloads for legacy markers')
  assert.match(integrator, /Error error when FactCodec\.isIgnoredLegacyDecodeError error -> Ok current/,
    'legacy classification runs only after decode failure, never for modern events')
})
test('WHAT[DURABLE-EVENTS-020] replay ignores a legacy Journal fact instead of cutting the stream', async () => {
  await withRepo(async (commonDir) => {
    const eventId = 'e'.repeat(40)
    const legacy = {
      event_id: eventId,
      event_type: 'JournalEnvelope',
      parents: [],
      payload: {
        EventId: ['EventId', eventId],
        Fact: ['Agent', ['Fallback', ['PluginPromptAccepted', {}]]],
        LocalSeq: ['LocalSeq', '1'],
        ObservedAt: '2026-01-01T00:00:00.000+00:00',
        RuntimeId: ['RuntimeId', 'rt_legacy'],
        Stream: 'Workspace',
      },
      payload_refs: [],
      stream_id: 'journal/workspace',
    }
    const eventsDir = join(commonDir, 'wanxiang', 'events')
    mkdirSync(eventsDir, { recursive: true })
    writeFileSync(join(eventsDir, 'legacy-writer.ndjson'), `${JSON.stringify(legacy)}\n`)

    const booted = mustOk(
      await journal.JournalSurface_bootWithWriterId(commonDir, 'modern-writer', 'rt_modern', 7001, '2026-05-02T00:00:00Z'),
      'boot with ignored legacy fact',
    )
    journal.JournalSurface_dispose(booted.journal)
  })
})
test('WHAT[DURABLE-EVENTS-020] Journal replay precompiles outer fact-family dispatch instead of reflecting nested unions per event', async () => {
  const { readFile } = await import('node:fs/promises')
  const envelope = await readFile(new URL('../../../src/Wanxiangshu/Persistence/Journal/Envelope.fs', import.meta.url), 'utf8')

  assert.match(envelope, /let\s+private\s+agentFactDecoder/)
  assert.match(envelope, /"Prompt"[\s\S]{0,200}AgentFact\.Prompt/)
  assert.match(envelope, /"Host"[\s\S]{0,200}AgentFact\.Host/)
  assert.match(envelope, /Extra\.withCustom[\s\S]{0,120}factDecoder/)
  assert.match(envelope, /generateDecoderCached<Envelope>\s*\(\s*extra\s*=\s*decodeExtra\s*\)/)
  assert.doesNotMatch(envelope, /generateDecoderCached<Envelope>\s*\(\s*extra\s*=\s*extra\s*\)/,
    'Envelope Auto decoding may preserve the wire representation, but Fact must use the precompiled custom decoder')
})
test('WHAT[DURABLE-EVENTS-020] dominant XTracePartAppended history case has a direct decoder with compatibility fallback', async () => {
  const { readFile } = await import('node:fs/promises')
  const envelope = await readFile(new URL('../../../src/Wanxiangshu/Persistence/Journal/Envelope.fs', import.meta.url), 'utf8')

  assert.match(envelope, /let\s+private\s+xTracePartAppendedDecoder/)
  assert.match(envelope, /let\s+private\s+xTracePartAppendedDecoder[\s\S]{0,1400}CompanionFactCases\.XTracePartAppended/)
  assert.match(envelope, /"XTracePartAppended"\s*->\s*xTracePartAppendedDecoder/)
  assert.match(envelope, /genericCompanionFactDecoder/,
    'non-dominant Companion cases must keep the existing generic decoder as the compatibility fallback')
})
}

{
const { default: assert } = await import("node:assert/strict");
const { execFileSync } = await import("node:child_process");
const { existsSync, mkdtempSync, readFileSync, rmSync } = await import("node:fs");
const { tmpdir } = await import("node:os");
const { join } = await import("node:path");
const { default: test } = await import("node:test");
const journal = await import("../../../dist/Persistence/Journal/Surface.js");

const CLOSED_AGENT = {
  family: 'Companion',
  case: 'CompanionBloggerClosed',
  payload: { SessionId: 'ses_es_writer' },
}
const mustOk = (result, label) => {
  assert.equal(result.ok, true, `${label}: ${JSON.stringify(result.error)}`)
  return result
}
const withRepo = (writerId, fn) => {
  const repo = mkdtempSync(join(tmpdir(), 'wxs-journal-'))
  execFileSync('git', ['init', '--quiet', repo])
  const commonDir = join(repo, '.git')
  return fn(commonDir)
    .finally(() => rmSync(repo, { recursive: true, force: true }))
}

test('WHAT[DURABLE-EVENTS-020] create_is_read_only_until_the_first_business_append', async () => {
  await withRepo('journal-writer-proof', async (commonDir) => {
    const booted = mustOk(await journal.JournalSurface_boot(commonDir, 'rt_es', 4242, '2026-04-01T00:00:00Z'), 'boot')
    const file = join(commonDir, 'wanxiang', 'events', 'journal-writer-proof.ndjson')
    assert.equal(existsSync(file), false)
    journal.JournalSurface_dispose(booted.journal)
  })
})
}
