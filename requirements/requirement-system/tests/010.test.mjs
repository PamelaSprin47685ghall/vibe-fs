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

test('WHAT[requirement-system-010] spec gate detects retired workflow paths', () => {
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

test('WHAT[requirement-system-010] spec gate detects references to the deleted archive tree', () => {
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
