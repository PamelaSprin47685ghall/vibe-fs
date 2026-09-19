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

test('WHAT[requirement-system-011] spec gate rejects proposed and specific completed dependencies but allows active scope', () => {
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
