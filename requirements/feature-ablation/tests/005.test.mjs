import assert from 'node:assert/strict'
import test from 'node:test'
import * as Ablation from '../../../dist/Ablation/Surface.js'

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

test('WHAT[feature-ablation-005] ABL_005_station_05_denies_downstream_tools', () => {
  withEnv([['WANXIANGSHU_ABLATION_PROFILE', 'station-05']], () => {
    Ablation.load()
    assert.equal(Ablation.allowsTool('fork'), false)
    assert.equal(Ablation.allowsToolSchema('fork'), false)
    assert.equal(Ablation.allowsTool('fission'), false)
    assert.equal(Ablation.allowsTool('review'), false)
    assert.equal(Ablation.allowsTool('commission'), false)
    assert.equal(Ablation.allowsTool('read'), true)
  })
})

test('WHAT[feature-ablation-005] ABL_005_station_14_keeps_coder_surface_and_ablates_manager_tools', () => {
  withEnv([['WANXIANGSHU_ABLATION_PROFILE', 'station-14']], () => {
    Ablation.load()
    assert.equal(Ablation.allowsTool('read'), true)
    assert.equal(Ablation.allowsTool('fork'), false)
    assert.equal(Ablation.allowsPrimaryAgent('manager'), false)
    assert.equal(Ablation.allowsPrimaryAgent('coder'), true)
    assert.equal(Ablation.fissionVisible(), false)
  })
})
