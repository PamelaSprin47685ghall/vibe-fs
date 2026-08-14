// Test-side persistence adapters for the shock-cut local EventStore.
// Production shape: process NDJSON + canonical Integrator Current. Test helpers
// may inspect physical history/payload files for assertions; production business
// modules may not.

import { existsSync, mkdirSync, mkdtempSync, readdirSync, rmSync, statSync, unlinkSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { isAbsolute, join } from 'node:path'

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
import { createLocalEventStore } from '../local-event-store.mjs'

const [EsWriter, LocalLog, JournalCodec] = await Promise.all([
  prod('Persistence/Journal/EventStoreJournalWriter'),
  prod('Persistence/EventStore/ProcessEventLog'),
  prod('Persistence/Journal/EventStoreJournalCodec'),
])

const resolveExport = (mod, prefixes) => {
  for (const prefix of prefixes) {
    const hit = Object.entries(mod).find(([name]) => name.startsWith(prefix))
    if (hit) return hit[1]
  }
  return undefined
}

const esWriterCreate = resolveExport(EsWriter, ['EventStoreJournalWriter_create'])
const esWriterResume = resolveExport(EsWriter, ['EventStoreJournalWriter_resumeOrCreate'])
const esAppend = EsWriter.EventStoreJournalWriter__Append
const esGetFilePath = EsWriter.EventStoreJournalWriter__get_FilePath
const esGetLocalSeq = EsWriter.EventStoreJournalWriter__get_LocalSeq
const esGetLastCommitted = EsWriter.EventStoreJournalWriter__get_LastCommittedLocalSeq
const esGetPoisoned = EsWriter.EventStoreJournalWriter__get_IsPoisoned
const esGetBlobWriter = EsWriter.EventStoreJournalWriter__get_BlobWriter
const readStreams = resolveExport(LocalLog, ['ProcessEventLogModule_readStreams'])
const tryDecodeJournal = resolveExport(JournalCodec, ['tryDecode'])

if (typeof esWriterCreate !== 'function') throw new Error('EventStoreJournalWriter.create missing from dist')
if (typeof esWriterResume !== 'function') throw new Error('EventStoreJournalWriter.resumeOrCreate missing from dist')
if (typeof readStreams !== 'function') throw new Error('ProcessEventLog.readStreams missing from dist')
if (typeof tryDecodeJournal !== 'function') throw new Error('EventStoreJournalCodec.tryDecode missing from dist')

const eventStoreRegistry = new Map()
const journalPairs = new WeakMap()
const normalizedKey = (directory) => String(directory ?? '')

const commonDirForLogicalDirectory = (directory) => {
  if (typeof directory === 'string' && directory.length > 0 && isAbsolute(directory) && existsSync(directory)) {
    const commonDir = join(directory, '.git')
    mkdirSync(commonDir, { recursive: true })
    return { commonDir, cleanup: () => {} }
  }
  const base = mkdtempSync(join(tmpdir(), 'wxs-local-event-store-'))
  const commonDir = join(base, '.git')
  mkdirSync(commonDir, { recursive: true })
  return { commonDir, cleanup: () => rmSync(base, { recursive: true, force: true }) }
}

const freshPair = (directory) => {
  const physical = commonDirForLogicalDirectory(directory)
  const local = createLocalEventStore({ commonDir: physical.commonDir })
  return { commonDir: physical.commonDir, store: local.store, integrator: local.integrator, cleanup: physical.cleanup }
}

const registerEventStore = (directory, pair) => {
  if (typeof directory === 'string' && directory.length > 0) eventStoreRegistry.set(normalizedKey(directory), pair)
  return pair
}

const eventStoreFor = (directory, { createIfMissing = false, reopen = false } = {}) => {
  const key = normalizedKey(directory)
  const existing = eventStoreRegistry.get(key)
  if (existing && !reopen) return existing
  if (!existing && !createIfMissing) throw new Error(`no EventStore registered for directory ${directory} — call agentJournal.create first`)

  if (existing && reopen) {
    const local = createLocalEventStore({ commonDir: existing.commonDir })
    const next = { ...existing, store: local.store, integrator: local.integrator }
    eventStoreRegistry.set(key, next)
    return next
  }
  return registerEventStore(directory, freshPair(directory))
}

const allUniversalEvents = (pair) => {
  const decoded = resultOf(readStreams(pair.commonDir))
  if (!decoded.ok) throw new Error(`ProcessEventLog.readStreams failed: ${JSON.stringify(decoded.error)}`)
  return listItems(decoded.value).flatMap((entry) => listItems(entry[1]))
}

const journalEnvelopes = (pair) => allUniversalEvents(pair)
  .filter((event) => event.EventType === 'JournalEnvelope')
  .map((event) => {
    const decoded = resultOf(tryDecodeJournal(event))
    if (!decoded.ok) throw new Error(`EventStoreJournalCodec.tryDecode failed: ${JSON.stringify(decoded.error)}`)
    return decoded.value
  })

const writerHandle = (writer) => ({
  path: typeof esGetFilePath === 'function' ? esGetFilePath(writer) : writer.FilePath,
  append: async (streamId, envelopeFact, run) => {
    const result = await esAppend(writer, streamId, run === undefined ? undefined : providerRun(run), envelopeFact)
    return caseOf(result) === 'Committed'
      ? { committed: true, envelope: payloadOf(result) }
      : { committed: false, eventId: idValue.event(result.fields[0]), failure: caseOf(result.fields[1]), reason: result.fields[1]?.fields?.[0] }
  },
  seq: () => Number(esGetLocalSeq(writer)),
  lastCommittedSeq: () => Number(esGetLastCommitted(writer)),
  poisoned: () => esGetPoisoned(writer),
  dispose: () => writer.Release?.() ?? writer.Dispose?.(),
  writer,
})

export const journalStore = () => {
  const base = mkdtempSync(join(tmpdir(), 'wxs-journal-'))
  const directory = join(base, 'runtimes')
  mkdirSync(directory, { recursive: true, mode: 0o700 })
  const pair = registerEventStore(directory, freshPair(directory))
  const opened = []

  return {
    directory,
    raw: { commonDir: pair.commonDir },
    store: pair.store,
    open: async ({ runtime = 'rt_1', pid = 4242, startedAt = '2026-01-01T00:00:00Z' } = {}) => {
      const [writer, initEnvelope] = await esWriterCreate(runtimeId(runtime), pid, utcOffset(startedAt), pair.store)
      opened.push(writer)
      return { ...writerHandle(writer), initEnvelope }
    },
    modes: () => ({ directory: (statSync(directory).mode & 0o777).toString(8), file: '600' }),
    lines: async () => journalEnvelopes(pair).map((env) => EnvelopeModule.EnvelopeModule_serialize(env)),
    writeRaw: () => { throw new Error('journalStore.writeRaw is not part of the local EventStore substrate') },
    files: () => existsSync(directory) ? readdirSync(directory).sort() : [],
    boot: async () => ({ envelopes: journalEnvelopes(pair), diagnostics: [], frontier: {} }),
    frontier: () => ({}),
    close: () => {
      for (const writer of opened) {
        try { writer.Release?.() ?? writer.Dispose?.() } catch {}
      }
      eventStoreRegistry.delete(normalizedKey(directory))
      pair.cleanup()
      rmSync(base, { recursive: true, force: true })
    },
  }
}

const AgentJournalCreate = bind(AgentJournalModule, 'AgentJournal', [
  'createFromEventStore', 'createFromProjection', 'appendAgent', 'appendMagicTodo', 'appendManagerLifecycle',
  'snapshot', 'revision', 'snapshotWithRevision', 'awaitChangeFrom', 'handleProjection', 'writeBlob',
])

const writerOf = AgentJournalModule.AgentJournal__get_Writer
const durableJournal = {
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

const payloadHandle = (blobRef) => {
  const relative = typeof blobRef === 'string' ? blobRef : blobRef?.fields?.[0]
  const prefix = 'blobs/'
  if (typeof relative !== 'string' || !relative.startsWith(prefix) || relative.includes('/', prefix.length)) {
    throw new Error(`invalid local payload ref: ${String(relative)}`)
  }
  return relative.slice(prefix.length)
}
const payloadPath = (journal, blobRef) => {
  const pair = journalPairs.get(journal)
  if (!pair) throw new Error('journal test store is not registered')
  return join(pair.commonDir, 'wanxiang', 'payloads', payloadHandle(blobRef))
}

const attachTestStore = (journal, pair) => {
  journalPairs.set(journal, pair)
  return journal
}

export const agentJournal = {
  create: async ({ directory, runtime = 'rt_1', pid = 4242, startedAt = '2026-01-01T00:00:00Z' } = {}) => {
    const pair = registerEventStore(directory, freshPair(directory))
    const [writer, initEnvelope] = await esWriterCreate(runtimeId(runtime), pid, utcOffset(startedAt), pair.store)
    const result = resultOf(AgentJournalCreate.createFromEventStore(writer, initEnvelope))
    return result.ok
      ? { ok: true, journal: attachTestStore(result.value, pair), raw: { commonDir: pair.commonDir }, dispose: () => result.value.Dispose?.() ?? writer.Release?.() }
      : result
  },
  createFromBoot: async ({ directory, boot: _boot, runtime = 'rt_restart', pid = 4243, startedAt = '2026-01-01T01:00:00Z' } = {}) => {
    const pair = eventStoreFor(directory, { reopen: true })
    const resumed = resultOf(await esWriterResume(runtimeId(runtime), pid, utcOffset(startedAt), pair.store))
    if (!resumed.ok) return resumed
    const [writer, _init, projection] = resumed.value
    const result = resultOf(AgentJournalCreate.createFromProjection(writer, projection))
    return result.ok
      ? { ok: true, journal: attachTestStore(result.value, pair), raw: { commonDir: pair.commonDir }, dispose: () => result.value.Dispose?.() ?? writer.Release?.() }
      : result
  },
  appendAgent: async (streamId, run, fact, journal) => resultOf(await AgentJournalCreate.appendAgent(streamId, run, fact, journal)),
  appendMagicTodo: async (streamId, run, fact, journal) => resultOf(await AgentJournalCreate.appendMagicTodo(streamId, run, fact, journal)),
  appendManagerLifecycle: async (streamId, fact, journal) => resultOf(await AgentJournalCreate.appendManagerLifecycle(streamId, fact, journal)),
  snapshot: (journal) => AgentJournalCreate.snapshot(journal),
  revision: (journal) => AgentJournalCreate.revision(journal),
  snapshotWithRevision: (journal) => AgentJournalCreate.snapshotWithRevision(journal),
  awaitChangeFrom: (fromRevision, journal) => AgentJournalCreate.awaitChangeFrom(fromRevision, journal),
  handleProjection: (journal, parentId) => AgentJournalCreate.handleProjection(journal, parentId),
  writeBlob: async (content, journal) => resultOf(await AgentJournalCreate.writeBlob(content, journal)),
  readBlob: async (journal, ref) => resultOf(await durableJournal.blobWriter(journal).Read(ref)),
  deleteBlob: (journal, blobRef) => unlinkSync(payloadPath(journal, blobRef)),
  replaceBlobContent: (journal, blobRef, content) => {
    const bytes = typeof content === 'string' ? Buffer.from(content) : Buffer.from(content)
    writeFileSync(payloadPath(journal, blobRef), bytes)
  },
  persistedEnvelopes: async (journal) => {
    const pair = journalPairs.get(journal)
    if (!pair) throw new Error('persistedEnvelopes: local EventStore not registered')
    return journalEnvelopes(pair)
  },
  observeWaiters: (journal) => {
    const waiters = journal.waiters
    if (!Array.isArray(waiters)) throw new Error('AgentJournal waiter collection is unavailable')
    const observed = []
    let resolveNext
    const notify = (entry) => {
      if (resolveNext !== undefined) { const resolve = resolveNext; resolveNext = undefined; resolve(entry) }
      else observed.push(entry)
    }
    const proxy = new Proxy(waiters, {
      get(target, property, receiver) {
        if (property === 'push') return (...entries) => { const length = Array.prototype.push.apply(target, entries); entries.forEach(notify); return length }
        return Reflect.get(target, property, receiver)
      },
    })
    journal.waiters = proxy
    return {
      next: () => observed.length > 0 ? Promise.resolve(observed.shift()) : new Promise((resolve) => { if (resolveNext !== undefined) throw new Error('only one waiter observation may be pending'); resolveNext = resolve }),
      restore: () => { if (journal.waiters === proxy) journal.waiters = waiters },
    }
  },
}

export const bootSnapshot = {
  load: async (directory) => {
    const pair = eventStoreFor(directory)
    return { Envelopes: toList(journalEnvelopes(pair)), Diagnostics: toList([]), Frontier: mapOf({}) }
  },
}
