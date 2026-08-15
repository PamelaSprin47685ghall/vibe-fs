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

const ROOT = resolve(dirname(fileURLToPath(import.meta.url)), '../../..')

const read = (p) => readFileSync(join(ROOT, p), 'utf8')

// ── 001: 所有 automated tests 使用 JavaScript ───────────────────────────────

test('JS_SURFACE_001_all_semantic_tests_are_mjs', () => {
  const testFiles = walk(join(ROOT, 'requirements'), ['.test.mjs', '.test.js', '.test.fs', '.test.ts', '.test.fsx'])
  const nonMjs = testFiles.filter((f) => !f.endsWith('.test.mjs'))
  assert.deepEqual(
    nonMjs.map((f) => f.replace(ROOT + '/', '')),
    [],
    'every automated semantic test must be a .mjs file',
  )
})

// ── 002: 只经正式 semantic surface；gate 已 wire ────────────────────────────

test('JS_SURFACE_002_forbidden_patterns_absent_from_semantic_tests', () => {
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

// ── 003: law → owner → surface 归属被文档化 ─────────────────────────────────

test('JS_SURFACE_003_law_owner_surface_registry', () => {
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

// ── 004: helper 不直接测试 ──────────────────────────────────────────────────

test('JS_SURFACE_004_helper_not_directly_tested', () => {
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

test('JS_SURFACE_005_js_native_representation_rules', () => {
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

test('JS_SURFACE_006_fable_representation_not_contract', () => {
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
