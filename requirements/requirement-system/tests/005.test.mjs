import assert from 'node:assert/strict'
import test from 'node:test'
import {
  clauseDefinitionHeadings,
  duplicateClauseDefinitions,
} from '../../../scripts/lib/spec-rules.mjs'

test('WHAT[requirement-system-005] formal clause definitions can only live in package WHAT.md', () => {
  // README/AGENTS/CHANGELOG 等非 WHAT.md 路由与系统文件严禁定义正式条款
  const entries = [
    {
      file: 'requirements/README.md',
      pkg: 'requirement-system',
      text: '# README\n\n## [002] illegal clause in navigation file\n',
    },
    {
      file: 'AGENTS.md',
      pkg: 'requirement-system',
      text: '# AGENTS\n\n## [003] illegal clause in root doc\n',
    },
    {
      file: 'CHANGELOG.md',
      pkg: 'requirement-system',
      text: '# CHANGELOG\n\n## [004] illegal clause in changelog\n',
    },
    {
      file: 'requirements/requirement-system/WHAT.md',
      pkg: 'requirement-system',
      text: '# requirement-system — WHAT\n\n## [001] legal clause\n',
    },
  ]

  const findings = duplicateClauseDefinitions(entries)
  assert.equal(findings.length, 3, 'must flag all 3 non-WHAT definitions')
  assert.ok(findings.some((f) => f.file.includes('README.md') && f.msg.includes('非 WHAT 文件严禁定义正式条款')))
  assert.ok(findings.some((f) => f.file.includes('AGENTS.md') && f.msg.includes('非 WHAT 文件严禁定义正式条款')))
  assert.ok(findings.some((f) => f.file.includes('CHANGELOG.md') && f.msg.includes('非 WHAT 文件严禁定义正式条款')))
})

test('WHAT[requirement-system-005] clauseDefinitionHeadings surfaces ## [NNN] definitions from any file for detection', () => {
  assert.deepEqual(
    clauseDefinitionHeadings([
      '# README',
      '## [002] a clause defined in a navigation file',
    ].join('\n')),
    [{ id: '002', line: 2 }],
  )
})

