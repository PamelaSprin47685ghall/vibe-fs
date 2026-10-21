import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import test from 'node:test'
import * as Ablation from '../../../dist/Ablation/Surface.js'
import { withAblationEnv } from './support/ablation-fixture.mjs'

const profiles = JSON.parse(readFileSync(new URL('../../../resources/ablation/profiles.json', import.meta.url), 'utf8')).profiles
const nodes = JSON.parse(readFileSync(new URL('../../../resources/ablation/nodes.json', import.meta.url), 'utf8'))
const tools = Object.keys(JSON.parse(readFileSync(new URL('../../../resources/ablation/tool-map.json', import.meta.url), 'utf8')).tools)
const facts = Object.keys(JSON.parse(readFileSync(new URL('../../../resources/ablation/fact-map.json', import.meta.url), 'utf8')).facts)

const assertMappedAdmission = (allowed) => {
  for (const tool of tools) {
    assert.equal(Ablation.allowsTool(tool), allowed, `execution: ${tool}`)
    assert.equal(Ablation.allowsToolSchema(tool), allowed, `schema: ${tool}`)
  }
  for (const fact of facts) assert.equal(Ablation.allowsFact(fact), allowed, `fact: ${fact}`)
  for (const agent of ['manager', 'orchestrator', 'inquiry']) {
    assert.equal(Ablation.allowsPrimaryAgent(agent), allowed, `primary agent: ${agent}`)
  }
  assert.equal(Ablation.fissionVisible(), allowed)
  assert.equal(Ablation.allowsPrimaryAgent('browser'), false)
  assert.equal(Ablation.allowsTool('unmapped-ablation-tool'), true)
  assert.equal(Ablation.allowsToolSchema('unmapped-ablation-tool'), true)
  assert.equal(Ablation.allowsFact('AgentFact.UnmappedFamily'), true)
  assert.equal(Ablation.allowsFact('MagicTodo'), true)
  assert.equal(Ablation.allowsPrimaryAgent('coder'), true)
}

const withEnv = (entries, run) => {
  try {
    Ablation.resetRegistry()
    return withAblationEnv(entries, run)
  } finally {
    Ablation.resetRegistry()
  }
}

test('WHAT[feature-ablation-004] every declared profile loads its configured modes through the public surface', () => {
  assert.deepEqual(Ablation.profileIds().sort(), Object.keys(profiles).sort())
  for (const [id, profile] of Object.entries(profiles)) {
    withEnv([['WANXIANGSHU_ABLATION_PROFILE', id]], () => {
      const result = Ablation.load()
      assert.equal(result.ok, true, id)
      assert.equal(result.profile, id)
      for (const [node, mode] of Object.entries(profile.modes)) {
        assert.equal(result.modes[node], mode, `${id}: ${node}`)
      }
    })
  }
})

test('WHAT[feature-ablation-004] a valid explicit mode overrides the selected profile', () => {
  withEnv([
    ['WANXIANGSHU_ABLATION_PROFILE', 'production'],
    ['WANXIANGSHU_ABLATION_change_integration', 'borrowed'],
  ], () => {
    const result = Ablation.load()
    assert.equal(result.ok, true)
    assert.equal(result.modes['change-integration'], 'borrowed')
  })
})

test('WHAT[feature-ablation-004] an override cannot bypass DAG validation', () => {
  withEnv([
    ['WANXIANGSHU_ABLATION_PROFILE', 'station-05'],
    ['WANXIANGSHU_ABLATION_delegation', 'active'],
  ], () => {
    const result = Ablation.load()
    assert.equal(result.ok, false)
    assert.equal(result.kind, 'DagViolation')
    assert.equal(Ablation.installed(), null)
    assertMappedAdmission(false)
  })
})

test('WHAT[feature-ablation-004] an unknown profile is rejected', () => {
  withEnv([['WANXIANGSHU_ABLATION_PROFILE', 'does-not-exist']], () => {
    const result = Ablation.load()
    assert.equal(result.ok, false)
    assert.equal(result.kind, 'UnknownProfile')
    assert.equal(Ablation.installed(), null)
    assertMappedAdmission(false)
  })
})

test('WHAT[feature-ablation-004] an invalid configured mode leaves every mapped gate denied after rejection', () => {
  withEnv([['WANXIANGSHU_ABLATION_work', 'invalid-mode']], () => {
    assert.throws(() => Ablation.load(), /InvalidMode/)
    assert.equal(Ablation.installed(), null)
    assertMappedAdmission(false)
  })
})

test('WHAT[feature-ablation-004] no configuration explicitly activates all known nodes and gates', () => {
  withEnv([], () => {
    assertMappedAdmission(true)
    assert.equal(Ablation.installed(), null, 'ambient resolution does not install a registry')
    const loaded = Ablation.load()
    assert.equal(loaded.ok, true)
    assert.deepEqual(Object.keys(loaded.modes).sort(), nodes.nodes.map(node => node.id).sort())
    assert.ok(Object.values(loaded.modes).every(mode => mode === 'active'))
    assertMappedAdmission(true)
  })
})

test('WHAT[feature-ablation-004] an explicitly supplied valid registry controls gates without reading invalid ambient configuration', () => {
  withEnv([['WANXIANGSHU_ABLATION_PROFILE', 'does-not-exist']], () => {
    const registry = Ablation.registryFromNodes(nodes)
    assert.notEqual(registry, null)
    Ablation.useRegistry(registry)
    assert.notEqual(Ablation.installed(), null)
    assertMappedAdmission(true)
  })
})
