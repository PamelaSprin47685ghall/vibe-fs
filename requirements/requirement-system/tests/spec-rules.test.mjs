import assert from 'node:assert/strict'
import test from 'node:test'

import {
  archivePathReferences,
  changeDependencyReferences,
  clauseDefinitionHeadings,
  clauseReferences,
  formalClauseDefinitionHeadings,
  legacyWorkflowPathReferences,
  markdownLocalLinks,
  navigationProblems,
  unknownClauseReferences,
} from '../../../scripts/checks/spec-rules.mjs'

const PREFIXES = ['ARCH', 'GOV', 'HOST']

test('WHAT[REQUIREMENT-SYSTEM-008] spec gate rejects unknown and suffixed clause-looking references', () => {
  assert.deepEqual(
    unknownClauseReferences(
      ['ARCH-010 is valid', 'ARCH-010-TOOL-BOUND is not', 'SECURITY-001 is unknown', 'SHA-256 is an algorithm'].join('\n'),
      PREFIXES,
    ),
    [
      { token: 'ARCH-010-TOOL-BOUND', line: 2 },
      { token: 'SECURITY-001', line: 3 },
    ],
  )
})

test('WHAT[REQUIREMENT-SYSTEM-007] spec gate requires exact README coverage of formal files', () => {
  const navigation = [
    '[kept](what/kept.md)',
    '[stale](what/stale.md)',
  ].join('\n')

  assert.deepEqual(
    navigationProblems(navigation, 'what', ['what/kept.md', 'what/missing.md']),
    {
      missing: ['what/missing.md'],
      stale: [{ file: 'what/stale.md', line: 2 }],
    },
  )
})

test('WHAT[REQUIREMENT-SYSTEM-007] spec gate covers links with spaces and hash characters exactly', () => {
  const navigation = [
    '[kept](why/kept.md)',
    '[research](<why/research # note.md>)',
    '[stale](why/stale.md)',
  ].join('\n')

  assert.deepEqual(
    navigationProblems(navigation, 'why', [
      'why/research # note.md',
      'why/kept.md',
      'why/missing.md',
    ]),
    {
      missing: ['why/missing.md'],
      stale: [{ file: 'why/stale.md', line: 3 }],
    },
  )
})

test('WHAT[REQUIREMENT-SYSTEM-008] spec gate expands slash lists and checks range endpoints', () => {
  assert.deepEqual(
    clauseReferences(
      ['ARCH-001/003', 'HOST-009..012', 'ARCH-001…008'].join('\n'),
      PREFIXES,
    ),
    [
      { id: 'ARCH-001', line: 1 },
      { id: 'ARCH-003', line: 1 },
      { id: 'HOST-009', line: 2 },
      { id: 'HOST-012', line: 2 },
      { id: 'ARCH-001', line: 3 },
      { id: 'ARCH-008', line: 3 },
    ],
  )
})

test('WHAT[REQUIREMENT-SYSTEM-008] spec gate finds Clause-shaped headings for any prefix and heading depth', () => {
  assert.deepEqual(
    clauseDefinitionHeadings([
      '# PROPOSE-001: candidate',
      'text PROPOSE-002 is only a reference',
      '### ARCH-010: shadow',
      '## FUTURE-042B: suffixed candidate',
    ].join('\n')),
    [
      { id: 'PROPOSE-001', line: 1 },
      { id: 'ARCH-010', line: 3 },
      { id: 'FUTURE-042B', line: 4 },
    ],
  )
})

test('WHAT[REQUIREMENT-SYSTEM-005] formalClauseDefinitionHeadings surfaces clause definitions from routing files', () => {
  // README/AGENTS/CHANGELOG 不是规范正文（无裸规范权威）；识别器必须仍能发现
  // 路由文件里的产品条款定义，使 scripts/checks/spec.mjs 的「正式条款只能定义在
  // package WHAT.md」gate 可以拒绝它。
  assert.deepEqual(
    formalClauseDefinitionHeadings([
      '# README',
      '## ARCH-002: a clause defined in a navigation file',
    ].join('\n'), PREFIXES),
    [{ id: 'ARCH-002', line: 2 }],
  )
})

test('WHAT[REQUIREMENT-SYSTEM-012] formalClauseDefinitionHeadings separates CHG-001 from product clauses', () => {
  assert.deepEqual(
    formalClauseDefinitionHeadings([
      '# CHG-001: lifecycle identity',
      '## ARCH-001: forbidden shadow definition',
      '### FUTURE-001: non-product candidate',
    ].join('\n'), PREFIXES),
    [{ id: 'ARCH-001', line: 2 }],
  )
})

test('WHAT[REQUIREMENT-SYSTEM-009] formalClauseDefinitionHeadings still recognizes a product clause defined in a Change file', () => {
  // Change 文件不得承担正式定义职责；formalClauseDefinitionHeadings 必须仍能识别
  // Change 文件里的产品条款定义（ARCH-001），由 scripts/checks/spec.mjs 的
  //「正式定义只在 WHAT.md」gate 拒绝它。
  assert.deepEqual(
    formalClauseDefinitionHeadings([
      '# CHG-002: some lifecycle identity',
      '## ARCH-001: a product clause smuggled into a Change file',
    ].join('\n'), PREFIXES),
    [{ id: 'ARCH-001', line: 2 }],
  )
})

test('WHAT[REQUIREMENT-SYSTEM-010] spec gate detects retired workflow paths', () => {
  assert.deepEqual(
    legacyWorkflowPathReferences([
      'read docs/proposal/future.md',
      'read docs/status/gap.md',
      'session.status is unrelated',
    ].join('\n')),
    [
      { token: 'docs/proposal/', line: 1 },
      { token: 'docs/status/', line: 2 },
    ],
  )
})

test('WHAT[REQUIREMENT-SYSTEM-010] spec gate detects references to the deleted archive tree', () => {
  assert.deepEqual(
    archivePathReferences([
      '// archive/docs/proof/verify.md is gone',
      'no archive reference here',
      'archive/ at line end without a path',
      'prearchive/ is a different word',
    ].join('\n')),
    [
      { token: 'archive/docs/proof/verify.md', line: 1 },
      { token: 'archive/', line: 3 },
    ],
  )
})

test('WHAT[REQUIREMENT-SYSTEM-011] spec gate rejects proposed and specific completed dependencies but allows active scope', () => {
  assert.deepEqual(
    changeDependencyReferences([
      '// changes/proposed/future.md is not current',
      '// changes/active/current.md may scope work',
      '// changes/completed/history.md is not current',
      '// changes/completed/ is a generic lifecycle directory',
    ].join('\n')),
    [
      { token: 'changes/proposed/', line: 1 },
      { token: 'changes/completed/<file>.md', line: 3 },
    ],
  )
})

test('WHAT[REQUIREMENT-SYSTEM-007] spec gate extracts local Markdown links without treating URLs or anchors as files', () => {
  assert.deepEqual(
    markdownLocalLinks([
      '[plain](what/agent.md)',
      '[space](<notes/research note.md>)',
      '[encoded](notes/research%20note.md#section)',
      '[anchor](#local)',
      '[web](https://example.com/doc.md)',
    ].join('\n')),
    [
      { target: 'what/agent.md', line: 1 },
      { target: 'notes/research note.md', line: 2 },
      { target: 'notes/research note.md', line: 3 },
    ],
  )
})
