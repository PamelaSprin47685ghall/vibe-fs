// WHAT[HOST-BOUNDARY-029] — the FatalProcess contract is proven against a
// REAL child process, not a harness flag.
//
// W11 / F32: console/report + kill is the single physical exit. The child
// (../fixtures/fatal-process-child.fixture.mjs) triggers the actual compiled
// owner paths — FatalProcess.trip and Diagnostic.fatal — with the
// WANXIANGSHU_NO_FATAL_EXIT suppression deleted, so the production
// SIGKILL-or-exit(1) sequence fires. The parent observes the hard exit, then
// asserts the durable aftermath by reopening the store it committed before
// the fuse.
//
// Platform rule: assert SIGKILL where the platform delivers a signal, else
// exit code 1 — never hardcode Unix. Node reports signal kills as
// `signal: 'SIGKILL', code: null`; the process.exit(1) fallback surfaces as
// `code: 1, signal: null`. Both shapes prove the process did not return.

import assert from 'node:assert/strict'
import { spawn } from 'node:child_process'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'

import * as journal from '../../../dist/Persistence/Journal/Surface.js'

const here = fileURLToPath(new URL('.', import.meta.url))
const fixture = join(here, 'fixtures', 'fatal-process-child.fixture.mjs')

const cleanEnv = (extra = {}) => {
  const env = { ...process.env, ...extra }
  delete env.WANXIANGSHU_NO_FATAL_EXIT
  env.NODE_TEST_CONTEXT = 'inherited-host-value'
  return env
}

const runChild = (args, envExtra) =>
  new Promise((resolve) => {
    const child = spawn(process.execPath, [fixture, ...args], {
      env: cleanEnv(envExtra),
      stdio: ['ignore', 'pipe', 'pipe'],
    })
    let stdout = ''
    let stderr = ''
    child.stdout.on('data', (chunk) => { stdout += String(chunk) })
    child.stderr.on('data', (chunk) => { stderr += String(chunk) })
    child.on('close', (code, signal) => resolve({ code, signal, stdout, stderr }))
  })

const assertHardExit = (result, mode) => {
  const killedBySignal = result.signal !== null
  const exitedOne = result.code === 1 && result.signal === null
  assert.ok(
    killedBySignal || exitedOne,
    `${mode}: must die by signal or exit(1); got code=${result.code} signal=${result.signal}`,
  )
  assert.match(result.stdout, /before-fatal/, `${mode}: must report before killing`)
  assert.doesNotMatch(result.stdout, /after-fatal/, `${mode}: must never return past the fuse`)
}

test('WHAT[HOST-BOUNDARY-029] fatal child reports once then dies without returning', async () => {
  const result = await runChild(['trip'])
  assertHardExit(result, 'trip')
  const report = JSON.parse(result.stderr.trim().split('\n').pop())
  assert.equal(report.operation, 'fixture-fatal')
  assert.equal(result.stderr.trim().split('\n').filter(Boolean).length, 1, 'exactly one incident report')
})

test('WHAT[HOST-BOUNDARY-029] diagnostic owner path reports once then dies without returning', async () => {
  const result = await runChild(['diagnostic'])
  assertHardExit(result, 'diagnostic')
  const report = JSON.parse(result.stderr.trim().split('\n').pop())
  assert.equal(report.operation, 'fixture-fatal-diagnostic')
})

test('WHAT[HOST-BOUNDARY-029] committed facts survive the fatal exit and reopen cleanly', async () => {
  const directory = mkdtempSync(join(tmpdir(), 'wxs-fatal-exit-'))
  try {
    const boot = await journal.JournalSurface_boot(directory, 'rt-fatal-exit', 4242, '2026-09-14T00:00:00Z')
    assert.equal(boot.ok, true, boot.ok ? '' : boot.error)
    // Durable write BEFORE the fuse: a content-addressed payload through the
    // production blob writer. The EventStore persists it; reopening the same
    // directory must read it back after the child dies.
    const written = await journal.JournalSurface_writePayload(boot.journal, 'committed-before-fuse')
    assert.equal(written.ok, true, written.ok ? '' : written.error)
    journal.JournalSurface_dispose(boot.journal)

    const result = await runChild(['trip'])
    assertHardExit(result, 'trip')

    const reopened = await journal.JournalSurface_boot(directory, 'rt-fatal-exit-reopen', 4242, '2026-09-14T00:00:01Z')
    assert.equal(reopened.ok, true, reopened.ok ? '' : reopened.error)
    const reread = await journal.JournalSurface_readPayload(reopened.journal, written.blobRef)
    assert.equal(reread.ok, true, reread.ok ? '' : reread.error)
    assert.equal(reread.content, 'committed-before-fuse')
    journal.JournalSurface_dispose(reopened.journal)
  } finally {
    rmSync(directory, { recursive: true, force: true })
  }
})

test('WHAT[HOST-BOUNDARY-029] failing console capture cannot bypass the fuse', async () => {
  const result = await runChild(['console-throw'])
  assertHardExit(result, 'console-throw')
})

test('WHAT[HOST-BOUNDARY-029] closed stdio cannot bypass the fuse', async () => {
  const result = await runChild(['pipe-close'])
  assertHardExit(result, 'pipe-close')
})

test('WHAT[HOST-BOUNDARY-029] repeated incident reports once then dies', async () => {
  const result = await runChild(['double-trip'])
  assertHardExit(result, 'double-trip')
  assert.equal(result.stderr.trim().split('\n').filter(Boolean).length, 1, 'rebroadcast must stay idempotent')
})

test('WHAT[HOST-BOUNDARY-029] normal rejection, stale callback, and exhaustion never exit the process', async () => {
  for (const mode of ['reject', 'stale-callback', 'exhausted']) {
    const result = await runChild([mode])
    assert.equal(result.signal, null, `${mode}: must not die by signal`)
    assert.equal(result.code, 0, `${mode}: must exit 0`)
    assert.match(result.stdout, new RegExp(`ok-${mode}`), `${mode}: must complete normally`)
  }
})
