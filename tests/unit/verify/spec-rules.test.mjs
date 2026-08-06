import assert from 'node:assert/strict'
import test from 'node:test'

import {
  clauseReferences,
  fluidNavigationProblems,
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
    '[research](<proposal/ChatGPT-F# DSL 规范问题.md>)',
    '[stale](proposal/stale.md)',
  ].join('\n')

  assert.deepEqual(
    fluidNavigationProblems(navigation, 'proposal', [
      'proposal/ChatGPT-F# DSL 规范问题.md',
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
