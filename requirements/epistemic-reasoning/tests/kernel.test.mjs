import assert from 'node:assert/strict'
import test from 'node:test'
import { readFileSync, readdirSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'

import { close } from '../../../dist/Sphinx/Closure.js'
import { createStore, start, resume, state, assessWhy } from './support.mjs'

const here = dirname(fileURLToPath(import.meta.url))
const root = join(here, '../../..')

test('start_yields_semantic_assessment_and_contract_keeps_distribution', () => {
  const store = createStore()
  const started = start(store, '花儿为什么这样红？')
  assert.equal(started.status, 'yield')
  assert.equal(started.request.type, 'SemanticAssessmentRequest')

  const assessed = assessWhy(store, started.handle)
  assert.equal(assessed.status, 'yield')
  assert.equal(assessed.request.type, 'GenerateCandidatesRequest')
  assert.equal(assessed.request.contract.formBelief.Why, 0.8)
  assert.equal(assessed.request.contract.formBelief.How, 0.2)
  assert.equal(assessed.request.contract.contractBelief.Explanation, 0.8)
  assert.equal(assessed.request.contract.contractBelief.Plan, 0.2)
})

test('semantic_assessment_and_candidates_are_control_observations_not_world_evidence', () => {
  const store = createStore()
  const started = start(store, '为什么天空是蓝色？')
  assessWhy(store, started.handle)

  let current = state(store, started.handle)
  assert.equal(current.Evidence.size, 0)
  assert.equal(current.Findings.size, 0)

  const next = resume(store, started.handle, {
    type: 'Candidates',
    items: [
      {
        method: 'Abduction',
        question: '瑞利散射是否足以解释蓝色？',
        semanticKey: 'question:rayleigh',
        expectedRootGain: 0.95,
        cost: 0.2,
      },
    ],
  })
  assert.equal(next.request.type, 'InvestigateRequest')

  current = state(store, started.handle)
  assert.equal(current.Evidence.size, 0)
  assert.equal(current.Findings.size, 0)
})

test('candidate_question_must_be_investigated_before_it_can_affect_answer', () => {
  const store = createStore()
  const started = start(store, '花儿为什么这样红？')
  assessWhy(store, started.handle)

  const candidate = resume(store, started.handle, {
    type: 'Candidates',
    items: [
      {
        method: 'CausalMechanism',
        question: '花青素合成链如何产生红色？',
        semanticKey: 'question:anthocyanin-chain',
        expectedRootGain: 0.9,
        gatewayGain: 0.2,
        cost: 0.2,
      },
    ],
  })

  assert.equal(candidate.status, 'yield')
  assert.equal(candidate.request.type, 'InvestigateRequest')
  assert.equal(candidate.request.action.semanticKey, 'question:anthocyanin-chain')
})

test('resume_rejects_observation_that_does_not_match_pending_kernel_request', () => {
  const store = createStore()
  const started = start(store, '为什么程序卡住？')
  const before = state(store, started.handle).Revision

  const wrong = resume(store, started.handle, {
    type: 'Synthesis',
    text: '跳过调查直接作答',
  })

  assert.equal(wrong.status, 'error')
  assert.match(wrong.error, /expected SemanticAssessment/)
  assert.equal(state(store, started.handle).Revision, before)
})

test('closure_is_idempotent_at_fixed_point', () => {
  const store = createStore()
  const started = start(store, '花儿为什么这样红？')
  assessWhy(store, started.handle)
  const current = state(store, started.handle)
  assert.deepEqual(close(current), current)
})

test('fsharp_kernel_has_no_agent_host_domain_dependency_and_sdk_stays_at_mcp_edge', () => {
  const sourceDir = join(root, 'src/Wanxiangshu/Sphinx')
  const files = readdirSync(sourceDir).filter((name) => name.endsWith('.fs')).sort()
  assert.ok(files.length >= 10)

  for (const name of files) {
    const source = readFileSync(join(sourceDir, name), 'utf8')
    assert.doesNotMatch(source, /open Wanxiangshu\.(Domain|OpenCode|Journal|Session|Agent|Application)/)
    if (name !== 'McpServer.fs') assert.doesNotMatch(source, /@modelcontextprotocol\/sdk|\bzod\b/)
  }

  const project = readFileSync(join(root, 'src/Wanxiangshu/Wanxiangshu.fsproj'), 'utf8')
  assert.match(project, /Sphinx\/Types\.fs/)
  assert.match(project, /Sphinx\/McpServer\.fs/)

  const build = readFileSync(join(root, 'scripts/build.mjs'), 'utf8')
  assert.doesNotMatch(build, /fs\.cpSync\([^\n]*sphinx/i)
  assert.match(build, /dist[^\n]*Sphinx[^\n]*McpServer\.js/)
})
