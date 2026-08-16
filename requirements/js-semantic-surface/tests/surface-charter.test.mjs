// requirements/js-semantic-surface/tests/surface-charter.test.mjs
//
// JS-SEMANTIC-SURFACE-001..006 的可静态判定机器面：
//
//   001 所有 automated tests 是 JS（.test.mjs 是唯一语义测试载体）
//   002 语义测试只经正式 surface；forbidden patterns 的 machine 面 =
//       js-boundary-gate 已 wire 进 check.mjs（ratchet 只减不增）
//   003 law → owner → surface 归属被文档化（本包 PROOF 表 + 六条 WHAT）
//   004 helper 不直接测试：representation validator 的 subject 是规则本身，
//       不测任何 domain helper
//   005 JS-native representation 规则由 js-contract validator 承载并在这里
//       直接可红：FSharpList/DU/Date/reflection 形状被拒，JSON 通过
//   006 Fable 形状不是 contract：quarantine（guide-contract/domain.meta）
//       存在且与 semantic 测试分开；baseline 只减不增由 gate 承接
//
// 本包是 META 包，测试不 import dist、不使用 interop helpers——自身即宪法
// 002/005/006 的 canary。

import assert from 'node:assert/strict'
import { existsSync, readFileSync, readdirSync, statSync } from 'node:fs'
import { dirname, join, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'
import { assertJsData, assertOpaque, isJsData } from '../../verification-system/tests/support/js-contract.mjs'
import { walk } from '../../../scripts/lib/walk.mjs'
import { scanAll, SURFACE_MANIFEST } from '../../../scripts/lib/test-surface-scan.mjs'

const ROOT = resolve(dirname(fileURLToPath(import.meta.url)), '../../..')

const read = (p) => readFileSync(join(ROOT, p), 'utf8')

/** Resolve a relative import target inside a test file to a ROOT-relative path. */
const normalizeImportTarget = (relFile, target) => {
  const abs = resolve(dirname(join(ROOT, relFile)), target)
  return abs.replace(ROOT + '/', '').replace(/\\/g, '/')
}

// ── 001: 所有 automated tests 使用 JavaScript ───────────────────────────────

test('WHAT[JS-SEMANTIC-SURFACE-001] JS_SURFACE_001_all_semantic_tests_are_mjs', () => {
  const testFiles = walk(join(ROOT, 'requirements'), ['.test.mjs', '.test.js', '.test.fs', '.test.ts', '.test.fsx'])
  const nonMjs = testFiles.filter((f) => !f.endsWith('.test.mjs'))
  assert.deepEqual(
    nonMjs.map((f) => f.replace(ROOT + '/', '')),
    [],
    'every automated semantic test must be a .mjs file',
  )
})

// ── 002: 只经正式 semantic surface；gate 已 wire ────────────────────────────

test('WHAT[JS-SEMANTIC-SURFACE-002] JS_SURFACE_002_forbidden_patterns_absent_from_semantic_tests', () => {
  // PR 5 (TASK.md §11): a property check, not a source-text check.
  // debt.minusApprovedBaseline() must be empty: every forbidden pattern in
  // the whole semantic-test zone must be pre-approved by the only-shrink
  // baseline. Beyond the approved baseline, forbidden patterns ARE absent —
  // this is exactly what the gate enforces, asserted here independently.
  const all = scanAll()
  const baseline = JSON.parse(read('scripts/checks/js-boundary-baseline.json'))
  const excess = []
  for (const [file, hits] of Object.entries(all)) {
    const allowed = baseline[file] ?? {}
    const byRule = {}
    for (const h of hits) byRule[h.rule] = (byRule[h.rule] ?? 0) + 1
    for (const [rule, count] of Object.entries(byRule)) {
      if (count > (allowed[rule] ?? 0)) excess.push(`${file}: ${rule} ${allowed[rule] ?? 0} -> ${count}`)
    }
  }
  for (const file of Object.keys(all)) {
    if (!(file in baseline)) excess.push(`${file}: NEW debt`)
  }
  assert.deepEqual(excess, [], `forbidden patterns beyond the approved baseline must be absent; excess: ${excess.join('; ')}`)

  // The gate remains the wired execution carrier of the ratchet.
  const checkSource = read('scripts/check.mjs')
  assert.match(checkSource, /checks\/js-boundary-gate\.mjs/, 'js-boundary-gate must be wired into check.mjs')
})

// ── 002 zone: 整个 semantic-test zone 都被扫描（TASK.md §6/§7/§8）────────────

test('WHAT[JS-SEMANTIC-SURFACE-002] JS_SURFACE_002c_whole_semantic_test_zone_is_scanned', () => {
  // The scanner must see support/fixtures/helpers/*-contract.mjs, not only
  // *.test.mjs — moving forbidden knowledge from a test file into test
  // support does not reduce debt. The zone fixture below deliberately
  // deep-imports dist and reads .fields; it must be visible to the scanner
  // and carry debt. If it ever turns green, the gate is theater.
  const scannerSource = read('scripts/lib/test-surface-scan.mjs')

  assert.match(
    scannerSource,
    /semanticTestFiles =[\s\S]*?walk\(root, \['\.mjs'\]\)/,
    'scanner must walk every .mjs in the semantic-test zone, not only *.test.mjs',
  )

  const fixtureRel = 'requirements/js-semantic-surface/tests/fixtures/zone-debt.mjs'
  assert.ok(existsSync(join(ROOT, fixtureRel)), `zone-debt fixture must exist: ${fixtureRel}`)

  const fixture = read(fixtureRel)
  assert.match(fixture, /dist\//, 'zone-debt fixture must deep-import dist (deliberate violation)')
  assert.match(fixture, /\.fields\b/, 'zone-debt fixture must read .fields (deliberate violation)')

  const all = scanAll()
  const hits = all[fixtureRel]
  assert.ok(hits && hits.length > 0, `zone-debt fixture must be visible to the scanner with debt, got ${JSON.stringify(all[fixtureRel])}`)
  const rules = hits.map((h) => h.rule)
  assert.ok(rules.includes('deep-dist-import'), `zone fixture must carry deep-dist-import, got ${rules.join(',')}`)
  assert.ok(rules.includes('du-shape'), `zone fixture must carry du-shape, got ${rules.join(',')}`)
})

// ── 003: law → owner → surface 归属被文档化 ─────────────────────────────────

test('WHAT[JS-SEMANTIC-SURFACE-003] JS_SURFACE_003_law_owner_surface_registry', () => {
  const what = read('requirements/js-semantic-surface/WHAT.md')
  for (const id of ['001', '002', '003', '004', '005', '006']) {
    assert.match(what, new RegExp(`^## JS-SEMANTIC-SURFACE-${id}：`, 'm'), `WHAT must define JS-SEMANTIC-SURFACE-${id}`)
  }
  const proof = read('requirements/js-semantic-surface/PROOF.md')
  for (const id of ['001', '002', '003', '004', '005', '006']) {
    assert.match(
      proof,
      new RegExp(`JS-SEMANTIC-SURFACE-${id}\\b`),
      `PROOF must carry a landing row for JS-SEMANTIC-SURFACE-${id}`,
    )
  }
})

// ── 003 manifest: 每个注册 surface 的 owner/laws/source 必须真实存在 ──────────

test('WHAT[JS-SEMANTIC-SURFACE-003] JS_SURFACE_003_manifest_binds_law_owner_source', () => {
  // PR 4 (TASK.md §10): registration is a manifest, not a string allowlist.
  // The gate must mechanically prove: owner package exists, every law exists
  // in that owner's WHAT.md, the production source file exists, and the
  // surface is imported by its contract test.
  const manifest = SURFACE_MANIFEST
  assert.ok(manifest.length >= 2, `expected at least the two pilots registered, found ${manifest.length}`)

  const requirementsRoot = join(ROOT, 'requirements')
  const testFiles = walk(requirementsRoot, ['.test.mjs']).filter((f) => !f.includes('/e2e/') && !f.includes('/integration/'))
  const allSources = testFiles.map((f) => readFileSync(f, 'utf8'))
  const proof = read('requirements/js-semantic-surface/PROOF.md')

  for (const entry of manifest) {
    const label = entry.module
    // Owner package must exist with a WHAT.md.
    assert.ok(existsSync(join(requirementsRoot, entry.owner, 'WHAT.md')), `${label}: owner package ${entry.owner} must exist`)
    const ownerWhat = readFileSync(join(requirementsRoot, entry.owner, 'WHAT.md'), 'utf8')
    const ownerProof = readFileSync(join(requirementsRoot, entry.owner, 'PROOF.md'), 'utf8')
    // Every governing law must exist in the owner's WHAT.md and have a
    // landing row in the owner's PROOF.md.
    for (const law of entry.laws) {
      assert.match(ownerWhat, new RegExp(`^## ${law}[:：]`, 'm'), `${label}: law ${law} must exist in ${entry.owner} WHAT.md`)
      assert.match(ownerProof, new RegExp(`${law}\\b`), `${label}: ${entry.owner} PROOF must carry a landing row for ${law}`)
    }
    // Production source must exist.
    assert.ok(existsSync(join(ROOT, entry.source)), `${label}: production source ${entry.source} must exist`)
    // Representation must be a declared kind.
    assert.ok(['json', 'opaque-capability'].includes(entry.representation), `${label}: representation must be json or opaque-capability`)
    assert.ok(['pure', 'resource'].includes(entry.kind), `${label}: kind must be pure or resource`)
    // A contract test must import the surface.
    const imported = allSources.some((src) => src.includes(`dist/${entry.module}`))
    assert.equal(
      imported,
      true,
      `${label}: no contract test imports it — registration without a contract test is a test-convenience entry, not a surface`,
    )
  }
})

// ── 004: helper 不直接测试 ──────────────────────────────────────────────────

test('WHAT[JS-SEMANTIC-SURFACE-004] JS_SURFACE_004_helper_not_directly_tested', () => {
  // PR 5 (TASK.md §13): the corpus, not this package's own tests.
  //
  // Legal import targets for a .test.mjs:
  //   - a registered surface (SURFACE_MANIFEST, 002/003);
  //   - the transition facade verification-system/tests/support/domain.mjs —
  //     the migration carrier approved by WHAT 002 boundary, not a helper
  //     being pinned;
  //   - the representation validator js-contract.mjs (its subject IS the
  //     rules);
  //   - a debt-free zone file (pure fixture data, no authority);
  //   - a zone file carrying debt ONLY if that debt is pre-approved by the
  //     js-boundary baseline (grandfathered contract adapters — they may only
  //     shrink; a NEW debt-bearing support file imported by a test is RED).
  const zoneFiles = walk(join(ROOT, 'requirements'), ['.mjs'])
    .filter((f) => !f.includes('/tests/e2e/') && !f.includes('/tests/integration/'))
    .map((f) => f.replace(ROOT + '/', '').replace(/\\/g, '/'))

  const testFiles = zoneFiles.filter((f) => f.includes('/tests/') && f.endsWith('.test.mjs'))
  const helperFiles = new Set(zoneFiles.filter((f) => f.includes('/tests/') && !f.endsWith('.test.mjs')))
  const baseline = JSON.parse(read('scripts/checks/js-boundary-baseline.json'))
  const scan = scanAll()

  const violations = []
  for (const file of testFiles) {
    const text = read(file)
    const re = /from\s+['"](\.[^'"]+)['"]/g
    let m
    while ((m = re.exec(text)) !== null) {
      const target = normalizeImportTarget(file, m[1])
      if (!helperFiles.has(target)) continue
      if (target.endsWith('verification-system/tests/support/domain.mjs')) continue // transition facade (WHAT 002)
      if (target.endsWith('verification-system/tests/support/js-contract.mjs')) continue // validator subject
      const debt = scan[target] ?? []
      if (debt.length === 0) continue // pure fixture data, no authority
      if (target in baseline) continue // grandfathered debt, only-shrink ratchet
      violations.push(`${file} imports debt-bearing helper ${target} — helper not directly tested (JS-SEMANTIC-SURFACE-004)`)
    }
  }
  assert.deepEqual(violations, [], violations.join('\n'))

  // The charter package itself must not import the transition facade or use
  // interop helpers (the old self-check remains as the local instance).
  const selfTests = walk(join(ROOT, 'requirements/js-semantic-surface/tests'), ['.mjs']).filter((f) =>
    f.endsWith('.test.mjs'),
  )
  for (const file of selfTests) {
    const text = readFileSync(file, 'utf8')
    assert.doesNotMatch(text, /from\s+['"][^'"]*support\/domain/, 'charter tests must not import the transition facade')
    assert.doesNotMatch(text, /\b(toList|caseOf|payloadOf|resultOf|unwrapOption)\(/, 'charter tests must not use interop helpers')
  }
})

// ── 005: JS-native representation 规则直接可红 ───────────────────────────────

test('WHAT[JS-SEMANTIC-SURFACE-005] JS_SURFACE_005_js_native_representation_rules', () => {
  // JSON-shaped passes.
  assert.equal(isJsData(null), true)
  assert.equal(isJsData('s'), true)
  assert.equal(isJsData(42), true)
  assert.equal(isJsData(true), true)
  assert.equal(isJsData(10n), true)
  assert.equal(isJsData([]), true)
  assert.equal(isJsData({ a: [1, { b: 'c' }] }), true)

  // F# DU instance shapes are rejected: cases() on constructor, tag+fields pair.
  const duInstance = { tag: 0, fields: ['x'] }
  assert.equal(isJsData(duInstance), false)
  const duCtor = { cases: () => ['A', 'B'] }
  assert.equal(isJsData(duCtor), false)

  // FSharpList head/tail shape is rejected.
  assert.equal(isJsData({ head: 1, tail: null }), false)

  // Fable reflection metadata is rejected.
  assert.equal(isJsData({ $reflection: {}, value: 1 }), false)

  // Bare Date is rejected: the time boundary is ISO-8601 string / epoch ms.
  assert.equal(isJsData(new Date()), false)

  // FSharpMap / FSharpSet / record runtime class instances are rejected:
  // they are class instances, not plain objects (functions as values are
  // legal JS-native; class identity is not).
  const fsharpMapLike = new (class {
    constructor() {
      this.size = 0
    }
    entries() {
      return []
    }
  })()
  assert.equal(isJsData(fsharpMapLike), false)

  // assertJsData throws on leaked representation.
  assert.throws(() => assertJsData({ tag: 1, fields: [] }), /JS-native/)
  assert.equal(assertJsData({ ok: true, value: [1, 2] }).ok, true)

  // Opaque handles: object/function pass, primitives rejected.
  assert.equal(assertOpaque({}, 'h') !== undefined, true)
  assert.equal(assertOpaque(() => {}, 'f') !== undefined, true)
  assert.throws(() => assertOpaque('s'), /opaque/)
  assert.throws(() => assertOpaque(1), /opaque/)
})

// ── 006: Fable 形状不是 contract；quarantine 单独存在 ───────────────────────

test('WHAT[JS-SEMANTIC-SURFACE-006] JS_SURFACE_006_fable_representation_not_contract', () => {
  // PR 5 (TASK.md §14): a property check, not a "this file must exist" check.
  // The terminal state must allow domain.meta.test.mjs and the debt baseline
  // to disappear — their removal is a VICTORY, not a charter failure. What
  // must hold forever:
  //   (1) every build-verification exemption names a real file (no ghost
  //       exemptions that silently forgive debt);
  //   (2) every exemption is entitled to know Fable: its subject IS the
  //       emitted artifact or the forbidden shapes themselves (it spells out
  //       dist / fable_modules / DU-shape tokens), so a plain semantic test
  //       cannot be smuggled into the allowlist;
  //   (3) no exemption lives in a product package's semantic-test zone —
  //       quarantine files sit in verification-system or are the charter /
  //       validator themselves.
  const scannerSource = read('scripts/lib/test-surface-scan.mjs')
  const block = scannerSource.match(/BUILD_VERIFICATION_FILES = new Set\(\[[\s\S]*?\]\)/)
  assert.ok(block, 'scanner must declare BUILD_VERIFICATION_FILES')
  const exempt = [...block[0].matchAll(/'([^']+)'/g)].map((m) => m[1])
  assert.ok(exempt.length >= 1, 'build-verification quarantine must name at least one file')

  for (const file of exempt) {
    // (1) No ghost exemptions.
    assert.ok(existsSync(join(ROOT, file)), `build-verification exemption ${file} must exist — ghost exemption silently forgives debt`)
    // (2) Entitlement: the file must actually know Fable shapes or dist.
    const text = read(file)
    const knowsFable = /fable_modules|\.fields\b|\.tag\b|FSharp|dist\//.test(text)
    assert.equal(
      knowsFable,
      true,
      `build-verification exemption ${file} carries no Fable/dist knowledge — a semantic test must not be in the quarantine allowlist`,
    )
    // (3) Location: quarantine files sit in verification-system, the
    // distribution artifact oracle (TASK.md §1.E distribution artifact
    // tests — their subject IS the packed artifact), or the charter /
    // validator themselves. A product package's semantic tests never
    // qualify (process-shared-routing was removed on exactly this ground).
    const inProductZone =
      file.startsWith('requirements/') &&
      !file.startsWith('requirements/verification-system/') &&
      !file.startsWith('requirements/js-semantic-surface/') &&
      !file.startsWith('requirements/distribution/')
    assert.equal(inProductZone, false, `exemption ${file} lives in a product package — quarantine is compiler/build verification only`)
  }
})

test('WHAT[JS-SEMANTIC-SURFACE-003] JS_SURFACE_003_every_registered_surface_has_a_contract_test', () => {
  // A registered surface is a legal entry point ONLY because a contract test
  // pins it (JS-SEMANTIC-SURFACE-003: surface exists because a component owns
  // a contract, never because a test wants access). Every manifest entry must
  // be imported by at least one semantic test file.
  const registered = SURFACE_MANIFEST.map((entry) => entry.module)
  assert.ok(registered.length >= 2, `expected the two pilots registered, found ${registered.length}`)

  const testsRoot = join(ROOT, 'requirements')
  const testFiles = walk(testsRoot, ['.test.mjs']).filter((f) => !f.includes('/e2e/') && !f.includes('/integration/'))
  const allSources = testFiles.map((f) => readFileSync(f, 'utf8'))

  for (const modulePath of registered) {
    const imported = allSources.some((src) => src.includes(`dist/${modulePath}`))
    assert.equal(
      imported,
      true,
      `registered surface ${modulePath} has no contract test importing it — registration without a contract test is a test-convenience entry, not a surface`,
    )
  }
})

test('WHAT[JS-SEMANTIC-SURFACE-002] JS_SURFACE_002b_registered_surfaces_exist_in_the_production_source_tree', () => {
  // Allowlist cannot grant immunity to a path that does not exist: every
  // registered surface must map to a real <Compile Include> entry in
  // Wanxiangshu.fsproj. A bogus entry is a silent debt exemption.
  // (The check is intentionally a file-path match, not a module-name match:
  // ToolResultBound.js legitimately lives in the Host.Contract module.)
  const fsproj = read('src/Wanxiangshu/Wanxiangshu.fsproj')

  for (const entry of SURFACE_MANIFEST) {
    const stem = entry.module.replace(/\.js$/, '')
    const exact = `<Compile Include="${stem}.fs"/>`
    assert.ok(
      fsproj.includes(exact),
      `registered surface ${entry.module} has no <Compile Include="${stem}.fs"/> in Wanxiangshu.fsproj — allowlist entry must name a real surface module`,
    )
  }
})
