// tests/unit/support/domain/persist.mjs — persistence adapters.
// EventStore-backed journal harness (journalStore / agentJournal / bootSnapshot).
// EventStore/GitRawStore/EventStoreJournalWriter loading is confined here.

import { existsSync, mkdirSync, mkdtempSync, readdirSync, rmSync, statSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'

import {
  EnvelopeModule,
  AgentJournalModule,
  bind,
  prod,
  resultOf,
  listItems,
  toList,
  mapOf,
  caseOf,
  payloadOf,
  utcOffset,
} from './interop.mjs'
import { runtimeId, providerRun, idValue } from './identity.mjs'

// ── EventStore journal harness (Phase 5; replaces NDJSON JournalWriter/Boot) ─
//
// Production durability is `GitRawStore` + `EventStore` + `EventStoreJournalWriter`.
// Tests keep the historical helper names (`journalStore`, `agentJournal.create`,
// `createFromBoot`, `bootSnapshot`) so call sites stay small; the backing store
// is in-memory and keyed by the caller's `directory` string for restart tests.

const [GitRawStoreMod, EventStoreMod, EsWriterHarness] = await Promise.all([
  prod('Infrastructure/Persist/GitRawStore'),
  prod('Infrastructure/Persist/EventStore'),
  prod('Journal/EventStoreJournalWriter'),
])

const resolveEsExport = (mod, prefixes) => {
  for (const prefix of prefixes) {
    const hit = Object.entries(mod).find(([name]) => name.startsWith(prefix))
    if (hit) return hit[1]
  }
  return undefined
}

const esWriterCreate =
  EsWriterHarness.EventStoreJournalWriter_create_Z10F3E7A9 ??
  resolveEsExport(EsWriterHarness, ['EventStoreJournalWriter_create'])
const esWriterResume =
  EsWriterHarness.EventStoreJournalWriter_resumeOrCreate_Z474F346C ??
  EsWriterHarness.EventStoreJournalWriter_resumeOrCreate_Z10F3E7A9 ??
  resolveEsExport(EsWriterHarness, ['EventStoreJournalWriter_resumeOrCreate'])
const esLoadJournalEnvelopes = EsWriterHarness.EventStoreJournalWriter_loadJournalEnvelopes
const esAppend = EsWriterHarness.EventStoreJournalWriter__Append
const esGetFilePath = EsWriterHarness.EventStoreJournalWriter__get_FilePath
const esGetLocalSeq = EsWriterHarness.EventStoreJournalWriter__get_LocalSeq
const esGetLastCommitted = EsWriterHarness.EventStoreJournalWriter__get_LastCommittedLocalSeq
const esGetPoisoned = EsWriterHarness.EventStoreJournalWriter__get_IsPoisoned
const esGetStoreSnapshot = EsWriterHarness.EventStoreJournalWriter__get_StoreSnapshot
const esGetBlobWriter = EsWriterHarness.EventStoreJournalWriter__get_BlobWriter

if (typeof esWriterCreate !== 'function') throw new Error('EventStoreJournalWriter.create missing from dist')
if (typeof esWriterResume !== 'function') throw new Error('EventStoreJournalWriter.resumeOrCreate missing from dist')
if (typeof esLoadJournalEnvelopes !== 'function') {
  throw new Error('EventStoreJournalWriter.loadJournalEnvelopes missing from dist')
}

/** directory → { raw, store } so createFromBoot can resume after dispose. */
const eventStoreRegistry = new Map()

const freshEventStorePair = () => {
  const raw = GitRawStoreMod.GitRawStore_createInMemory()
  const store = EventStoreMod.EventStore_create(raw)
  return { raw, store }
}

const registerEventStore = (directory, pair) => {
  if (typeof directory === 'string' && directory.length > 0) {
    eventStoreRegistry.set(directory, pair)
  }
  return pair
}

const eventStoreFor = (directory, { createIfMissing = false } = {}) => {
  if (typeof directory === 'string' && directory.length > 0) {
    const existing = eventStoreRegistry.get(directory)
    if (existing) return existing
    if (!createIfMissing) {
      throw new Error(`no EventStore registered for directory ${directory} — call agentJournal.create first`)
    }
  }
  return registerEventStore(directory, freshEventStorePair())
}

const writerHandle = (writer) => ({
  path: typeof esGetFilePath === 'function' ? esGetFilePath(writer) : writer.FilePath,

  /** PERSIST-002: `{ committed: true, envelope }` or `{ committed: false, ... }`. */
  append: (streamId, envelopeFact, run) => {
    const result = esAppend(writer, streamId, run === undefined ? undefined : providerRun(run), envelopeFact)
    return caseOf(result) === 'Committed'
      ? { committed: true, envelope: payloadOf(result) }
      : {
          committed: false,
          eventId: idValue.event(result.fields[0]),
          failure: caseOf(result.fields[1]),
          reason: result.fields[1]?.fields?.[0],
        }
  },

  seq: () => Number(esGetLocalSeq(writer)),
  lastCommittedSeq: () => Number(esGetLastCommitted(writer)),
  poisoned: () => esGetPoisoned(writer),
  dispose: () => writer.Dispose(),
  writer,
})

/**
 * Disposable EventStore-backed journal harness (replaces NDJSON `journalStore`).
 *
 * `open()` publishes RuntimeStarted via EventStoreJournalWriter.create and
 * returns the same append/seq surface tests already use.
 */
export const journalStore = () => {
  const base = mkdtempSync(join(tmpdir(), 'wxs-journal-'))
  const directory = join(base, 'runtimes')
  mkdirSync(directory, { recursive: true, mode: 0o700 })
  const pair = registerEventStore(directory, freshEventStorePair())
  const opened = []

  return {
    directory,
    raw: pair.raw,
    store: pair.store,

    open: ({ runtime = 'rt_1', pid = 4242, startedAt = '2026-01-01T00:00:00Z' } = {}) => {
      const [writer, initEnvelope] = esWriterCreate(
        runtimeId(runtime),
        pid,
        utcOffset(startedAt),
        pair.store,
        pair.raw,
      )
      opened.push(writer)
      return { ...writerHandle(writer), initEnvelope }
    },

    /** EventStore has no NDJSON file modes; retain shape for any leftover callers. */
    modes: () => ({
      directory: (statSync(directory).mode & 0o777).toString(8),
      file: '600',
    }),

    /** Serialized journal envelopes at the store tip (not NDJSON lines). */
    lines: () => {
      const loaded = resultOf(esLoadJournalEnvelopes(pair.raw, pair.store.OpenSnapshot()))
      if (!loaded.ok) throw new Error(`journalStore.lines load failed: ${loaded.error}`)
      return listItems(loaded.value).map((env) => EnvelopeModule.EnvelopeModule_serialize(env))
    },

    /** Corrupt-journal injection is NDJSON-only; EventStore tests seed via Append. */
    writeRaw: () => {
      throw new Error('journalStore.writeRaw removed with NDJSON Boot substrate')
    },

    files: () => (existsSync(directory) ? readdirSync(directory).sort() : []),

    /** `{ envelopes, diagnostics, frontier }` from the EventStore tip. */
    boot: () => {
      const loaded = resultOf(esLoadJournalEnvelopes(pair.raw, pair.store.OpenSnapshot()))
      if (!loaded.ok) {
        return { envelopes: [], diagnostics: [String(loaded.error)], frontier: {} }
      }
      return { envelopes: listItems(loaded.value), diagnostics: [], frontier: {} }
    },

    frontier: () => ({}),

    close: () => {
      for (const writer of opened) {
        try {
          writer.Dispose()
        } catch {
          // Already disposed by the test; closing twice is not a failure.
        }
      }
      eventStoreRegistry.delete(directory)
      rmSync(base, { recursive: true, force: true })
    },
  }
}

const AgentJournalCreate = bind(AgentJournalModule, 'AgentJournal', [
  'createFromEventStore',
  'createFromProjection',
  'appendAgent',
  'appendMagicTodo',
  'appendManagerLifecycle',
  'snapshot',
  'revision',
  'snapshotWithRevision',
  'awaitChangeFrom',
  'handleProjection',
  'writeBlob',
])

const durableJournal = (() => {
  const writerOf = AgentJournalModule.AgentJournal__get_Writer
  if (typeof writerOf !== 'function') throw new Error('AgentJournal.get_Writer missing from dist')
  return {
    writer: (value) => writerOf(value),
    blobWriter: (value) => {
      const writer = writerOf(value)
      return typeof esGetBlobWriter === 'function' ? esGetBlobWriter(writer) : writer.BlobWriter
    },
    path: (value) => {
      const writer = writerOf(value)
      return typeof esGetFilePath === 'function' ? esGetFilePath(writer) : writer.FilePath
    },
  }
})()


/** Strip `blobs/<oid>` → Git OID hex used as InMemoryGitRawStore.objects key. */
const gitOidFromBlobRef = (blobRef) => {
  const relative =
    typeof blobRef === 'string'
      ? blobRef
      : blobRef?.fields?.[0] ??
        (() => {
          throw new Error('blobRef must be a BlobRef DU or blobs/<oid> string')
        })()
  const prefix = 'blobs/'
  if (!relative.startsWith(prefix) || relative.length <= prefix.length || relative.includes('/', prefix.length)) {
    throw new Error(`invalid EventStore blob ref: ${relative}`)
  }
  return relative.slice(prefix.length)
}

const rawStoreOf = (journal) => {
  const raw = durableJournal.blobWriter(journal)?.raw
  if (raw == null || !(raw.objects instanceof Map)) {
    throw new Error('journal BlobWriter has no mutable InMemoryGitRawStore.objects Map')
  }
  return raw
}

export const agentJournal = {
  create: ({ directory, runtime = 'rt_1', pid = 4242, startedAt = '2026-01-01T00:00:00Z' } = {}) => {
    const pair = registerEventStore(directory, freshEventStorePair())
    const [writer, initEnvelope] = esWriterCreate(
      runtimeId(runtime),
      pid,
      utcOffset(startedAt),
      pair.store,
      pair.raw,
    )
    const result = resultOf(AgentJournalCreate.createFromEventStore(writer, initEnvelope))
    return result.ok
      ? { ok: true, journal: result.value, raw: pair.raw, dispose: () => result.value.Dispose() }
      : result
  },
  /**
   * Restart: resumeOrCreate on the EventStore registered for `directory`.
   * `boot` is accepted for call-site compatibility but ignored (store is source of truth).
   */
  createFromBoot: ({
    directory,
    boot: _boot,
    runtime = 'rt_restart',
    pid = 4243,
    startedAt = '2026-01-01T01:00:00Z',
  } = {}) => {
    const pair = eventStoreFor(directory)
    const resumed = resultOf(
      esWriterResume(runtimeId(runtime), pid, utcOffset(startedAt), pair.store, pair.raw),
    )
    if (!resumed.ok) return resumed
    const [writer, _init, projection] = resumed.value
    const result = resultOf(AgentJournalCreate.createFromProjection(writer, projection))
    return result.ok
      ? { ok: true, journal: result.value, dispose: () => result.value.Dispose() }
      : result
  },
  appendAgent: (streamId, providerRun, agentFactValue, journal) =>
    resultOf(AgentJournalCreate.appendAgent(streamId, providerRun, agentFactValue, journal)),
  appendMagicTodo: (streamId, providerRun, magicTodoFactValue, journal) =>
    resultOf(AgentJournalCreate.appendMagicTodo(streamId, providerRun, magicTodoFactValue, journal)),
  appendManagerLifecycle: (streamId, lifecycleFactValue, journal) =>
    resultOf(AgentJournalCreate.appendManagerLifecycle(streamId, lifecycleFactValue, journal)),
  snapshot: (journal) => AgentJournalCreate.snapshot(journal),
  /** Module-level revision (AgentJournal.revision). */
  revision: (journal) => AgentJournalCreate.revision(journal),
  snapshotWithRevision: (journal) => AgentJournalCreate.snapshotWithRevision(journal),
  /** Module-level awaitChangeFrom (fromRevision, journal) → Task/Promise. */
  awaitChangeFrom: (fromRevision, journal) => AgentJournalCreate.awaitChangeFrom(fromRevision, journal),
  handleProjection: (journal, parentId) => AgentJournalCreate.handleProjection(journal, parentId),
  /** Blob write receipt: { BlobRef, BlobDigest } after Ok. */
  writeBlob: (content, journal) => resultOf(AgentJournalCreate.writeBlob(content, journal)),
  readBlob: (journal, ref) => resultOf(durableJournal.blobWriter(journal).Read(ref)),
  /**
   * Test-only: delete a blob from the journal's InMemoryGitRawStore so
   * BlobWriter.Read fails closed (EventStore bodies are not RuntimePath files).
   */
  deleteBlob: (journal, blobRef) => {
    const raw = rawStoreOf(journal)
    const oid = gitOidFromBlobRef(blobRef)
    if (!raw.objects.delete(oid)) {
      throw new Error(`deleteBlob: oid ${oid} not present in raw store`)
    }
  },
  /**
   * Test-only: overwrite blob bytes under the same Git OID so reads succeed
   * with tampered content (digest mismatch / corrupt JSON paths).
   */
  replaceBlobContent: (journal, blobRef, content) => {
    const raw = rawStoreOf(journal)
    const oid = gitOidFromBlobRef(blobRef)
    if (!raw.objects.has(oid)) {
      throw new Error(`replaceBlobContent: oid ${oid} not present in raw store`)
    }
    const bytes =
      typeof content === 'string'
        ? new TextEncoder().encode(content)
        : content instanceof Uint8Array
          ? content
          : (() => {
              throw new Error('replaceBlobContent: content must be string or Uint8Array')
            })()
    const prior = raw.objects.get(oid)
    raw.objects.set(
      oid,
      prior?.constructor
        ? new prior.constructor(/* Blob */ 0, [bytes])
        : { tag: 0, fields: [bytes] },
    )
  },
  persistedEnvelopes: (durable) => {
    const writer = durableJournal.writer(durable)
    const blobWriter = durableJournal.blobWriter(durable)
    const raw = blobWriter?.raw
    if (raw == null) {
      throw new Error('persistedEnvelopes: EventStore journal missing IGitRawStore on BlobWriter')
    }
    const snapshot =
      typeof esGetStoreSnapshot === 'function' ? esGetStoreSnapshot(writer) : writer.baseSnapshot
    const loaded = resultOf(esLoadJournalEnvelopes(raw, snapshot))
    if (!loaded.ok) {
      throw new Error(`persistedEnvelopes EventStore load failed: ${loaded.error}`)
    }
    return listItems(loaded.value)
  },
  observeWaiters: (journal) => {
    const waiters = journal.waiters
    if (!Array.isArray(waiters)) throw new Error('AgentJournal waiter collection is unavailable')

    const observed = []
    let resolveNext
    const notify = (entry) => {
      if (resolveNext !== undefined) {
        const resolve = resolveNext
        resolveNext = undefined
        resolve(entry)
      } else {
        observed.push(entry)
      }
    }
    const proxy = new Proxy(waiters, {
      get(target, property, receiver) {
        if (property === 'push') {
          return (...entries) => {
            const length = Array.prototype.push.apply(target, entries)
            entries.forEach(notify)
            return length
          }
        }
        return Reflect.get(target, property, receiver)
      },
    })
    journal.waiters = proxy

    return {
      next: () => {
        if (observed.length > 0) return Promise.resolve(observed.shift())
        if (resolveNext !== undefined) throw new Error('only one waiter observation may be pending')
        return new Promise((resolve) => {
          resolveNext = resolve
        })
      },
      restore: () => {
        if (journal.waiters === proxy) journal.waiters = waiters
      },
    }
  },
}

/** Opaque boot handle for createFromBoot call-site compatibility (store is authoritative). */
export const bootSnapshot = {
  load: (directory) => {
    const pair = eventStoreFor(directory)
    const loaded = resultOf(esLoadJournalEnvelopes(pair.raw, pair.store.OpenSnapshot()))
    if (!loaded.ok) {
      return { Envelopes: toList([]), Diagnostics: toList([String(loaded.error)]), Frontier: mapOf({}) }
    }
    return {
      Envelopes: toList(listItems(loaded.value)),
      Diagnostics: toList([]),
      Frontier: mapOf({}),
    }
  },
}