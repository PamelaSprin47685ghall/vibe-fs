// Moved from tests/unit/orchestrator/program.test.mjs (cutover Wave 2a); owner: structured-workflow.
//
// FLOW-001 Orchestrator direct CE (STRUCTURED-WORKFLOW-001/002): the workflow
// is the exported surface of Application/Orchestration/Program.fs (task CE),
// never a Command/Reply/Step AST + interpreter. PR3 clean break: the Domain
// OrchestratorProgram AST module and OrchestratorInterpreter are deleted and
// must not return.

import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import test from 'node:test'
import { walk } from '../../../scripts/lib/walk.mjs'

const load = (modulePath) => import(new URL(`../../../dist/${modulePath}.js`, import.meta.url).pathname)
const surfaceOf = (mod) => Object.keys(mod).filter((name) => !name.endsWith('_$reflection'))

test('ORCHESTRATOR_PROGRAM_001: Application Program is the sole direct-CE entrypoint', async () => {
  const mod = await load('Change/Program')
  assert.deepEqual(surfaceOf(mod).sort(), ['run'])
  assert.equal(typeof mod.run, 'function')
})

test('ORCHESTRATOR_PROGRAM_002: Domain OrchestratorProgram AST module is gone', async () => {
  await assert.rejects(
    () => load('Domain/OrchestratorProgram'),
    (error) => {
      const message = String(error?.message ?? error)
      return (
        message.includes('Cannot find module') ||
        message.includes('ERR_MODULE_NOT_FOUND') ||
        message.includes('Failed to load') ||
        error?.code === 'ERR_MODULE_NOT_FOUND'
      )
    },
    'Domain/OrchestratorProgram must be deleted after PR3 direct-CE cutover',
  )
})

test('ORCHESTRATOR_PROGRAM_003: OrchestratorInterpreter is gone', async () => {
  await assert.rejects(
    () => load('Application/Orchestration/OrchestratorInterpreter'),
    (error) => {
      const message = String(error?.message ?? error)
      return (
        message.includes('Cannot find module') ||
        message.includes('ERR_MODULE_NOT_FOUND') ||
        message.includes('Failed to load') ||
        error?.code === 'ERR_MODULE_NOT_FOUND'
      )
    },
    'Application/Orchestration/OrchestratorInterpreter must be deleted after PR3',
  )
})

test('ORCHESTRATOR_PROGRAM_004: no Command/Reply/Step AST tokens in Orchestration workflow source', () => {
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
