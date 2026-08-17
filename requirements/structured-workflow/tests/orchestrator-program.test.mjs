// Moved from tests/unit/orchestrator/program.test.mjs (cutover Wave 2a); owner: structured-workflow.
//
// FLOW-001 Orchestrator direct CE (STRUCTURED-WORKFLOW-001/002): the workflow
// is the exported surface of Application/Orchestration/Program.fs (task CE),
// never a Command/Reply/Step AST + interpreter. PR3 clean break: the Domain
// OrchestratorProgram AST module and OrchestratorInterpreter are deleted and
// must not return.
//
// Build-verification (guide-contract.test.mjs) proves Change/Program exports
// `run` and that the deleted modules stay deleted. This semantic test proves
// the source-tree invariant: no second-runtime protocol tokens in the
// Orchestration workflow source.

import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import test from 'node:test'
import { walk } from '../../../scripts/lib/walk.mjs'

test('WHAT[STRUCTURED-WORKFLOW-002] ORCHESTRATOR_PROGRAM_004: no Command/Reply/Step AST tokens in Orchestration workflow source', () => {
  // Fail closed if a second-runtime protocol sneaks back into the vertical slice.
  const files = walk('src/Wanxiangshu/Change', ['.fs'])
  assert.ok(files.length > 0, 'expected Change/*.fs')
  const forbidden =
    /\b(?:type|and)\s+(?:private\s+|internal\s+|public\s+)?(?:\w*(?:Command|Reply)(?:<[^=>]*>)?|(?:\w*Program)<[^=>]*>)\s*=|\|\s*(?:Step|Suspend)\s+of\b|\bProtocolMismatch\b|\bmodule\s+(?:private\s+|internal\s+)?(?:\w+\.)*\w*Interpreter\s*=/
  const hits = []
  for (const file of files) {
    const text = readFileSync(file, 'utf8')
    for (const [i, line] of text.split('\n').entries()) {
      const code = line.replace(/\/\/.*/g, '').trim()
      if (code && forbidden.test(code)) hits.push(`${file}:${i + 1}: ${line.trim()}`)
    }
  }
  assert.deepEqual(hits, [])
})

test('WHAT[STRUCTURED-WORKFLOW-002] ORCHESTRATOR_PROGRAM_002: Domain OrchestratorProgram AST module is gone', () => {
  const files = walk('src/Wanxiangshu', ['.fs'])
  const ast = files.filter((file) => /OrchestratorProgram\.fs$/.test(file))
  assert.deepEqual(ast, [], 'Domain OrchestratorProgram AST module must be deleted after PR3 direct-CE cutover')
})

test('WHAT[STRUCTURED-WORKFLOW-002] ORCHESTRATOR_PROGRAM_003: OrchestratorInterpreter is gone', () => {
  const files = walk('src/Wanxiangshu', ['.fs'])
  const interpreter = files.filter((file) => /OrchestratorInterpreter\.fs$/.test(file))
  assert.deepEqual(interpreter, [], 'OrchestratorInterpreter must be deleted after PR3')
})
