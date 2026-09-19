import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { mkdirSync, mkdtempSync, rmSync } = await import("node:fs");
const { tmpdir } = await import("node:os");
const { join } = await import("node:path");
const { default: test } = await import("node:test");
const eventStore = await import("../../../dist/Persistence/EventStore/Surface.js");
const journal = await import("../../../dist/Persistence/Journal/Surface.js");
const casebook = await import("../../../dist/Repository/Knowledge/Casebook/Surface.js");
const transaction = await import("../../../dist/Repository/Programming/Js/TransactionSurface.js");
const strength = await import("../../../dist/Strength/Surface.js");

const id = (n) => n.toString(16).padStart(40, '0')
const hash = (text) => `proof-hash(${text})`
const structuralEvent = {
  id: id(1),
  stream: 'canonical-integrator/proof',
  type: 'JobRequested',
  parents: [],
  payload: { proof: 'structural' },
  payloadRefs: [],
}
const mustOk = (result, label) => {
  assert.equal(result.ok, true, `${label}: ${JSON.stringify(result.error)}`)
  return result
}

test('WHAT[durable-events-019] every registered business oracle changes its production Current', async () => {
  const root = mkdtempSync(join(tmpdir(), 'wxs-integrator-registration-'))
  const commonDir = join(root, '.git')
  mkdirSync(commonDir, { recursive: true })

  try {
    const store = eventStore.create(commonDir, 'business-registration')

    try {
      mustOk(await eventStore.append(store, [structuralEvent]), 'append Structural fact')

      const payload = mustOk(
        await strength.storeWritePayload(store, new TextEncoder().encode('canonical strength material')),
        'write Strength payload',
      )
      const strengthFact = strength.eventPrepared(
        'canonical-owner',
        'canonical-decision',
        'canonical-target-run',
        'canonical-replica',
        'K1',
        'canonical-anchor',
        'canonical-frame',
        27,
        [payload.value],
      )
      mustOk(await strength.storeAppend(store, hash, strengthFact), 'append Strength fact')

      mustOk(
        await casebook.archive(store, {
          sessionId: 'canonical-case',
          q: 'Q',
          a: 'A',
          observations: [],
          lastAccessOrder: 0,
        }),
        'append Casebook fact',
      )

      mustOk(
        await transaction.appendPrepared(store, {
          transactionId: 'canonical-transaction',
          workspaceRoot: '/canonical-workspace',
          mutations: [{ path: 'a.txt', originalText: 'before', newText: 'after' }],
        }),
        'append JsTransaction fact',
      )

      const fetched = mustOk(await casebook.fetchCase(store, 10, 'canonical-case'), 'read Casebook Current')
      const strengthCurrent = strength.storeCurrent(store)

      assert.deepEqual(
        {
          structuralHead: eventStore.head(store, structuralEvent.stream),
          structuralEvent: eventStore.read(store, structuralEvent.id)?.payload?.proof ?? null,
          strengthDecision: strength.projectionDecisionForTarget('canonical-target-run', strengthCurrent),
          caseAnswer: fetched.value?.a ?? null,
          pendingTransactions: transaction.pending(store).map(({ transactionId }) => transactionId).sort(),
        },
        {
          structuralHead: structuralEvent.id,
          structuralEvent: 'structural',
          strengthDecision: 'canonical-decision',
          caseAnswer: 'A',
          pendingTransactions: ['canonical-transaction'],
        },
      )
    } finally {
      eventStore.dispose(store)
    }

    const booted = mustOk(
      await journal.JournalSurface_bootWithWriterId(
        commonDir,
        'journal-registration',
        'runtime-journal-registration',
        4242,
        '9999-01-01T00:00:00Z',
      ),
      'boot Journal',
    )

    try {
      const payload = mustOk(
        await journal.JournalSurface_writePayload(booted.journal, 'canonical opening text'),
        'write Journal payload',
      )
      mustOk(
        await journal.JournalSurface_appendManagerLifecycle(
          booted.journal,
          { kind: 'Session', session: 'canonical-session' },
          {
            case: 'LifeOpened',
            payload: {
              SessionId: 'canonical-session',
              LifeId: 'canonical-life',
              OpeningUserMessageId: 'canonical-message',
              OpeningTextRef: payload.blobRef,
              OpeningTextDigest: payload.blobDigest,
              OpeningCursorSequence: 1,
            },
          },
        ),
        'append Journal fact',
      )
      mustOk(
        await journal.JournalSurface_appendAgent(
          booted.journal,
          { kind: 'Session', session: 'canonical-session' },
          null,
          {
            family: 'Companion',
            case: 'CompanionBloggerClosed',
            payload: {
              SessionId: 'canonical-session',
            },
          },
        ),
        'append Journal fact',
      )
      assert.equal(journal.JournalSurface_hasSession(booted.journal, 'canonical-session'), true)
    } finally {
      journal.JournalSurface_dispose(booted.journal)
    }
  } finally {
    rmSync(root, { recursive: true, force: true })
  }
})
}

