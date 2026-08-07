import assert from 'node:assert/strict'
import test from 'node:test'

import {
  clauseDefinitionHeadings,
  clauseReferences,
  fluidNavigationProblems,
  markdownLocalLinks,
  proposalDependencyReferences,
  statusNavigationProblems,
  unknownClauseReferences,
} from '../../../scripts/checks/spec-rules.mjs'

const PREFIXES = ['ARCH', 'GOV', 'HOST']

test('spec gate rejects unknown and suffixed clause-looking references', () => {
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

test('spec gate requires exact README coverage of active status files', () => {
  const navigation = [
    '[kept](status/kept.md)',
    '[stale](status/stale.md)',
  ].join('\n')

  assert.deepEqual(
    statusNavigationProblems(navigation, ['status/kept.md', 'status/missing.md']),
    {
      missing: ['status/missing.md'],
      stale: [{ file: 'status/stale.md', line: 2 }],
    },
  )
})

test('spec gate covers proposal links with spaces and hash characters exactly', () => {
  const navigation = [
    '[kept](proposal/kept.md)',
    '[research](<proposal/research # note.md>)',
    '[stale](proposal/stale.md)',
  ].join('\n')

  assert.deepEqual(
    fluidNavigationProblems(navigation, 'proposal', [
      'proposal/research # note.md',
      'proposal/kept.md',
      'proposal/missing.md',
    ]),
    {
      missing: ['proposal/missing.md'],
      stale: [{ file: 'proposal/stale.md', line: 3 }],
    },
  )
})

test('spec gate expands slash lists and checks range endpoints', () => {
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

test('spec gate finds Clause-shaped headings for any prefix and heading depth', () => {
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

test('spec gate detects implementation dependencies on Proposal IDs and paths', () => {
  assert.deepEqual(
    proposalDependencyReferences([
      '// FUTURE-001 is treated as a contract',
      '// docs/proposal/future.md',
      '// ARCH-001 is formal',
    ].join('\n'), ['FUTURE-001']),
    [
      { token: 'FUTURE-001', line: 1 },
      { token: 'docs/proposal/', line: 2 },
    ],
  )
})

test('spec gate extracts local Markdown links without treating URLs or anchors as files', () => {
  assert.deepEqual(
    markdownLocalLinks([
      '[plain](what/agent.md)',
      '[space](<proposal/research note.md>)',
      '[encoded](proposal/research%20note.md#section)',
      '[anchor](#local)',
      '[web](https://example.com/doc.md)',
    ].join('\n')),
    [
      { target: 'what/agent.md', line: 1 },
      { target: 'proposal/research note.md', line: 2 },
      { target: 'proposal/research note.md', line: 3 },
    ],
  )
})
