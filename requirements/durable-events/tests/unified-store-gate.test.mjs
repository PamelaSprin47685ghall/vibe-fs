/**
 * Unified-store gate: Phase 1–3 fixtures RED + P4U2 clean-break RED + production GREEN
 * (empty git-bypass / dual-write allowlists).
 */
import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import test from 'node:test'
import {
  DUAL_WRITE_ALLOWLIST,
  GIT_BYPASS_ALLOWLIST,
  NON_STORE_SCHEMA_VERSION_SITES,
  SCANNER_IDS,
  collectProductionEntries,
  scanDualWrite,
  scanFeatureRef,
  scanFiles,
  scanGitBypass,
  scanNoMigrator,
  scanSchemaVersionInStoreContext,
  scanStudentQaRevival,
  scanText,
} from '../../../scripts/checks/unified-store-gate.mjs'

const readFixture = (name) =>
  readFileSync(new URL(`./fixtures/${name}`, import.meta.url), 'utf8')

test('scanner ids cover Phase 1–3 and P4U2 clean-break rules', () => {
  assert.deepEqual([...SCANNER_IDS], [
    'feature-ref',
    'schema-version-in-store-context',
    'git-bypass',
    'student-qa-revival',
    'no-migrator',
    'dual-write',
  ])
})

test('fixture unified-store-feature-ref.fs is RED for feature-ref', () => {
  const source = readFixture('unified-store-feature-ref.fs')
  const hits = scanFeatureRef(source, 'Domain/CasebookStore.fs')
  assert.ok(hits.length >= 1, 'expected feature-ref violation')
  assert.equal(hits[0].id, 'feature-ref')
  assert.match(hits[0].text, /refs\/wanxiang\/foo/)
  assert.equal(scanSchemaVersionInStoreContext(source).length, 0)
  assert.equal(scanGitBypass(source, 'Domain/CasebookStore.fs').length, 0)
})

test('fixture unified-store-schema-version.fs is RED for schema-version-in-store-context', () => {
  const source = readFixture('unified-store-schema-version.fs')
  const hits = scanSchemaVersionInStoreContext(source, 'Domain/EventStore.fs')
  assert.ok(hits.length >= 1, 'expected schema-version-in-store-context violation')
  assert.equal(hits[0].id, 'schema-version-in-store-context')
  assert.match(hits[0].text, /schemaVersion/)
  assert.equal(scanFeatureRef(source, 'Domain/EventStore.fs').length, 0)
  assert.equal(scanGitBypass(source, 'Domain/EventStore.fs').length, 0)
})

test('fixture unified-store-git-bypass.fs is RED for git-bypass', () => {
  const source = readFixture('unified-store-git-bypass.fs')
  const hits = scanGitBypass(source, 'Domain/FeatureGit.fs')
  assert.ok(hits.length >= 1, 'expected git-bypass violation')
  assert.equal(hits[0].id, 'git-bypass')
  assert.match(hits[0].text, /FileName\s*=\s*"git"/)
  assert.equal(scanFeatureRef(source, 'Domain/FeatureGit.fs').length, 0)
  assert.equal(scanSchemaVersionInStoreContext(source).length, 0)
})

test('fixture unified-store-student-qa-revival.fs is RED for student-qa-revival', () => {
  const source = readFixture('unified-store-student-qa-revival.fs')
  const hits = scanStudentQaRevival(source, 'src/Wanxiangshu/Infrastructure/OpenCode/Host/StudentQaStore.fs')
  assert.ok(hits.length >= 1, 'expected student-qa-revival violation')
  assert.ok(hits.every((h) => h.id === 'student-qa-revival'))
  assert.ok(hits.some((h) => /StudentQaStore/.test(h.text)))
  assert.ok(hits.some((h) => /QA\.md/.test(h.text)))
})

