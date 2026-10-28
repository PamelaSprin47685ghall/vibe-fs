import assert from 'node:assert/strict'
import test from 'node:test'
import {
  clauseDefinitionHeadings,
  clauseReferences,
  unknownClauseReferences,
} from '../../../scripts/lib/spec-rules.mjs'

test('WHAT[requirement-system-008] spec gate rejects unknown, vacant, and suffixed clause references', () => {
  // 现行格式：小写 包名-NNN 为引用；有效条款集明确给定（含存活条款），009 为永久空缺编号
  const validClauses = new Set([
    'requirement-system-001',
    'requirement-system-008',
    'distribution-001',
  ])
  const text = [
    'requirement-system-008 is valid',
    'requirement-system-008-TOOL-BOUND is not',
    'unknown-pkg-001 is unknown',
    'requirement-system-009 is vacant',
    'SHA-256 is an algorithm',
  ].join('\n')

  const findings = unknownClauseReferences(text, validClauses)
  assert.deepEqual(findings, [
    { token: 'requirement-system-008-TOOL-BOUND', line: 2 },
    { token: 'unknown-pkg-001', line: 3 },
    { token: 'requirement-system-009', line: 4 },
  ])
})

test('WHAT[requirement-system-008] spec gate expands slash lists and checks range endpoints with lowercase clause IDs', () => {
  assert.deepEqual(
    clauseReferences(
      [
        'requirement-system-001/003',
        'distribution-009..012',
        'requirement-system-001…008',
      ].join('\n'),
      ['requirement-system', 'distribution'],
    ),
    [
      { id: 'requirement-system-001', line: 1 },
      { id: 'requirement-system-003', line: 1 },
      { id: 'distribution-009', line: 2 },
      { id: 'distribution-012', line: 2 },
      { id: 'requirement-system-001', line: 3 },
      { id: 'requirement-system-008', line: 3 },
    ],
  )
})

test('WHAT[requirement-system-008] spec gate finds ## [NNN] Clause definition headings in WHAT.md', () => {
  assert.deepEqual(
    clauseDefinitionHeadings([
      '# requirement-system — WHAT',
      '## [001] 唯一语义所有权',
      'text requirement-system-001 is only a reference',
      '### [008] 条款 ID',
    ].join('\n')),
    [
      { id: '001', line: 2 },
      { id: '008', line: 4 },
    ],
  )
})
