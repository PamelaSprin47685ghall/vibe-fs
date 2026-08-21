// requirements/capability-enforcement/tests/tool-spec-contracts.test.mjs
//
// ENF-002 / ENF-009: ToolSpec + Admission contracts.
// Each tool owner defines its own ToolSpec with typed Admission.
// ToolRegistry is an aggregate + Host projection and delegates admission to spec.Admission.

import assert from 'node:assert/strict'
import test from 'node:test'

import { rolePredicate } from '../../../dist/OpenCode/Tools/ToolRegistrySurface.js'

test('WHAT[ENF-002] TOOLSPEC_delegation_tools_have_owner_defined_admission', () => {
  // fork: Manager only
  assert.equal(rolePredicate('fork', 'manager'), true)
  assert.equal(rolePredicate('fork', 'coder'), false)
  assert.equal(rolePredicate('fork', 'orchestrator'), false)

  // commission: Orchestrator only
  assert.equal(rolePredicate('commission', 'orchestrator'), true)
  assert.equal(rolePredicate('commission', 'manager'), false)
  assert.equal(rolePredicate('commission', 'coder'), false)

  // join & horizon: Join/Horizon permissions
  assert.equal(rolePredicate('join', 'manager'), true)
  assert.equal(rolePredicate('join', 'orchestrator'), true)
  assert.equal(rolePredicate('join', 'coder'), false)

  assert.equal(rolePredicate('horizon', 'manager'), true)
  assert.equal(rolePredicate('horizon', 'orchestrator'), true)
  assert.equal(rolePredicate('horizon', 'coder'), false)
})

test('WHAT[ENF-002] TOOLSPEC_coder_and_devops_tools_have_owner_defined_admission', () => {
  // bash-honeypot: Coder only
  assert.equal(rolePredicate('bash-honeypot', 'coder'), true)
  assert.equal(rolePredicate('bash-honeypot', 'inspector'), false)
  assert.equal(rolePredicate('bash-honeypot', 'devops'), false)

  // mv & rm: Coder only (Move / Remove permission)
  assert.equal(rolePredicate('mv', 'coder'), true)
  assert.equal(rolePredicate('mv', 'inspector'), false)
  assert.equal(rolePredicate('rm', 'coder'), true)
  assert.equal(rolePredicate('rm', 'inspector'), false)

  // pty tools: DevOps only
  assert.equal(rolePredicate('open-terminal', 'devops'), true)
  assert.equal(rolePredicate('open-terminal', 'coder'), false)
  assert.equal(rolePredicate('send-terminal', 'devops'), true)
  assert.equal(rolePredicate('read-terminal', 'devops'), true)
  assert.equal(rolePredicate('signal-terminal', 'devops'), true)

  // run: DevOps only; query-shell: Inspector only
  assert.equal(rolePredicate('run', 'devops'), true)
  assert.equal(rolePredicate('run', 'inspector'), false)
  assert.equal(rolePredicate('query-shell', 'inspector'), true)
  assert.equal(rolePredicate('query-shell', 'devops'), false)

  // behavior tools: DevOps only
  assert.equal(rolePredicate('establish-behavior', 'devops'), true)
  assert.equal(rolePredicate('establish-behavior', 'coder'), false)
  assert.equal(rolePredicate('repair-behavior', 'devops'), true)
  assert.equal(rolePredicate('repair-behavior', 'coder'), false)
})

test('WHAT[ENF-002] TOOLSPEC_cognitive_utility_and_fission_tools_admission', () => {
  // assume / enough / abandon / defer / subscribe / publish / celebrate / regret
  // Allowed for interactive roles, denied for Blogger and Distiller
  for (const tool of ['assume', 'enough', 'abandon', 'defer', 'subscribe', 'publish', 'celebrate', 'regret']) {
    assert.equal(rolePredicate(tool, 'coder'), true, `${tool} should be allowed for coder`)
    assert.equal(rolePredicate(tool, 'manager'), true, `${tool} should be allowed for manager`)
    assert.equal(rolePredicate(tool, 'blogger'), false, `${tool} should be denied for blogger`)
    assert.equal(rolePredicate(tool, 'distiller'), false, `${tool} should be denied for distiller`)
  }

  // fission: Fission permission (Manager, Coder, Inspector, Browser, Inquiry)
  assert.equal(rolePredicate('fission', 'manager'), true)
  assert.equal(rolePredicate('fission', 'coder'), true)
  assert.equal(rolePredicate('fission', 'inspector'), true)
  assert.equal(rolePredicate('fission', 'inquiry'), true)
  assert.equal(rolePredicate('fission', 'reviewer'), false)
})

test('WHAT[ENF-009] TOOLSPEC_review_and_finality_tools_have_owner_defined_admission', () => {
  // judge: Reviewer only
  assert.equal(rolePredicate('judge', 'reviewer'), true)
  assert.equal(rolePredicate('judge', 'coder'), false)
  assert.equal(rolePredicate('judge', 'manager'), false)

  // suicide: Manager only
  assert.equal(rolePredicate('suicide', 'manager'), true)
  assert.equal(rolePredicate('suicide', 'reviewer'), false)
  assert.equal(rolePredicate('suicide', 'coder'), false)
})

test('WHAT[ENF-009] TOOLSPEC_unknown_tools_and_host_natives_fail_closed', () => {
  // Host natives and unknown tools fail closed in ToolRegistry rolePredicate
  assert.equal(rolePredicate('nonexistent-tool', 'coder'), false)
  assert.equal(rolePredicate('read', 'coder'), false)
  assert.equal(rolePredicate('write', 'coder'), false)
  assert.equal(rolePredicate('skill', 'coder'), false)
})
