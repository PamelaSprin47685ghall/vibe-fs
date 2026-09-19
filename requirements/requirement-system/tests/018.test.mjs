import assert from 'node:assert/strict'
import test from 'node:test'
import {
  clauseDefinitionHeadings,
  duplicateClauseDefinitions,
  formalClauseDefinitionHeadings,
  unknownClauseReferences,
} from '../../../scripts/lib/spec-rules.mjs'

test('WHAT[requirement-system-018] RS_018_executable_proof_bidirectional_traceability_rules', () => {
  // 1. 验证证明有效性校验函数：条款定义抽取与格式约束
  const sampleMarkdown = `
# Sample Package

## REQ-001: First Clause
Valid clause body.

## REQ-002: Second Clause
Reference to REQ-001 is valid.
Reference to UNKNOWN-999 is invalid.
`

  const headings = clauseDefinitionHeadings(sampleMarkdown)
  assert.equal(headings.length, 2)
  assert.equal(headings[0].id, 'REQ-001')
  assert.equal(headings[1].id, 'REQ-002')

  // 2. 变异防御：未知命题引用拦截
  const unknownRefs = unknownClauseReferences(sampleMarkdown, ['REQ'])
  assert.equal(unknownRefs.length, 1)
  assert.equal(unknownRefs[0].token, 'UNKNOWN-999')

  // 3. 变异防御：重复条款定义检测（禁止跨包或同一包重复声明权威）
  const duplicateEntries = [
    { file: 'pkg-a/WHAT.md', pkg: 'pkg-a', text: '## REQ-001: First Clause\nContent' },
    { file: 'pkg-b/WHAT.md', pkg: 'pkg-b', text: '## REQ-001: Stolen Clause\nContent' },
  ]
  const dupFindings = duplicateClauseDefinitions(duplicateEntries)
  assert.ok(dupFindings.length > 0, 'Duplicate clause definition across packages must be flagged as violation')
  assert.match(dupFindings[0].msg, /条款 ID 重复定义：REQ-001/)

  // 4. 变异防御：前缀所有权单义性（同一前缀严禁多包定义）
  const multiPrefixEntries = [
    { file: 'pkg-a/WHAT.md', pkg: 'pkg-a', text: '## REQ-001: Clause\nContent' },
    { file: 'pkg-b/WHAT.md', pkg: 'pkg-b', text: '## REQ-002: Clause\nContent' },
  ]
  const prefixFindings = duplicateClauseDefinitions(multiPrefixEntries)
  assert.ok(prefixFindings.length > 0, 'Multiple packages declaring the same prefix must be flagged as violation')
  assert.match(prefixFindings[0].msg, /前缀 REQ- 被多包定义/)

  // 5. 正例验证：合法命题前缀被完整接纳
  const formal = formalClauseDefinitionHeadings(sampleMarkdown, ['REQ'])
  assert.equal(formal.length, 2)
  assert.deepEqual(formal.map((f) => f.id), ['REQ-001', 'REQ-002'])
})
