import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const Ablation = await import("../../../dist/Ablation/Surface.js");

const withEnv = (entries, run) => {
  const previous = Object.fromEntries(entries.map(([name]) => [name, process.env[name]]))
  try {
    for (const [name, value] of entries) {
      if (value === undefined) delete process.env[name]
      else process.env[name] = value
    }
    run()
  } finally {
    for (const [name, value] of Object.entries(previous)) {
      if (value === undefined) delete process.env[name]
      else process.env[name] = value
    }
  }
}

test('WHAT[ABL-011] ABL_011_station_05_denies_ablated_durable_fact_tags', () => {
  withEnv([['WANXIANGSHU_ABLATION_PROFILE', 'station-05']], () => {
    Ablation.load()
    assert.equal(Ablation.allowsFact('AgentFact.Delegation'), false)
    assert.equal(Ablation.allowsFact('AgentFact.Relay'), false)
    assert.equal(Ablation.allowsFact('MagicTodo'), false)
    assert.equal(Ablation.allowsFact('AgentFact.UnmappedFamily'), true)
  })
})
test('WHAT[ABL-011] ABL_011_station_15_borrows_delegation_facts', () => {
  withEnv([['WANXIANGSHU_ABLATION_PROFILE', 'station-15']], () => {
    Ablation.load()
    assert.equal(Ablation.allowsFact('AgentFact.Delegation'), true)
    assert.equal(Ablation.allowsFact('AgentFact.Execution'), true)
    assert.equal(Ablation.allowsFact('AgentFact.Relay'), false)
  })
})
}

{
const { default: assert } = await import("node:assert/strict");
const { readFileSync } = await import("node:fs");
const { join } = await import("node:path");
const { default: test } = await import("node:test");

const ROOT = new URL('../../..', import.meta.url).pathname
const read = (rel) => readFileSync(join(ROOT, rel), 'utf8')
const factMap = JSON.parse(read('resources/ablation/fact-map.json'))
const nodes = JSON.parse(read('resources/ablation/nodes.json'))

test('WHAT[ABL-011] ABL_011_every_mapped_fact_targets_manifest_node', () => {
  for (const [tag, node] of Object.entries(factMap.facts)) {
    assert.ok(nodes.nodes.some((entry) => entry.id === node), `${tag} -> unknown node ${node}`)
  }
})
}
