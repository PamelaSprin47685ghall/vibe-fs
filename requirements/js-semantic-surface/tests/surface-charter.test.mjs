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
  // Machine face: js-boundary-gate is wired into check.mjs (P2). A semantic
  // test carrying debt is red unless the baseline explicitly tolerates it.
  const checkSource = read('scripts/check.mjs')
  assert.match(checkSource, /checks\/js-boundary-gate\.mjs/, 'js-boundary-gate must be wired into check.mjs')

  const gateSource = read('scripts/checks/js-boundary-gate.mjs')
  assert.match(
    gateSource,
    /baseline can only shrink|baseline can be deleted|只减不增|can only shrink/,
    'gate contract must state the baseline only-shrink rule',
  )
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
  // The representation validator is tested through its own contract (005),
  // never through a domain helper. No test file in this package may name an
  // interop helper or import the transition facade.
  const selfTests = walk(join(ROOT, 'requirements/js-semantic-surface/tests'), ['.mjs']).filter((f) =>
    f.endsWith('.test.mjs'),
  )
  for (const file of selfTests) {
    const text = readFileSync(file, 'utf8')
    assert.doesNotMatch(text, /support\/domain/, 'charter tests must not import the transition facade')
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
  // Compiler/build verification quarantine exists and is separate from
  // semantic tests: it names Fable output shape because its subject IS the
  // compiled artifact (guide-contract emitted-surface pin, domain.meta facade
  // self-contract). Their presence is the machine face of "Fable knowledge
  // lives only in quarantine".
  assert.ok(existsSync(join(ROOT, 'requirements/verification-system/tests/guide-contract.test.mjs')), 'quarantine guide-contract must exist')
  assert.ok(existsSync(join(ROOT, 'requirements/verification-system/tests/domain.meta.test.mjs')), 'quarantine domain.meta must exist')
  assert.ok(existsSync(join(ROOT, 'scripts/checks/js-boundary-baseline.json')), 'debt baseline must exist (only-shrink ratchet)')

  // The baseline is a finite set — inventory and gate share one scanner, so a
  // debt line cannot hide from the gate.
  const gate = read('scripts/checks/js-boundary-gate.mjs')
  assert.match(gate, /test-surface-scan\.mjs/, 'gate must share the inventory scanner')
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
