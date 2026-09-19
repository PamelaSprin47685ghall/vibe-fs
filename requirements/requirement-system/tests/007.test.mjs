import assert from 'node:assert/strict'
import test from 'node:test'
import {
  duplicateClauseDefinitions,
  markdownLocalLinks,
  navigationProblems,
} from '../../../scripts/lib/spec-rules.mjs'

test('WHAT[requirement-system-007] WHAT.md is the sole normative authority; WHY.md and HOW.md cannot define clauses', () => {
  // WHAT.md 是该包唯一权威，为用户可观察行为；WHY.md 仅解释设计理由与动机，HOW.md 仅说明实现决策
  const entries = [
    {
      file: 'requirements/sample-pkg/WHY.md',
      pkg: 'sample-pkg',
      text: '# WHY\n\n## [001] clause defined in WHY is forbidden\n',
    },
    {
      file: 'requirements/sample-pkg/HOW.md',
      pkg: 'sample-pkg',
      text: '# HOW\n\n## [002] clause defined in HOW is forbidden\n',
    },
    {
      file: 'requirements/sample-pkg/WHAT.md',
      pkg: 'sample-pkg',
      text: '# WHAT\n\n## [001] clause defined in WHAT is normative and authoritative\n',
    },
  ]

  const findings = duplicateClauseDefinitions(entries)
  assert.equal(findings.length, 2, 'must reject clause definitions from WHY.md and HOW.md')
  assert.ok(findings.some((f) => f.file.includes('WHY.md') && f.msg.includes('非 WHAT 文件严禁定义正式条款')))
  assert.ok(findings.some((f) => f.file.includes('HOW.md') && f.msg.includes('非 WHAT 文件严禁定义正式条款')))
})

test('WHAT[requirement-system-007] spec gate requires exact README coverage of formal files', () => {
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

test('WHAT[requirement-system-007] spec gate extracts local Markdown links without treating URLs or anchors as files', () => {
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
