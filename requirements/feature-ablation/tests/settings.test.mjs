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

test('WHAT[ABL-006] ABL_006_unknown_profile_fail_closed', () => {
  withEnv([['WANXIANGSHU_ABLATION_PROFILE', 'does-not-exist']], () => {
    const result = Ablation.load()
    assert.equal(result.ok, false)
    assert.equal(result.kind, 'UnknownProfile')
  })
})

test('WHAT[ABL-004] ABL_004_production_default_is_all_active', () => {
  withEnv([['WANXIANGSHU_ABLATION_PROFILE', undefined]], () => {
    const result = Ablation.load()
    assert.equal(result.ok, true)
    assert.equal(result.profile, null)
    assert.equal(result.modes.delegation, 'active')
    assert.equal(result.modes['speculative-investigation'], 'active')
  })
})

test('WHAT[ABL-004] ABL_004_station_05_ablates_downstream_packages', () => {
  withEnv([['WANXIANGSHU_ABLATION_PROFILE', 'station-05']], () => {
    const result = Ablation.load()
    assert.equal(result.ok, true)
    assert.equal(result.profile, 'station-05')
    assert.equal(result.modes.delegation, 'ablated')
    assert.equal(result.modes['relay-incumbency'], 'ablated')
    assert.equal(result.modes['change-integration'], 'ablated')
    assert.equal(result.modes['host-boundary'], 'active')
  })
})

test('WHAT[ABL-004] ABL_004_explicit_env_overrides_profile', () => {
  withEnv(
    [
      ['WANXIANGSHU_ABLATION_PROFILE', 'station-05'],
      ['WANXIANGSHU_ABLATION_delegation', 'active'],
    ],
    () => {
      const result = Ablation.load()
      assert.equal(result.ok, false)
      assert.equal(result.kind, 'DagViolation')
    },
  )
})

test('WHAT[ABL-008] ABL_008_strength_forced_off_when_speculation_ablated', () => {
  withEnv([['WANXIANGSHU_ABLATION_PROFILE', 'station-05']], () => {
    Ablation.load()
    assert.equal(Ablation.strengthForcedOff(), true)
  })
})
