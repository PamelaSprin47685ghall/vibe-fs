import assert from 'node:assert/strict'
import fs from 'node:fs'
import path from 'node:path'
import test from 'node:test'
import { fileURLToPath } from 'node:url'

import { REPO_ROOT, scanRawTimeTokens } from '../../../scripts/checks/raw-time-scanner.mjs'
import * as ReconcileSurface from '../../../dist/Composition/Turn/ReconcileSurface.js'

const repoRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../..')
const orchestratorWorkflowPath = path.join(
  repoRoot,
  'src/Wanxiangshu/Change/Host/OrchestratorWorkflow.fs',
)

test('WHAT[STRUCTURED-WORKFLOW-002] ORCHESTRATOR_PROGRAM_004: no Command/Reply/Step AST tokens in Orchestration workflow source', () => {
  assert.ok(
    fs.existsSync(orchestratorWorkflowPath),
    `OrchestratorWorkflow.fs must exist at ${orchestratorWorkflowPath}`,
  )
  const source = fs.readFileSync(orchestratorWorkflowPath, 'utf8')
  const lines = source.split('\n')
  const forbiddenPatterns = [
    /\btype\s+OrchestratorCommand\b/,
    /\btype\s+OrchestratorReply\b/,
    /\btype\s+OrchestratorStep\b/,
    /\bOrchestratorCommand\./,
    /\bOrchestratorReply\./,
    /\bOrchestratorStep\./,
    /\binterpret\s*\(/,
    /\bstepInterpreter\b/,
  ]
  const violations = []
  for (let i = 0; i < lines.length; i++) {
    const line = lines[i]
    if (line.trim().startsWith('//')) continue
    for (const pattern of forbiddenPatterns) {
      if (pattern.test(line)) {
        violations.push(`Line ${i + 1}: ${line.trim()} (matched ${pattern})`)
      }
    }
  }
  assert.deepEqual(
    violations,
    [],
    `Found forbidden AST/interpreter tokens in OrchestratorWorkflow.fs:\n${violations.join('\n')}`,
  )
})

test('WHAT[STRUCTURED-WORKFLOW-002] ORCHESTRATOR_PROGRAM_002: Domain OrchestratorProgram AST module is gone', () => {
  const legacyProgramFs = path.join(repoRoot, 'src/Wanxiangshu/Change/OrchestratorProgram.fs')
  assert.equal(
    fs.existsSync(legacyProgramFs),
    false,
    'OrchestratorProgram.fs must not exist in domain Change/',
  )
})

test('WHAT[STRUCTURED-WORKFLOW-002] ORCHESTRATOR_PROGRAM_003: OrchestratorInterpreter is gone', () => {
  const legacyInterpreterFs = path.join(
    repoRoot,
    'src/Wanxiangshu/Change/Host/OrchestratorInterpreter.fs',
  )
  assert.equal(
    fs.existsSync(legacyInterpreterFs),
    false,
    'OrchestratorInterpreter.fs must not exist in Change/Host/',
  )
})

test('WHAT[STRUCTURED-WORKFLOW-002] G4R_CE_documents_raw_time_tokens_and_scan_root', () => {
  assert.equal(typeof scanRawTimeTokens, 'function')
  assert.equal(typeof REPO_ROOT, 'string')
})

test('WHAT[STRUCTURED-WORKFLOW-002] G4R_CE_S0_raw_time_scanner_RED_on_synthetic_tokens', () => {
  const syntheticCases = [
    { text: 'let x = DateTime.UtcNow', expectedToken: 'DateTime.UtcNow' },
    { text: 'let y = DateTimeOffset.Now', expectedToken: 'DateTimeOffset.Now' },
    { text: 'let z = Date.now()', expectedToken: 'Date.now' },
    { text: 'let ms = System.Environment.TickCount', expectedToken: 'System.Environment.TickCount' },
    { text: 'do! Async.Sleep 100', expectedToken: 'Async.Sleep' },
    { text: 'do! Task.Delay 100', expectedToken: 'Task.Delay' },
  ]
  for (const c of syntheticCases) {
    const hits = scanRawTimeTokens(c.text, 'synthetic.fs')
    assert.ok(hits.length >= 1, `expected hit for ${c.expectedToken}`)
    assert.equal(hits[0].token, c.expectedToken)
  }
})

test('WHAT[STRUCTURED-WORKFLOW-002] G4R_CE_S0_raw_time_scanner_ignores_comment_only_mentions', () => {
  const commentedSource = `
    // this comment mentions DateTime.UtcNow and Task.Delay(100)
    (* block comment with Date.now() *)
    let validCode = 42
  `
  const hits = scanRawTimeTokens(commentedSource, 'synthetic.fs')
  assert.deepEqual(hits, [])
})

test('WHAT[STRUCTURED-WORKFLOW-002] G4R_CE_raw_time_allowlist_is_exact_file_only', () => {
  const hits = scanRawTimeTokens('let x = DateTime.UtcNow', 'src/Wanxiangshu/Unapproved/File.fs')
  assert.ok(hits.length >= 1)
})

test('WHAT[STRUCTURED-WORKFLOW-002] RECONCILE_PROGRAM_006: Domain surface has no Command/Reply/Trace AST exports', () => {
  const exported = Object.keys(ReconcileSurface)
  for (const name of ['Command', 'Reply', 'Trace', 'Program', 'Interpreter']) {
    assert.equal(
      exported.includes(name),
      false,
      `ReconcileSurface must not export AST-shaped name: ${name}`,
    )
  }
})
