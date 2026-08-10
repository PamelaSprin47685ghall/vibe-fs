/**
 * Unified-store Phase 1 gate: fixtures RED + production GREEN (empty git-bypass allowlist).
 */
import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import test from 'node:test'
import {
  GIT_BYPASS_ALLOWLIST,
  NON_STORE_SCHEMA_VERSION_SITES,
  SCANNER_IDS,
  collectProductionEntries,
  scanFeatureRef,
  scanFiles,
  scanGitBypass,
  scanSchemaVersionInStoreContext,
  scanText,
} from '../../../scripts/checks/unified-store-gate.mjs'

const readFixture = (name) =>
  readFileSync(new URL(`./fixtures/${name}`, import.meta.url), 'utf8')

test('scanner ids cover the three Phase 1 rules', () => {
  assert.deepEqual([...SCANNER_IDS], [
    'feature-ref',
    'schema-version-in-store-context',
    'git-bypass',
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
  assert.equal(scanGitBypass(source, 'src/Wanxiangshu/Infrastructure/Git/GitSubject.fs').length, 0)
  assert.equal(scanGitBypass(source, 'src/Wanxiangshu/Infrastructure/Persist/GitRawStore.fs').length, 0)
  assert.ok(scanGitBypass(source, 'src/Wanxiangshu/Domain/Sneaky.fs').length >= 1)
  assert.ok(scanGitBypass(source, 'src/Wanxiangshu/Journal/RuntimePath.fs').length >= 1)
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
