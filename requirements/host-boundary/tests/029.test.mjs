import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { spawn } = await import("node:child_process");
const { mkdtempSync, rmSync } = await import("node:fs");
const { tmpdir } = await import("node:os");
const { join } = await import("node:path");
const { fileURLToPath } = await import("node:url");
const { default: test } = await import("node:test");
const journal = await import("../../../dist/Persistence/Journal/Surface.js");

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

test('WHAT[host-boundary-029] fatal child reports once then dies without returning', async () => {
  const result = await runChild(['trip'])
  assertHardExit(result, 'trip')
  const report = JSON.parse(result.stderr.trim().split('\n').pop())
  assert.equal(report.operation, 'fixture-fatal')
  assert.equal(result.stderr.trim().split('\n').filter(Boolean).length, 1, 'exactly one incident report')
})
test('WHAT[host-boundary-029] diagnostic owner path reports once then dies without returning', async () => {
  const result = await runChild(['diagnostic'])
  assertHardExit(result, 'diagnostic')
  const report = JSON.parse(result.stderr.trim().split('\n').pop())
  assert.equal(report.operation, 'fixture-fatal-diagnostic')
})
test('WHAT[host-boundary-029] committed facts survive the fatal exit and reopen cleanly', async () => {
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
test('WHAT[host-boundary-029] failing console capture cannot bypass the fuse', async () => {
  const result = await runChild(['console-throw'])
  assertHardExit(result, 'console-throw')
})
test('WHAT[host-boundary-029] closed stdio cannot bypass the fuse', async () => {
  const result = await runChild(['pipe-close'])
  assertHardExit(result, 'pipe-close')
})
test('WHAT[host-boundary-029] repeated incident reports once then dies', async () => {
  const result = await runChild(['double-trip'])
  assertHardExit(result, 'double-trip')
  assert.equal(result.stderr.trim().split('\n').filter(Boolean).length, 1, 'rebroadcast must stay idempotent')
})
test('WHAT[host-boundary-029] normal rejection, stale callback, and exhaustion never exit the process', async () => {
  for (const mode of ['reject', 'stale-callback', 'exhausted']) {
    const result = await runChild([mode])
    assert.equal(result.signal, null, `${mode}: must not die by signal`)
    assert.equal(result.code, 0, `${mode}: must exit 0`)
    assert.match(result.stdout, new RegExp(`ok-${mode}`), `${mode}: must complete normally`)
  }
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { default: path } = await import("node:path");
const { fileURLToPath } = await import("node:url");
const { readCompileShardInventory } = await import("../../../scripts/lib/compile-shards.mjs");
const { buildSubsystemInventory } = await import("../../../scripts/checks/subsystems.mjs");
const HostSignalSurface = await import("../../../dist/OpenCode/Host/HostSignalSurface.js");
const { assertEffectIsInjected, assertFatalBoundary, assertOptionalObservationNoninterference, assertPureContract } = await import("../../structured-workflow/tests/support/m6-boundary-proof.mjs");

const ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..', '..')
const requireShard = (projects, shardId) => {
  const matches = [...projects.values()].filter((candidate) => candidate.shard === shardId)
  assert.equal(matches.length, 1, `${shardId} must resolve to exactly one production compile shard`)
  return matches[0]
}
const relSources = (project) => project.implementationFiles.map((p) => path.relative(ROOT, p)).sort()
const refShards = (project, projects) => project.references.map((refPath) => projects.get(refPath).shard).sort()
const closureSources = (root, projects) => {
  const closure = new Set()
  const pending = [root]
  while (pending.length > 0) {
    const project = pending.pop()
    if (closure.has(project)) continue
    closure.add(project)
    for (const refPath of project.references) {
      pending.push(projects.get(refPath))
    }
  }
  return new Set([...closure].flatMap(relSources))
}

test('WHAT[host-boundary-029] fatal vocabulary stays pure and physical execution is composition-only', () => {
  assertPureContract()
  assertFatalBoundary('host-boundary')
})
}
