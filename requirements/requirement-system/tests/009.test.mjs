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

test('WHAT[REQUIREMENT-SYSTEM-009] formalClauseDefinitionHeadings still recognizes a product clause defined in a Change file', () => {
  // Change 文件不得承担正式定义职责；formalClauseDefinitionHeadings 必须仍能识别
  // Change 文件里的产品条款定义（ARCH-001），由 scripts/lib/spec-rules.mjs 的 duplicateClauseDefinitions
  //「正式定义只在 WHAT.md」gate 拒绝它。
  assert.deepEqual(
    formalClauseDefinitionHeadings([
      '# CHG-002: some lifecycle identity',
      '## ARCH-001: a product clause smuggled into a Change file',
    ].join('\n'), PREFIXES),
    [{ id: 'ARCH-001', line: 2 }],
  )
})
