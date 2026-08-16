// Boot replay is owned by CanonicalIntegrator over local writer files.

import assert from 'node:assert/strict'
import { execFileSync } from 'node:child_process'
import { existsSync, mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'

import * as journal from '../../../dist/Persistence/Journal/Surface.js'

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

test('WHAT[DURABLE-EVENTS-013] restart_replays_prior_writer_files_then_fresh_runtime_starts_LocalSeq_at_1', async () => {
  await withRepo(async (commonDir) => {
    const first = mustOk(await journal.bootWithWriterId(commonDir, 'boot-writer-a', 'rt_before', 4242, '2026-04-01T00:00:00Z'), 'first boot')

    assert.equal(Number(first.localSeq), 1)
    const firstAppend = mustOk(
      await journal.appendAgent(first.journal, { kind: 'Session', session: 'ses_es_boot' }, null, CLOSED),
      'first append',
    )
    assert.ok(journal.hasSession(first.journal, 'ses_es_boot'))
    first.release?.()

    const restarted = mustOk(await journal.bootWithWriterId(commonDir, 'boot-writer-b', 'rt_after', 5252, '2026-04-02T00:00:00Z'), 'reboot')
    assert.equal(Number(restarted.localSeq), 1, 'fresh RuntimeId owns a fresh LocalSeq domain')
    assert.ok(journal.hasSession(restarted.journal, 'ses_es_boot'), 'prior journal fact is rebuilt only through Integrator boot replay')
    restarted.release?.()
  })
})

test('WHAT[DURABLE-EVENTS-020] empty_boot_is_read_only_and_keeps_RuntimeStarted_in_memory_until_activation', async () => {
  await withRepo(async (commonDir) => {
    const booted = mustOk(await journal.bootWithWriterId(commonDir, 'boot-empty', 'rt_empty', 6001, '2026-05-01T00:00:00Z'), 'empty boot')
    assert.equal(Number(booted.localSeq), 1)
    assert.equal(existsSync(join(commonDir, 'wanxiang', 'events', 'boot-empty.ndjson')), false)
    booted.release?.()
  })
})

test('WHAT[DURABLE-EVENTS-013] boot_and_live_use_one_CanonicalIntegrator_program', async () => {
  const { readFile } = await import('node:fs/promises')
  const integrator = await readFile(new URL('../../../src/Wanxiangshu/Persistence/EventStore/CanonicalIntegrator.fs', import.meta.url), 'utf8')
  const writer = await readFile(new URL('../../../src/Wanxiangshu/Persistence/Journal/EventStoreJournalWriter.fs', import.meta.url), 'utf8')
  assert.match(integrator, /EventKWayMerge\.merge/)
  assert.match(integrator, /integrateOne/)
  assert.match(integrator, /prepareLive/)
  assert.doesNotMatch(writer, /readStreams|loadEvent|Fold\.apply|OpenSnapshot/)
})
