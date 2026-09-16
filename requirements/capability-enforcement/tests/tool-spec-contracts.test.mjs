// requirements/capability-enforcement/tests/tool-spec-contracts.test.mjs
//
// ENF-002 / ENF-009 / ENF-022 / ENF-023 / ENF-024: ToolSpec + Admission contracts.
// Each tool owner defines its own ToolSpec with typed Admission.
// ToolRegistry is an aggregate + Host projection and delegates admission to spec.Admission.

import assert from 'node:assert/strict'
import test from 'node:test'

import { rolePredicate } from '../../../dist/OpenCode/Tools/ToolRegistrySurface.js'

test('WHAT[ENF-002] TOOLSPEC_delegation_tools_have_owner_defined_admission', () => {
  // fork & resume: Manager only
  assert.equal(rolePredicate('fork', 'manager'), true)
  assert.equal(rolePredicate('fork', 'engineer'), false)
  assert.equal(rolePredicate('fork', 'orchestrator'), false)
  assert.equal(rolePredicate('fork', 'devops'), false)
  assert.equal(rolePredicate('resume', 'manager'), true)
  assert.equal(rolePredicate('resume', 'engineer'), false)
  assert.equal(rolePredicate('resume', 'orchestrator'), false)
  assert.equal(rolePredicate('resume', 'devops'), false)

  // commission: Orchestrator only
  assert.equal(rolePredicate('commission', 'orchestrator'), true)
  assert.equal(rolePredicate('commission', 'manager'), false)
  assert.equal(rolePredicate('commission', 'engineer'), false)

  // join & horizon: Join/Horizon permissions
  assert.equal(rolePredicate('join', 'manager'), true)
  assert.equal(rolePredicate('join', 'orchestrator'), true)
  assert.equal(rolePredicate('join', 'devops'), true)
  assert.equal(rolePredicate('join', 'engineer'), false)

  assert.equal(rolePredicate('horizon', 'manager'), true)
  assert.equal(rolePredicate('horizon', 'orchestrator'), true)
  assert.equal(rolePredicate('horizon', 'devops'), true)
  assert.equal(rolePredicate('horizon', 'engineer'), false)
})

test('WHAT[ENF-002] TOOLSPEC_engineer_and_devops_tools_have_owner_defined_admission', () => {
  // bash-honeypot: Engineer only
  assert.equal(rolePredicate('bash-honeypot', 'engineer'), true)
  assert.equal(rolePredicate('bash-honeypot', 'devops'), false)
  assert.equal(rolePredicate('bash-honeypot', 'manager'), false)

  // mv & rm: Engineer and DevOps (Move / Remove permission)
  assert.equal(rolePredicate('mv', 'engineer'), true)
  assert.equal(rolePredicate('mv', 'devops'), true)
  assert.equal(rolePredicate('mv', 'manager'), false)
  assert.equal(rolePredicate('rm', 'engineer'), true)
  assert.equal(rolePredicate('rm', 'devops'), true)
  assert.equal(rolePredicate('rm', 'manager'), false)

  // pty tools: DevOps only
  assert.equal(rolePredicate('open-terminal', 'devops'), true)
  assert.equal(rolePredicate('open-terminal', 'engineer'), false)
  assert.equal(rolePredicate('send-terminal', 'devops'), true)
  assert.equal(rolePredicate('read-terminal', 'devops'), true)
  assert.equal(rolePredicate('signal-terminal', 'devops'), true)

  // run: DevOps only
  assert.equal(rolePredicate('run', 'devops'), true)
  assert.equal(rolePredicate('run', 'engineer'), false)
  assert.equal(rolePredicate('run', 'manager'), false)
})

test('WHAT[ENF-022] TOOLSPEC_fission_is_exclusive_to_engineer', () => {
  assert.equal(rolePredicate('fission', 'engineer'), true, 'Engineer is admitted for Fission')
  assert.equal(rolePredicate('fission', 'manager'), false, 'Manager must be denied Fission')
  assert.equal(rolePredicate('fission', 'orchestrator'), false, 'Orchestrator must be denied Fission')
  assert.equal(rolePredicate('fission', 'devops'), false, 'DevOps must be denied Fission')
  assert.equal(rolePredicate('fission', 'blogger'), false, 'Blogger must be denied Fission')
})

test('WHAT[ENF-002] TOOLSPEC_cognitive_utility_tools_admission', () => {
  for (const tool of ['assume', 'enough', 'abandon', 'defer', 'subscribe', 'publish', 'celebrate', 'regret']) {
    assert.equal(rolePredicate(tool, 'engineer'), true, `${tool} should be allowed for engineer`)
    assert.equal(rolePredicate(tool, 'devops'), true, `${tool} should be allowed for devops`)
    assert.equal(rolePredicate(tool, 'manager'), true, `${tool} should be allowed for manager`)
    assert.equal(rolePredicate(tool, 'blogger'), false, `${tool} should be denied for blogger`)
  }
})

test('WHAT[ENF-009] TOOLSPEC_review_and_finality_tools_have_owner_defined_admission', () => {
  // review: Manager only (Relay)
  assert.equal(rolePredicate('review', 'manager'), true)
  assert.equal(rolePredicate('review', 'engineer'), false)

  // suicide: Manager only
  assert.equal(rolePredicate('suicide', 'manager'), true)
  assert.equal(rolePredicate('suicide', 'engineer'), false)
})

test('WHAT[ENF-009] TOOLSPEC_unknown_tools_and_host_natives_fail_closed', () => {
  assert.equal(rolePredicate('nonexistent-tool', 'engineer'), false)
  assert.equal(rolePredicate('read', 'engineer'), false)
  assert.equal(rolePredicate('write', 'engineer'), false)
  assert.equal(rolePredicate('skill', 'engineer'), false)
})
