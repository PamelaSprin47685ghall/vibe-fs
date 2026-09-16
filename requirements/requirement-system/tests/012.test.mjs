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
