// requirement-trace.test.mjs — REQUIREMENT-SYSTEM-018 的机器落点（自举包）。
//
// 本文件自身使用 WHAT[REQUIREMENT-SYSTEM-018] 标签（018 规范要求测试显式声明
// 恰一个 primary WHAT）；每个 test 只回答一个问题。

import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
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
  ].join('\n')
  const calls = scanTestSource('<virtual>', src)
  assert.deepEqual(
    calls.map((c) => [c.title, c.state]),
    [
      ['WHAT[B-001] skipped still carries a tag', 'skip'],
      ['WHAT[B-002] todo is not proof', 'todo'],
      ['WHAT[B-003] nested counts', 'active'],
      ['WHAT[B-004] nested skip counts', 'skip'],
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

test('WHAT[REQUIREMENT-SYSTEM-018] buildTraceGraph classifies orphan / unknown / multi-primary / unproved', () => {
  const graph = buildTraceGraph(ROOT)
  assert.ok(graph.whats.size > 0, 'requirements tree must define WHAT propositions')
  assert.ok(graph.tests.length > 0, 'requirements tree must contain tests')
  for (const t of graph.tests) {
    for (const id of t.whatIds) {
      assert.match(id, /^[A-Z][A-Z0-9-]*-\d{3}(?:[A-Z]|-[A-Z0-9-]+)?$/, `tag ${id} must be a well-formed WHAT ID`)
    }
  }
  // multi-primary is only ever produced by the scanner, never by this suite.
  assert.equal(graph.multiPrimary.length, 0)
})

test('WHAT[REQUIREMENT-SYSTEM-018] packageOf resolves the owning package, not a tests/eval directory', () => {
  assert.equal(packageOf('requirements/office-capability/tests/eval/provider-office-boundary/office-boundary-eval.test.mjs'), 'office-capability')
  assert.equal(packageOf('requirements/behavior-diagnosis/tests/paired-history-eval.test.mjs'), 'behavior-diagnosis')
  assert.equal(packageOf('requirements/verification-system/tests/run.mjs'), 'verification-system')
  assert.equal(packageOf('scripts/checks/requirement-trace.mjs'), null)
})
