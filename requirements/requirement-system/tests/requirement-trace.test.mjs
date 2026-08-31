// requirement-trace.test.mjs — REQUIREMENT-SYSTEM-018 的机器落点（自举包）。
//
// 本文件自身使用 WHAT[REQUIREMENT-SYSTEM-018] 标签（018 规范要求测试显式声明
// 恰一个 primary WHAT）；每个 test 只回答一个问题。

import assert from 'node:assert/strict'
import { mkdirSync, mkdtempSync, readFileSync, rmSync, symlinkSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { dirname, join, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'

import {
  buildTraceGraph,
  packageOf,
  resolveExactProofTitle,
  resolveProofLevel,
  scanTestSource,
  validateProofLevelRegistry,
  whatHeadings,
} from '../../../scripts/lib/requirement-trace.mjs'

const ROOT = resolve(dirname(fileURLToPath(import.meta.url)), '../../..')
const REQUIREMENTS = join(ROOT, 'requirements')

test('WHAT[REQUIREMENT-SYSTEM-018] scanner skips strings, comments, and template literals', () => {
  const src = [
    "import test from 'node:test'",
    "// test('commented WHAT[A-001] must not fire')",
    "const s = \"test('string WHAT[A-002] must not fire')\"",
    'const label = `test("template WHAT[A-003] must not fire")`',
    "test('WHAT[A-004] real call fires', () => {})",
  ].join('\n')
  const calls = scanTestSource('<virtual>', src)
  assert.equal(calls.length, 1)
  assert.equal(calls[0].title, 'WHAT[A-004] real call fires')
  assert.deepEqual(calls[0].whatIds, ['A-004'])
  assert.equal(calls[0].state, 'active')
})

test('WHAT[REQUIREMENT-SYSTEM-018] scanner recognizes supported root and bound context forms', () => {
  const src = [
    "import test from 'node:test'",
    "test.skip('WHAT[B-001] skipped still carries a tag', () => {})",
    "test.todo('WHAT[B-002] todo is not proof', () => {})",
    "test.only('WHAT[B-007] only remains executable', () => {})",
    "test('WHAT[B-003] parent', async (t) => {",
    "  await t.test('WHAT[B-004] nested counts', () => {})",
    '})',
  ].join('\n')
  const calls = scanTestSource('<virtual>', src)
  assert.deepEqual(
    calls.map((c) => [c.title, c.state]),
    [
      ['WHAT[B-001] skipped still carries a tag', 'skip'],
      ['WHAT[B-002] todo is not proof', 'todo'],
      ['WHAT[B-007] only remains executable', 'active'],
      ['WHAT[B-003] parent', 'active'],
      ['WHAT[B-004] nested counts', 'active'],
    ],
  )
})

test('WHAT[REQUIREMENT-SYSTEM-018] template titles with ${} nesting are parsed', () => {
  const src = [
    "import test from 'node:test'",
    "const bad = 'x'",
    'test(`WHAT[C-001] rejects ${bad}`, () => {})',
  ].join('\n')
  const calls = scanTestSource('<virtual>', src)
  assert.equal(calls.length, 1)
  assert.equal(calls[0].dynamic, true)
  assert.deepEqual(calls[0].whatIds, ['C-001'])
})

test('WHAT[REQUIREMENT-SYSTEM-018] scanner rejects duplicate, non-leading, and missing primary tags', () => {
  const src = [
    "import test from 'node:test'",
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
    "import test from 'node:test'",
    'class Fixture { test(title) {} }',
    "new Fixture().test('WHAT[G-001] method is not a proof case')",
    "test('WHAT[G-003] actual call remains visible', () => {})",
  ].join('\n')
  const calls = scanTestSource('<virtual>', src)
  assert.deepEqual(calls.map((call) => call.title), ['WHAT[G-003] actual call remains visible'])
})

test('WHAT[REQUIREMENT-SYSTEM-018] scanner sees a bound nested test and skips lexical decoys', () => {
  const src = [
    "import test from 'node:test'",
    "test('WHAT[F-001] outer', async (t) => {",
    "  await t.test('WHAT[F-002] inner', () => {})",
    '})',
    "const notCode = /t\\.test\\('WHAT\\[F-003\\] regex'\\)/",
    "/* t.test('WHAT[F-004] block comment') */",
  ].join('\n')
  const calls = scanTestSource('<virtual>', src)
  assert.deepEqual(
    calls.map((call) => [call.title, call.whatIds, call.state]),
    [
      ['WHAT[F-001] outer', ['F-001'], 'active'],
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
    "import test from 'node:test'",
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

test('WHAT[REQUIREMENT-SYSTEM-018] only executable node:test bindings with callbacks create active trace declarations', () => {
  const src = [
    "import test from 'node:test'",
    "test('WHAT[BINDING-001] missing callback')",
    "test('WHAT[BINDING-002] skipped by options', { skip: true }, () => {})",
    "test('WHAT[BINDING-003] dynamic state', { skip: runtimeFlag }, () => {})",
    '{',
    '  const test = () => undefined',
    "  test('WHAT[BINDING-004] shadowed root', () => {})",
    '}',
    'const register = () => {',
    "  test('WHAT[BINDING-005] indirect registration', () => {})",
    '}',
    "t.test('WHAT[BINDING-006] unbound context', () => {})",
  ].join('\n')

  assert.deepEqual(
    scanTestSource('<virtual>', src).map(({ title, state, issue }) => ({ title, state, issue })),
    [
      { title: 'WHAT[BINDING-001] missing callback', state: 'invalid', issue: 'MissingCallback' },
      { title: 'WHAT[BINDING-002] skipped by options', state: 'skip', issue: null },
      { title: 'WHAT[BINDING-003] dynamic state', state: 'invalid', issue: 'DynamicTestState' },
      { title: 'WHAT[BINDING-004] shadowed root', state: 'invalid', issue: 'ShadowedTestBinding' },
      { title: 'WHAT[BINDING-005] indirect registration', state: 'invalid', issue: 'IndirectRegistration' },
      { title: 'WHAT[BINDING-006] unbound context', state: 'invalid', issue: 'UnboundTestContext' },
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
    writeFileSync(testFile, "import test from 'node:test'\ntest('WHAT[FIXTURE-PACKAGE-001] exact anchor', () => {})\n")

    writeFileSync(proofFile, '| 命题 | 落点 |\n|---|---|\n| FIXTURE-PACKAGE-001 | `tests/case.test.mjs` |\n')
    const bare = buildTraceGraph(requirements)
    assert.equal(bare.proofEdges.length, 1)
    assert.equal(bare.danglingProof[0].reason, 'bare test path has no exact title anchor')
    assert.deepEqual(bare.proofMissing.map(({ id }) => id), ['FIXTURE-PACKAGE-001'])

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
      { line: 2, title: 'WHAT[FIXTURE-PACKAGE-001] exact anchor', state: 'active', whatId: 'FIXTURE-PACKAGE-001' },
    )

    writeFileSync(testFile, [
      "import test from 'node:test'",
      "test('WHAT[FIXTURE-PACKAGE-001] exact anchor', () => {})",
      "test('WHAT[FIXTURE-PACKAGE-001] exact anchor', () => {})",
    ].join('\n') + '\n')
    const ambiguous = buildTraceGraph(requirements)
    assert.equal(ambiguous.danglingProof.length, 1)
    assert.equal(ambiguous.danglingProof[0].reason, 'anchor resolves to multiple tests')
    assert.deepEqual(ambiguous.proofMissing.map(({ id }) => id), ['FIXTURE-PACKAGE-001'])

    writeFileSync(testFile, "import test from 'node:test'\ntest('WHAT[FIXTURE-PACKAGE-001] exact anchor', () => {})\n")
    writeFileSync(proofFile, '| 命题 | 落点 |\n|---|---|\n| FIXTURE-PACKAGE-001 | `tests/case.test.mjs::WHAT[FIXTURE-PACKAGE-001] removed anchor` |\n')
    const dangling = buildTraceGraph(requirements)
    assert.equal(dangling.danglingProof.length, 1)
    assert.equal(dangling.danglingProof[0].state, 'dangling')
  } finally {
    rmSync(root, { recursive: true, force: true })
  }
})

test('WHAT[REQUIREMENT-SYSTEM-018] exact proof-title resolution is reusable and never guesses', () => {
  const tests = scanTestSource(
    '<virtual>',
    [
      "test('WHAT[A-001] exact title', () => {})",
      "test('WHAT[A-001] another title', () => {})",
      "test('WHAT[A-002] exact title', () => {})",
    ].join('\n'),
  )

  assert.deepEqual(resolveExactProofTitle(tests, 'test: WHAT[A-001] another title').map((candidate) => candidate.line), [2])
  assert.deepEqual(resolveExactProofTitle(tests, 'another title').map((candidate) => candidate.line), [2])
  assert.deepEqual(resolveExactProofTitle(tests, 'exact title').map((candidate) => candidate.line), [1, 3])
  assert.deepEqual(resolveExactProofTitle(tests, 'case.test.mjs'), [])
})

test('WHAT[REQUIREMENT-SYSTEM-018] proof levels resolve only from the independent exact registry', () => {
  const registry = JSON.parse(readFileSync(join(ROOT, 'scripts/checks/proof-levels.json'), 'utf8'))
  assert.deepEqual(validateProofLevelRegistry(registry), [])

  const proof = {
    path: 'requirements/durable-events/tests/event-store-identity-collision.test.mjs',
    title: 'WHAT[DURABLE-EVENTS-003] same_EventId_different_canonical_bytes_fail_closed',
    what_id: 'DURABLE-EVENTS-003',
  }
  assert.equal(resolveProofLevel(registry, { ...proof, level: 'adapter' }), 'pure', 'a consumer cannot self-relabel a proof')
  assert.equal(resolveProofLevel(registry, { ...proof, title: `${proof.title}_renamed` }), null)

  const duplicate = structuredClone(registry)
  duplicate.proofs.push({ ...duplicate.proofs.find((entry) => entry.path === proof.path && entry.title === proof.title), level: 'adapter' })
  assert.equal(validateProofLevelRegistry(duplicate).some(({ code }) => code === 'PROOF_LEVEL_DUPLICATE'), true)
  assert.equal(resolveProofLevel(duplicate, proof), null, 'ambiguous registry authority fails closed')
})

test('WHAT[REQUIREMENT-SYSTEM-018] buildTraceGraph classifies orphan / unknown / multi-primary / unproved', () => {
  const graph = buildTraceGraph(REQUIREMENTS)
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

test('WHAT[REQUIREMENT-SYSTEM-018] buildTraceGraph refuses symlink entries inside the governed requirements tree', () => {
  const root = mkdtempSync(join(tmpdir(), 'requirement-trace-symlink-'))
  const requirements = join(root, 'requirements')
  const packageRoot = join(requirements, 'fixture-package')
  const outside = join(root, 'outside.md')
  mkdirSync(packageRoot, { recursive: true })
  try {
    writeFileSync(outside, '# outside\n')
    symlinkSync(outside, join(packageRoot, 'linked.md'))
    assert.throws(
      () => buildTraceGraph(requirements),
      /walk: refusing to traverse symlink entry/,
    )
  } finally {
    rmSync(root, { recursive: true, force: true })
  }
})

test('WHAT[REQUIREMENT-SYSTEM-008] duplicate definitions retain every location and never acquire authority', () => {
  const root = mkdtempSync(join(tmpdir(), 'requirement-trace-definitions-'))
  const requirements = join(root, 'requirements')
  const packageA = join(requirements, 'package-a')
  const packageB = join(requirements, 'package-b')
  mkdirSync(join(packageA, 'tests'), { recursive: true })
  mkdirSync(packageB, { recursive: true })
  try {
    writeFileSync(join(packageA, 'WHAT.md'), '# WHAT\n## SHARED-001: first owner\n## LOCAL-001: first duplicate\n## LOCAL-001: second duplicate\n## UNIQUE-001: unique authority\n')
    writeFileSync(join(packageA, 'HOW.md'), '# HOW\n')
    writeFileSync(join(packageA, 'tests/case.test.mjs'), "import test from 'node:test'\ntest('WHAT[SHARED-001] ambiguous authority', () => {})\n")
    writeFileSync(join(packageB, 'WHAT.md'), '# WHAT\n## SHARED-001: second owner\n')
    writeFileSync(join(packageB, 'HOW.md'), '# HOW\n')

    const graph = buildTraceGraph(requirements)
    assert.deepEqual([...graph.whats.keys()], ['UNIQUE-001'])
    assert.deepEqual(
      graph.duplicateWhats.map(({ id, kind, definitions }) => [id, kind, definitions.map(({ package: owner, line }) => `${owner}:${line}`)]),
      [
        ['SHARED-001', 'multi-owner', ['package-a:2', 'package-b:2']],
        ['LOCAL-001', 'duplicate', ['package-a:3', 'package-a:4']],
      ],
    )
    assert.equal(graph.whatDefinitions.get('SHARED-001').length, 2)
    assert.equal(graph.unknownWhat.includes('SHARED-001'), false, 'ambiguous is not unknown')
    assert.equal(graph.edges[0].what, null, 'an ambiguous definition cannot own a proof edge')
  } finally {
    rmSync(root, { recursive: true, force: true })
  }
})

test('WHAT[REQUIREMENT-SYSTEM-018] graph preserves proof portfolios and rejects orphan or multi-primary tests', () => {
  const root = mkdtempSync(join(tmpdir(), 'requirement-trace-portfolio-'))
  const requirements = join(root, 'requirements')
  const packageRoot = join(requirements, 'fixture-package')
  const tests = join(packageRoot, 'tests')
  mkdirSync(tests, { recursive: true })
  try {
    writeFileSync(join(packageRoot, 'WHAT.md'), '# WHAT\n## PORTFOLIO-001: several independent proofs\n## SECOND-001: another proposition\n')
    writeFileSync(
      join(tests, 'case.test.mjs'),
      [
        "import test from 'node:test'",
        "test('WHAT[PORTFOLIO-001] first proof', () => {})",
        "test('WHAT[PORTFOLIO-001] second proof', () => {})",
        "test('orphan proof', () => {})",
        "test('WHAT[PORTFOLIO-001] WHAT[SECOND-001] ambiguous test owner', () => {})",
      ].join('\n') + '\n',
    )
    writeFileSync(
      join(packageRoot, 'HOW.md'),
      [
        '| 命题 | 落点 |',
        '|---|---|',
        '| PORTFOLIO-001 | `tests/case.test.mjs::WHAT[PORTFOLIO-001] first proof` |',
        '| PORTFOLIO-001 | `tests/case.test.mjs::WHAT[PORTFOLIO-001] second proof` |',
      ].join('\n') + '\n',
    )

    const graph = buildTraceGraph(requirements)
    assert.deepEqual(graph.proofEdges.map(({ whatId, line }) => [whatId, line]), [
      ['PORTFOLIO-001', 2],
      ['PORTFOLIO-001', 3],
    ])
    assert.deepEqual(graph.orphans.map(({ line }) => line), [4])
    assert.deepEqual(graph.multiPrimary.map(({ test: ownedTest }) => ownedTest.line), [5])
  } finally {
    rmSync(root, { recursive: true, force: true })
  }
})

test('WHAT[REQUIREMENT-SYSTEM-018] graph resolves ownership before a five-column semantic proof cell', () => {
  const root = mkdtempSync(join(tmpdir(), 'requirement-trace-wide-how-'))
  const requirements = join(root, 'requirements')
  const packageRoot = join(requirements, 'fixture-package')
  const tests = join(packageRoot, 'tests')
  mkdirSync(tests, { recursive: true })
  try {
    writeFileSync(join(packageRoot, 'WHAT.md'), '# WHAT\n## WIDE-PROOF-001: semantic law\n')
    writeFileSync(join(tests, 'case.test.mjs'), "import test from 'node:test'\ntest('WHAT[WIDE-PROOF-001] exact semantic proof', () => {})\n")
    writeFileSync(join(packageRoot, 'HOW.md'), [
      '| vocabulary | owner | law | relation | proof |',
      '|---|---|---|---|---|',
      '| `owner.entry` | Owner / Source.fs | `WIDE-PROOF-001` | one intent → one outcome | `tests/case.test.mjs::WHAT[WIDE-PROOF-001] exact semantic proof` |',
    ].join('\n') + '\n')

    const graph = buildTraceGraph(requirements)
    assert.equal(graph.danglingProof.length, 0)
    assert.equal(graph.proofMissing.length, 0)
    assert.deepEqual(graph.proofEdges.map(({ whatId, title }) => [whatId, title]), [
      ['WIDE-PROOF-001', 'WHAT[WIDE-PROOF-001] exact semantic proof'],
    ])

    writeFileSync(join(packageRoot, 'HOW.md'), [
      '| vocabulary | owner | law | relation | proof |',
      '|---|---|---|---|---|',
      '| `owner.entry` | Owner / Source.fs | no law owner | one intent → one outcome | `tests/case.test.mjs::WHAT[WIDE-PROOF-001] exact semantic proof` |',
    ].join('\n') + '\n')
    const titleOnly = buildTraceGraph(requirements)
    assert.deepEqual(titleOnly.proofMissing.map(({ id }) => id), ['WIDE-PROOF-001'])
    assert.equal(titleOnly.danglingProof[0].reason, 'test WHAT does not match PROOF proposition')
  } finally {
    rmSync(root, { recursive: true, force: true })
  }
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
    writeFileSync(testFile, "import test from 'node:test'\ntest('WHAT[FIXTURE-PACKAGE-001] exact anchor', () => {})\n")
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
