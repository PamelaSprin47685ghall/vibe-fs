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

test('WHAT[ABL-005] ABL_005_station_05_denies_downstream_tools', () => {
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

test('WHAT[ABL-005] ABL_005_station_14_keeps_coder_surface_and_ablates_manager_tools', () => {
  withEnv([['WANXIANGSHU_ABLATION_PROFILE', 'station-14']], () => {
    Ablation.load()
    assert.equal(Ablation.allowsTool('read'), true)
    assert.equal(Ablation.allowsTool('fork'), false)
    assert.equal(Ablation.allowsPrimaryAgent('manager'), false)
    assert.equal(Ablation.allowsPrimaryAgent('coder'), true)
    assert.equal(Ablation.fissionVisible(), false)
  })
})

test('WHAT[ABL-010] ABL_010_station_05_hides_browser_and_inquiry_agents', () => {
  withEnv([['WANXIANGSHU_ABLATION_PROFILE', 'station-05']], () => {
    Ablation.load()
    assert.equal(Ablation.allowsPrimaryAgent('browser'), false)
    assert.equal(Ablation.allowsPrimaryAgent('inquiry'), false)
  })
})

test('WHAT[ABL-004] ABL_004_station_15_borrows_sync_delegate_slice', () => {
  withEnv([['WANXIANGSHU_ABLATION_PROFILE', 'station-15']], () => {
    const result = Ablation.load()
    assert.equal(result.ok, true)
    assert.equal(result.modes.delegation, 'borrowed')
    assert.equal(result.modes['delegation.sync-delegate'], 'borrowed')
    assert.equal(result.modes['delegation.async-fork'], 'ablated')
    assert.equal(Ablation.allowsTool('fork'), false)
    assert.equal(Ablation.allowsTool('inspect'), true)
  })
})

test('WHAT[ABL-004] ABL_004_station_42_activates_delegation', () => {
  withEnv([['WANXIANGSHU_ABLATION_PROFILE', 'station-42']], () => {
    const result = Ablation.load()
    assert.equal(result.ok, true)
    assert.equal(result.modes.delegation, 'active')
    assert.equal(result.modes['delegation.async-fork'], 'active')
    assert.equal(Ablation.allowsTool('fork'), true)
  })
})

test('WHAT[ABL-004] ABL_004_station_56_is_full_production_surface', () => {
  withEnv([['WANXIANGSHU_ABLATION_PROFILE', 'station-56']], () => {
    const result = Ablation.load()
    assert.equal(result.ok, true)
    assert.equal(result.modes['change-integration'], 'active')
    assert.equal(Ablation.allowsPrimaryAgent('orchestrator'), true)
    assert.equal(Ablation.fissionVisible(), true)
  })
})