test('fixture unified-store-no-migrator.mjs is RED for no-migrator', () => {
  const source = readFixture('unified-store-no-migrator.mjs')
  const hits = scanNoMigrator(source, 'tests/integration/persist/migration.test.mjs')
  assert.ok(hits.length >= 1, 'expected no-migrator violation')
  assert.ok(hits.every((h) => h.id === 'no-migrator'))
  assert.ok(
    hits.some((h) => /LegacyProjection|LegacyMigrator|wanxiangshu-next/i.test(h.text + h.label)),
    'expected LegacyProjection / LegacyMigrator / wanxiangshu-next signal',
  )
})

test('synthetic LegacyProjection≡NewProjection claim is RED for no-migrator', () => {
  const source = 'assert.deepEqual(LegacyProjection, NewProjection) // LegacyProjection == NewProjection'
  const hits = scanNoMigrator(source, 'tests/integration/persist/migration.test.mjs')
  assert.ok(hits.some((h) => /LegacyProjection/.test(h.label) || /LegacyProjection/.test(h.text)))
})

test('fixture unified-store-dual-write.fs is RED for dual-write', () => {
  const source = readFixture('unified-store-dual-write.fs')
  const hits = scanDualWrite(source, 'src/Wanxiangshu/Application/DualWriteBridge.fs')
  assert.ok(hits.length >= 1, 'expected dual-write violation')
  assert.equal(hits[0].id, 'dual-write')
})

test('Journal-only or EventStore-only modules are not dual-write', () => {
  const journalOnly = [
    'module RuntimePath',
    'let root = joinPath common "wanxiangshu-next"',
    'let file = sprintf "%s.ndjson" runtimeId',
    'type AgentJournal(writer: JournalWriter) =',
    '    member _.AppendAgent fact = writer.Append fact',
  ].join('\n')
  assert.equal(scanDualWrite(journalOnly, 'src/Wanxiangshu/Journal/AgentJournal.fs').length, 0)

  const eventStoreOnly = [
    'module EventStore',
    'let append (store: IEventStore) candidate =',
    '    store.Append candidate.NewEvents',
    'let create store = EventStore.create store',
  ].join('\n')
  assert.equal(
    scanDualWrite(eventStoreOnly, 'src/Wanxiangshu/Persistence/EventStore/Store.fs').length,
    0,
  )
})

test('schemaVersion without store context is not flagged (host/authored allow)', () => {
  const host = [
    'module HandleCompletionCodec',
    'let encode () =',
    '    [ "schemaVersion", box 2',
    '      "finality", str "completed" ]',
  ].join('\n')
  assert.equal(scanSchemaVersionInStoreContext(host, 'Session/HandleCompletionCodec.fs').length, 0)

  const enforcer = [
    'module EnforcerCatalog',
    'let validate (schemaVersion: int) rules =',
    '    if schemaVersion <> 1 then Error "bad" else Ok rules',
  ].join('\n')
  assert.equal(scanSchemaVersionInStoreContext(enforcer, 'Domain/EnforcerCatalog.fs').length, 0)
})

test('always-forbidden store version tokens are RED without extra context', () => {
  for (const token of ['storageVersion', 'journalVersion', 'formatVersion', 'StoreV2', 'JournalV2']) {
    const hits = scanSchemaVersionInStoreContext(`let x = ${token}`, 'Domain/Bad.fs')
    assert.ok(hits.some((h) => h.text.includes(token)), `expected hit for ${token}`)
  }
  assert.ok(
    scanSchemaVersionInStoreContext('let p = "/events/v2/stream"', 'Domain/Bad.fs').length >= 1,
  )
  assert.ok(
    scanSchemaVersionInStoreContext('let r = "refs/wanxiang/store-v2"', 'Domain/Bad.fs').length >= 1,
  )
})

