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
} from '../../../scripts/lib/spec-rules.mjs'

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
