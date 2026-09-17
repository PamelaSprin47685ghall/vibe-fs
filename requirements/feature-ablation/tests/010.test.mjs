import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { readFileSync } = await import("node:fs");
const { join } = await import("node:path");
const { default: test } = await import("node:test");

const ROOT = new URL('../../..', import.meta.url).pathname
const read = (rel) => readFileSync(join(ROOT, rel), 'utf8')

test('WHAT[ABL-010] Sphinx (epistemic-reasoning) has an independent ablation switch and is not disabled by browser removal', () => {
  const nodes = JSON.parse(read('resources/ablation/nodes.json'))
  
  // epistemic-reasoning node must exist as an independent node
  const sphinxNode = nodes.nodes.find((n) => n.id === 'epistemic-reasoning')
  assert.ok(sphinxNode, 'epistemic-reasoning node must exist in ablation nodes')

  // epistemic-reasoning must not have dependency on external-investigation
  const edges = nodes.edges || []
  const hasBrowserDep = edges.some((e) => e.to === 'epistemic-reasoning' && e.from === 'external-investigation')
  assert.equal(hasBrowserDep, false, 'epistemic-reasoning must not depend on external-investigation')
})
}

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

test('WHAT[ABL-010] ABL_010_station_05_hides_browser_and_inquiry_agents', () => {
  withEnv([['WANXIANGSHU_ABLATION_PROFILE', 'station-05']], () => {
    Ablation.load()
    assert.equal(Ablation.allowsPrimaryAgent('browser'), false)
    assert.equal(Ablation.allowsPrimaryAgent('inquiry'), false)
  })
})
}