{
const { default: assert } = await import("node:assert/strict");
const { readFileSync } = await import("node:fs");
const { default: test } = await import("node:test");
const { CANONICAL_EVENT_READER_OWNER_PATHS, DUAL_WRITE_ALLOWLIST, GIT_BYPASS_ALLOWLIST, NON_STORE_SCHEMA_VERSION_SITES, PHYSICAL_HISTORY_OBSERVER_PATHS, SCANNER_IDS, collectProductionEntries, scanCanonicalSharedProgram, scanDualWrite, scanFeatureHistoryLoop, scanFeatureRef, scanFiles, scanGitBypass, scanPrivateDurableSubstrate, scanSchemaVersionInStoreContext, scanText } = await import("../../../scripts/checks/unified-store-gate.mjs");

const readFixture = (name) =>
  readFileSync(new URL(`./fixtures/${name}`, import.meta.url), 'utf8')

test('WHAT[durable-events-019] feature history loops report exact path line and token', () => {
  const fixtures = [
    ['ProcessEventLog.readStreams commonDir', 'ProcessEventLog.readStreams'],
    ['let loadEvents raw = raw', 'loadEvents'],
    ['let scanHistory stream = stream', 'scanHistory'],
    ['let readHistory stream = stream', 'readHistory'],
    ['let foldHistory state events = List.fold apply state events', 'foldHistory'],
    ['let replayEvents events = List.fold alternate empty events', 'replayEvents'],
    ['let event = (store: IEventStore).TryEvent eventId', 'TryEvent'],
    ['let heads = (store: IEventStore).TryHeads streamId', 'TryHeads'],
    ['let heads = (store: IEventStore).AllHeads()', 'AllHeads'],
    ['let event = EventStore.Surface.read(handle, eventId)', 'read'],
    ['let heads = Surface.heads(handle, streamId)', 'heads'],
  ]

  for (const [source, token] of fixtures) {
    const file = 'src/Wanxiangshu/Repository/Feature/History.fs'
    const hits = scanFeatureHistoryLoop(`module Feature\n${source}`, file)
    const hit = hits.find((candidate) => candidate.token === token)
    assert.deepEqual(
      hit && { file: hit.file, line: hit.line, token: hit.token },
      { file, line: 2, token },
    )
  }

  const manualMerge = [
    'module FeatureHistory',
    'let merge streams =',
    '    streams',
    '    |> List.collect snd',
    '    |> List.sortBy (fun envelope -> envelope.EventId)',
    '    |> List.fold apply empty',
  ].join('\n')
  const manualHit = scanFeatureHistoryLoop(
    manualMerge,
    'src/Wanxiangshu/Repository/Feature/ManualMerge.fs',
  ).find((hit) => hit.token === 'manual merge')
  assert.deepEqual(
    manualHit && { line: manualHit.line, token: manualHit.token },
    { line: 5, token: 'manual merge' },
  )
})
test('WHAT[durable-events-019] feature-local NDJSON SQLite and private stores are forbidden', () => {
  const file = 'src/Wanxiangshu/Repository/Feature/PrivateStore.fs'
  const source = [
    'module FeatureStorage',
    'let journal = "feature-history.ndjson"',
    'let connection = SQLite.open "feature.sqlite"',
    'let store = PrivateEventStore(connection)',
  ].join('\n')
  const hits = scanPrivateDurableSubstrate(source, file)
  assert.deepEqual(
    hits.map(({ file, line, token }) => ({ file, line, token })),
    [
      { file, line: 2, token: '.ndjson' },
      { file, line: 3, token: 'SQLite' },
      { file, line: 4, token: 'PrivateEventStore' },
    ],
  )
})
test('WHAT[durable-events-019] durable file database and custom-store writer capabilities are owner-bound', () => {
  const file = 'src/Wanxiangshu/Repository/Feature/CustomHistory.fs'
  const fixtures = [
    ['System.IO.File.AppendAllText("feature-history.log", payload)', 'System.IO.File.AppendAllText'],
    ['File.WriteAllText("feature-history.log", payload)', 'File.WriteAllText'],
    ['use writer = StreamWriter(path)', 'StreamWriter('],
    ['let connection: DbConnection = openDatabase ()', 'DbConnection'],
    ['let store = CustomStore(path)', 'CustomStore'],
  ]

  for (const [source, token] of fixtures) {
    const hits = scanPrivateDurableSubstrate(source, file)
    assert.ok(hits.some((hit) => hit.token === token), `${token} must be RED outside owners`)
  }

  const owner = 'src/Wanxiangshu/Persistence/EventStore/RetentionSurface.fs'
  assert.equal(
    scanPrivateDurableSubstrate(fixtures.map(([source]) => source).join('\n'), owner).length,
    0,
    'the exact physical owner may use durable writer capabilities',
  )
})
test('WHAT[durable-events-019] canonical integrator and exact physical or proof readers are allowed', () => {
  const reader = [
    'let streams = ProcessEventLog.readStreams commonDir',
    'let event = (store: IEventStore).TryEvent eventId',
    'let streamHeads = (store: IEventStore).TryHeads streamId',
    'let allHeads = (store: IEventStore).AllHeads()',
    'let exposed = EventStore.Surface.read(handle, eventId)',
    'let exposedHeads = EventStore.Surface.heads(handle, streamId)',
  ].join('\n')
  assert.equal(
    scanFeatureHistoryLoop(
      reader,
      'src/Wanxiangshu/Persistence/EventStore/IntegratorEngine.fs',
    ).length,
    0,
  )

  assert.deepEqual([...PHYSICAL_HISTORY_OBSERVER_PATHS], [
    'src/Wanxiangshu/Persistence/EventStore/ProcessEventLog.fs',
    'src/Wanxiangshu/Persistence/EventStore/EventKWayMerge.fs',
    'src/Wanxiangshu/Persistence/EventStore/WriterStreamSync.fs',
    'src/Wanxiangshu/Persistence/EventStore/RetentionSurface.fs',
    'src/Wanxiangshu/Persistence/EventStore/MergeSurface.fs',
    'src/Wanxiangshu/Verification/TemporalSurface.fs',
    'src/Wanxiangshu/Verification/JournalPortObservationSurface.fs',
  ])
  for (const file of PHYSICAL_HISTORY_OBSERVER_PATHS) {
    assert.equal(scanFeatureHistoryLoop(reader, file).length, 0, file)
  }

  assert.deepEqual([...CANONICAL_EVENT_READER_OWNER_PATHS], [
    'src/Wanxiangshu/Persistence/EventStore/Store.fs',
    'src/Wanxiangshu/Persistence/EventStore/Surface.fs',
    'src/Wanxiangshu/OpenCode/Host/WorkspaceEventStore.fs',
    'src/Wanxiangshu/Persistence/Journal/EventStoreJournalWriter.fs',
    'src/Wanxiangshu/Verification/EventStoreWriterSurface.fs',
    'src/Wanxiangshu/Verification/JournalPortObservationSurface.fs',
  ])
  const eventStoreReaders = reader.split('\n').slice(1).join('\n')
  for (const file of CANONICAL_EVENT_READER_OWNER_PATHS) {
    assert.equal(scanFeatureHistoryLoop(eventStoreReaders, file).length, 0, file)
  }

  const substrate = 'let physicalLine = "writer.ndjson"'
  const substrateOwners = [
    'src/Wanxiangshu/Persistence/EventStore/RetentionSurface.fs',
    'src/Wanxiangshu/OpenCode/Host/WorkspaceEventStore.fs',
    'src/Wanxiangshu/Persistence/Journal/EventStoreJournalWriter.fs',
    'src/Wanxiangshu/Verification/EventStoreWriterSurface.fs',
  ]
  for (const file of substrateOwners) {
    assert.equal(scanPrivateDurableSubstrate(substrate, file).length, 0, file)
  }
  assert.ok(
    scanPrivateDurableSubstrate(
      substrate,
      'src/Wanxiangshu/Repository/Knowledge/Casebook/Surface.fs',
    ).length > 0,
    'the same substrate token remains forbidden outside exact physical/proof owners',
  )

  const physicalMerge = [
    'let merge streams =',
    '    streams',
    '    |> List.collect snd',
    '    |> List.sortBy eventKey',
  ].join('\n')
  assert.equal(
    scanFeatureHistoryLoop(
      physicalMerge,
      'src/Wanxiangshu/Persistence/EventStore/EventKWayMerge.fs',
    ).length,
    0,
  )
  assert.ok(
    scanFeatureHistoryLoop(
      physicalMerge,
      'src/Wanxiangshu/Repository/Feature/EventKWayMerge.fs',
    ).some((hit) => hit.token === 'manual merge'),
    'the exact physical merge path does not grant similarly named feature files ownership',
  )

  assert.ok(
    scanFeatureHistoryLoop(
      reader,
      'src/Wanxiangshu/Verification/AnotherProbe.fs',
    ).length > 0,
    'verification observation is granted to exact probes, not the whole directory',
  )
})
test('WHAT[durable-events-019] production tree has no feature history loop or private substrate', () => {
  const entries = collectProductionEntries()
  const violations = scanFiles(entries).filter(
    (v) => v.id === 'feature-history-loop' || v.id === 'private-durable-substrate',
  )
  assert.deepEqual(
    violations,
    [],
    violations.map((v) => `[${v.id}] ${v.file}:${v.line} ${v.token} ${v.label}`).join('\n'),
  )
})
}
