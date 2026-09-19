import assert from 'node:assert/strict'
import test from 'node:test'
import {
  duplicateClauseDefinitions,
  isProposalPath,
} from '../../../scripts/lib/spec-rules.mjs'

test('WHAT[requirement-system-011] spec gate ignores proposals directory by default', () => {
  // [011]：未来材料与延期提案归存于 proposals/ 目录并由用户全权管理。除非用户明确要求阅读/操作，否则直接忽略。
  // 1. isProposalPath 准确定位 proposals 路径
  assert.ok(isProposalPath('proposals/something.md'))
  assert.ok(isProposalPath('requirements/proposals/future.md'))
  assert.ok(!isProposalPath('requirements/requirement-system/WHAT.md'))
  assert.ok(!isProposalPath('scripts/lib/spec-rules.mjs'))

  // 2. 门禁扫描时必须忽略 proposals/ 目录下的所有文件，哪怕包含重复定义或非法格式
  const entries = [
    {
      file: 'requirements/proposals/future-feature/WHAT.md',
      pkg: 'future-feature',
      text: '# WHAT\n\n## [001] some proposal clause\n',
    },
    {
      file: 'proposals/deferred.md',
      pkg: 'proposals',
      text: '## [001] deferred idea\n',
    },
    {
      file: 'requirements/requirement-system/WHAT.md',
      pkg: 'requirement-system',
      text: '# requirement-system — WHAT\n\n## [001] legal clause\n',
    },
  ]

  const findings = duplicateClauseDefinitions(entries)
  assert.deepEqual(
    findings,
    [],
    'duplicateClauseDefinitions must ignore files in proposals/ without generating errors',
  )
})
