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
