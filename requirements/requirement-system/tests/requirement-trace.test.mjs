// requirement-trace.test.mjs — REQUIREMENT-SYSTEM-018 的机器落点（自举包）。
//
// 本文件自身使用 WHAT[REQUIREMENT-SYSTEM-018] 标签（018 规范要求测试显式声明
// 恰一个 primary WHAT）；每个 test 只回答一个问题。

import assert from 'node:assert/strict'
import { mkdirSync, mkdtempSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { dirname, join, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'

import { buildTraceGraph, packageOf, scanTestSource, whatHeadings } from '../../../scripts/lib/requirement-trace.mjs'

const ROOT = resolve(dirname(fileURLToPath(import.meta.url)), '../../..')

test('WHAT[REQUIREMENT-SYSTEM-018] scanner skips strings, comments, and template literals', () => {
  const src = [
    "// test('commented WHAT[A-001] must not fire')",
    "const s = \"test('string WHAT[A-002] must not fire')\"",
    'const t = `test(`template WHAT[A-003] must not fire`)`',
    "test('WHAT[A-004] real call fires')",
  ].join('\n')
  const calls = scanTestSource('<virtual>', src)
  assert.equal(calls.length, 1)
  assert.equal(calls[0].title, 'WHAT[A-004] real call fires')
  assert.deepEqual(calls[0].whatIds, ['A-004'])
  assert.equal(calls[0].state, 'active')
})

test('WHAT[REQUIREMENT-SYSTEM-018] scanner recognizes test.skip / test.todo / t.test forms', () => {
  const src = [
    "test.skip('WHAT[B-001] skipped still carries a tag', () => {})",
    "test.todo('WHAT[B-002] todo is not proof', () => {})",
    "t.test('WHAT[B-003] nested counts', () => {})",
    "t.test.skip('WHAT[B-004] nested skip counts', () => {})",
    "t.test.todo('WHAT[B-005] nested todo counts', () => {})",
    "t.test.fails('WHAT[B-006] nested fails remains executable', () => {})",
    "test.only('WHAT[B-007] only remains executable', () => {})",
  ].join('\n')
  const calls = scanTestSource('<virtual>', src)
  assert.deepEqual(
    calls.map((c) => [c.title, c.state]),
    [
      ['WHAT[B-001] skipped still carries a tag', 'skip'],
      ['WHAT[B-002] todo is not proof', 'todo'],
      ['WHAT[B-003] nested counts', 'active'],
      ['WHAT[B-004] nested skip counts', 'skip'],
      ['WHAT[B-005] nested todo counts', 'todo'],
      ['WHAT[B-006] nested fails remains executable', 'active'],
      ['WHAT[B-007] only remains executable', 'active'],
    ],
  )
})

test('WHAT[REQUIREMENT-SYSTEM-018] template titles with ${} nesting are parsed', () => {
  const src = [
    "const bad = ['x', 'y']",
    'for (const b of bad) {',
    '  test(`WHAT[C-001] rejects ${b}`, () => {})',
    '}',
  ].join('\n')
  const calls = scanTestSource('<virtual>', src)
  assert.equal(calls.length, 1)
  assert.equal(calls[0].dynamic, true)
  assert.deepEqual(calls[0].whatIds, ['C-001'])
})

test('WHAT[REQUIREMENT-SYSTEM-018] scanner rejects duplicate, non-leading, and missing primary tags', () => {
  const src = [
    "test('WHAT[E-001] one WHAT[E-001] duplicate', () => {})",
    "test('prose WHAT[E-002] is not a declaration', () => {})",
    'test(dynamicTitle, () => {})',
    "test.beforeEach('WHAT[E-003] hook is not a test case', () => {})",
    "it('WHAT[E-004] alias is outside the test()/t.test() contract', () => {})",
  ].join('\n')
  const calls = scanTestSource('<virtual>', src)
  assert.deepEqual(
    calls.map((call) => [call.title, call.whatIds]),
    [
      ['WHAT[E-001] one WHAT[E-001] duplicate', ['E-001', 'E-001']],
      ['prose WHAT[E-002] is not a declaration', []],
      [null, []],
    ],
  )
})

test('WHAT[REQUIREMENT-SYSTEM-018] scanner ignores declarations, constructors, and methods named test', () => {
  const src = [
    "function test('WHAT[G-001] declaration is not a call site') {}",
    "new test('WHAT[G-002] constructor is not a proof case')",
    'class Fixture { test(title) {} }',
    "test('WHAT[G-003] actual call remains visible', () => {})",
  ].join('\n')
  const calls = scanTestSource('<virtual>', src)
  assert.deepEqual(calls.map((call) => call.title), ['WHAT[G-003] actual call remains visible'])
})

test('WHAT[REQUIREMENT-SYSTEM-018] scanner sees nested test calls in template expressions', () => {
  const src = [
    'test(`WHAT[F-001] outer ${t.test(\'WHAT[F-002] inner\', () => {})}`, () => {})',
    "const notCode = /t\\.test\\('WHAT[F-003] regex'\\)/",
    "/* t.test('WHAT[F-004] block comment') */",
  ].join('\n')
  const calls = scanTestSource('<virtual>', src)
  assert.deepEqual(
    calls.map((call) => [call.title, call.whatIds, call.state]),
    [
      ['WHAT[F-001] outer ${}', ['F-001'], 'active'],
      ['WHAT[F-002] inner', ['F-002'], 'active'],
    ],
  )
})

test('WHAT[REQUIREMENT-SYSTEM-018] whatHeadings extracts PREFIX-NNN with title and line', () => {
  const md = [
    '# WHAT',
    '',
    '## A-001：第一条',
    '## A-002：第二条',
  ].join('\n')
  assert.deepEqual(whatHeadings(md), [
    { id: 'A-001', title: '第一条', line: 3 },
    { id: 'A-002', title: '第二条', line: 4 },
  ])
})

test('WHAT[REQUIREMENT-SYSTEM-018] scanner skips regex literals containing quotes', () => {
  const src = [
    "const re = /join\\(root,\\s*'checks\\/([^']+)'\\)/g",
    "test('WHAT[D-001] call after a regex literal still fires', () => {})",
    'const division = 6 / 3',
    "test('WHAT[D-002] division does not confuse the scanner', () => {})",
  ].join('\n')
  const calls = scanTestSource('<virtual>', src)
  assert.deepEqual(
    calls.map((c) => c.title),
    [
      'WHAT[D-001] call after a regex literal still fires',
      'WHAT[D-002] division does not confuse the scanner',
    ],
  )
})

test('WHAT[REQUIREMENT-SYSTEM-018] graph closes exact proof anchors and rejects stale anchors', () => {
  const root = mkdtempSync(join(tmpdir(), 'requirement-trace-'))
  const requirements = join(root, 'requirements')
  const packageRoot = join(requirements, 'fixture-package')
  const tests = join(packageRoot, 'tests')
  const testFile = join(tests, 'case.test.mjs')
  const whatFile = join(packageRoot, 'WHAT.md')
  const proofFile = join(packageRoot, 'HOW.md')
  mkdirSync(packageRoot, { recursive: true })
  mkdirSync(tests, { recursive: true })
  try {
    writeFileSync(whatFile, '# WHAT\n\n## FIXTURE-PACKAGE-001：contract\n')
    writeFileSync(testFile, "test('WHAT[FIXTURE-PACKAGE-001] exact anchor', () => {})\n")
    writeFileSync(proofFile, '| 命题 | 落点 |\n|---|---|\n| FIXTURE-PACKAGE-001 | `tests/case.test.mjs::WHAT[FIXTURE-PACKAGE-001] exact anchor` |\n')
    const closed = buildTraceGraph(requirements)
    assert.equal(closed.danglingProof.length, 0)
    assert.equal(closed.proofEdges.length, 1)
    assert.deepEqual(
      {
        line: closed.proofEdges[0].line,
        title: closed.proofEdges[0].title,
        state: closed.proofEdges[0].state,
        whatId: closed.proofEdges[0].whatId,
      },
      { line: 1, title: 'WHAT[FIXTURE-PACKAGE-001] exact anchor', state: 'active', whatId: 'FIXTURE-PACKAGE-001' },
    )

    writeFileSync(proofFile, '| 命题 | 落点 |\n|---|---|\n| FIXTURE-PACKAGE-001 | `tests/case.test.mjs::WHAT[FIXTURE-PACKAGE-001] removed anchor` |\n')
    const dangling = buildTraceGraph(requirements)
    assert.equal(dangling.danglingProof.length, 1)
    assert.equal(dangling.danglingProof[0].state, 'dangling')
  } finally {
    rmSync(root, { recursive: true, force: true })
  }
})


test('WHAT[REQUIREMENT-SYSTEM-018] buildTraceGraph classifies orphan / unknown / multi-primary / unproved', () => {
  const graph = buildTraceGraph(ROOT)
  assert.ok(graph.whats.size > 0, 'requirements tree must define WHAT propositions')
  assert.ok(graph.tests.length > 0, 'requirements tree must contain tests')
  for (const t of graph.tests) {
    for (const id of t.whatIds) {
      assert.match(id, /^[A-Z][A-Z0-9-]*-\d{3}$/, `tag ${id} must be a well-formed WHAT ID`)
    }
  }
  // multi-primary is only ever produced by the scanner, never by this suite.
  assert.equal(graph.multiPrimary.length, 0)
})

test('WHAT[REQUIREMENT-SYSTEM-018] graph rejects prose-only proof rows with no executable test anchor', () => {
  const root = mkdtempSync(join(tmpdir(), 'requirement-trace-prose-'))
  const requirements = join(root, 'requirements')
  const packageRoot = join(requirements, 'fixture-package')
  const tests = join(packageRoot, 'tests')
  const testFile = join(tests, 'case.test.mjs')
  const whatFile = join(packageRoot, 'WHAT.md')
  const proofFile = join(packageRoot, 'HOW.md')
  mkdirSync(packageRoot, { recursive: true })
  mkdirSync(tests, { recursive: true })
  try {
    writeFileSync(whatFile, '# WHAT\n\n## FIXTURE-PACKAGE-001：contract\n\n## FIXTURE-PACKAGE-002：untested\n')
    writeFileSync(testFile, "test('WHAT[FIXTURE-PACKAGE-001] exact anchor', () => {})\n")
    // Row 1 has a test path → valid proof. Row 2 has only a law ID + prose,
    // no .test.mjs path → must be flagged as prose-only.
    writeFileSync(proofFile, [
      '| 命题 | 落点 |',
      '|---|---|',
      '| FIXTURE-PACKAGE-001 | `tests/case.test.mjs::WHAT[FIXTURE-PACKAGE-001] exact anchor` |',
      '| FIXTURE-PACKAGE-002 | prose narrative with no test path |',
    ].join('\n') + '\n')

    const graph = buildTraceGraph(requirements)
    assert.equal(graph.proseOnlyProof.length, 1, 'exactly one prose-only proof row')
    assert.equal(graph.proseOnlyProof[0].whatIds[0], 'FIXTURE-PACKAGE-002')
    assert.match(graph.proseOnlyProof[0].rowText, /prose narrative with no test path/)
    // The valid row must still produce a proof edge.
    assert.equal(graph.proofEdges.length, 1)
    assert.equal(graph.proofEdges[0].whatId, 'FIXTURE-PACKAGE-001')
  } finally {
    rmSync(root, { recursive: true, force: true })
  }
})

test('WHAT[REQUIREMENT-SYSTEM-018] packageOf resolves the owning package, not a tests/eval directory', () => {
  assert.equal(packageOf('requirements/office-capability/tests/eval/provider-office-boundary/office-boundary-eval.test.mjs'), 'office-capability')
  assert.equal(packageOf('requirements/behavior-diagnosis/tests/paired-history-eval.test.mjs'), 'behavior-diagnosis')
  assert.equal(packageOf('requirements/verification-system/tests/run.mjs'), 'verification-system')
  assert.equal(packageOf('scripts/checks/requirement-trace.mjs'), null)
})
