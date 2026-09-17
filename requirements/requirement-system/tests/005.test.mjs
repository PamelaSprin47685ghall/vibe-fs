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

test('WHAT[REQUIREMENT-SYSTEM-005] formalClauseDefinitionHeadings surfaces clause definitions from routing files', () => {
  // README/AGENTS/CHANGELOG 不是规范正文（无裸规范权威）；识别器必须仍能发现
  // 路由文件里的产品条款定义，使 scripts/lib/spec-rules.mjs 的 duplicateClauseDefinitions「正式条款只能定义在
  // package WHAT.md」gate 可以拒绝它。
  assert.deepEqual(
    formalClauseDefinitionHeadings([
      '# README',
      '## ARCH-002: a clause defined in a navigation file',
    ].join('\n'), PREFIXES),
    [{ id: 'ARCH-002', line: 2 }],
  )
})
