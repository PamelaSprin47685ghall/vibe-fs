import assert from 'node:assert/strict'
import test from 'node:test'
import { duplicateClauseDefinitions } from '../../../scripts/lib/spec-rules.mjs'

test('WHAT[requirement-system-007] package explanatory documents may reference clauses but cannot define them', () => {
  const what = { file: 'requirements/sample/WHAT.md', pkg: 'sample', text: '## [001] Observable rule\n' }
  for (const name of ['WHY.md', 'tests/README.md']) {
    const explanation = { file: `requirements/sample/${name}`, pkg: 'sample', text: 'Reason or example for sample-001.\n' }
    assert.deepEqual(duplicateClauseDefinitions([what, explanation]), [])
    const findings = duplicateClauseDefinitions([what, { ...explanation, text: '## [002] Extra rule\n' }])
    assert.equal(findings.length, 1)
    assert.equal(findings[0].file, explanation.file)
    assert.match(findings[0].msg, /非 WHAT 文件严禁定义正式条款/)
  }
})