test('canonical refs/wanxiang/store is allowed only under Persist/Git ownership', () => {
  const source = 'let storeRef = "refs/wanxiang/store"'
  assert.equal(
    scanFeatureRef(source, 'Infrastructure/Persist/GitRawStore.fs').length,
    0,
  )
  assert.equal(scanFeatureRef(source, 'Infrastructure/Git/GitGateway.fs').length, 0)
  const red = scanFeatureRef(source, 'Domain/Casebook.fs')
  assert.ok(red.length >= 1)
})

test('owner remote-tracking store ref is allowed; other feature refs stay RED', () => {
  const remote = 'let r = "refs/wanxiang/remotes/origin/store"'
  assert.equal(
    scanFeatureRef(remote, 'Infrastructure/Persist/StoreTypes.fs').length,
    0,
  )
  assert.equal(scanFeatureRef(remote, 'Infrastructure/Git/GitGateway.fs').length, 0)
  assert.ok(scanFeatureRef(remote, 'Domain/Casebook.fs').length >= 1)

  const feature = 'let r = "refs/wanxiang/foo"'
  assert.ok(
    scanFeatureRef(feature, 'Infrastructure/Persist/StoreTypes.fs').length >= 1,
    'non-store refs/wanxiang/* remain RED even under Persist/Git ownership',
  )
  assert.ok(scanFeatureRef(feature, 'Domain/Casebook.fs').length >= 1)
})

test('git-bypass allowlist is empty; only Persist/Git ownership may invoke git', () => {
  assert.deepEqual([...GIT_BYPASS_ALLOWLIST], [])
  const source = 'let c = { FileName = "git"; Arguments = [] }'
  assert.equal(scanGitBypass(source, 'src/Wanxiangshu/Git/Subject.fs').length, 0)
  assert.equal(scanGitBypass(source, 'src/Wanxiangshu/Infrastructure/Persist/GitRawStore.fs').length, 0)
  assert.ok(scanGitBypass(source, 'src/Wanxiangshu/Domain/Sneaky.fs').length >= 1)
  assert.ok(scanGitBypass(source, 'src/Wanxiangshu/Journal/RuntimePath.fs').length >= 1)
})

test('dual-write allowlist is empty (no parked bridges)', () => {
  assert.deepEqual([...DUAL_WRITE_ALLOWLIST], [])
})

test('e2e journal observers that only read wanxiangshu-next are not no-migrator', () => {
  const observer = [
    "const dir = path.join(common, 'wanxiangshu-next', 'runtimes')",
    "const text = fs.readFileSync(path.join(dir, runtimeId + '.ndjson'), 'utf8')",
    'assert.ok(text.includes("LifeOpened"))',
  ].join('\n')
  assert.equal(
    scanNoMigrator(observer, 'tests/e2e/cases/reviewer-verdict.test.mjs').length,
    0,
    'live Journal observation is not a legacy migrator',
  )
})

test('production scan is GREEN under gate rules (empty git-bypass allowlist)', () => {
  const entries = collectProductionEntries()
  assert.ok(entries.length > 0, 'expected production .fs files')
  const violations = scanFiles(entries)
  assert.deepEqual(
    violations,
    [],
    violations.map((v) => `[${v.id}] ${v.file}:${v.line} ${v.label}`).join('\n'),
  )
})

test('documented non-store schemaVersion sites remain unflagged in production text', () => {
  // Informational contract: these files may mention schemaVersion but must not trip the gate.
  assert.ok(NON_STORE_SCHEMA_VERSION_SITES.length >= 1)
  const entries = collectProductionEntries().filter((e) =>
    NON_STORE_SCHEMA_VERSION_SITES.some((rel) => e.file.endsWith(rel)),
  )
  assert.equal(entries.length, NON_STORE_SCHEMA_VERSION_SITES.length)
  for (const entry of entries) {
    const hits = scanText(entry.text, entry.file).filter(
      (h) => h.id === 'schema-version-in-store-context',
    )
    assert.equal(hits.length, 0, entry.file)
  }
})
