import assert from 'node:assert/strict'
import { readFileSync, readdirSync } from 'node:fs'
import { join } from 'node:path'
import test from 'node:test'
import * as Ablation from '../../../dist/Ablation/Surface.js'

const ROOT = new URL('../../..', import.meta.url).pathname
const read = (rel) => readFileSync(join(ROOT, rel), 'utf8')
const nodesDoc = JSON.parse(read('resources/ablation/nodes.json'))
const profilesDoc = JSON.parse(read('resources/ablation/profiles.json'))

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

test('WHAT[feature-ablation-004] ABL_004_profiles_cover_all_primary_nodes', () => {
  const primary = nodesDoc.nodes.filter((node) => node.kind === 'package').map((node) => node.id)
  for (const [profileId, profile] of Object.entries(profilesDoc.profiles)) {
    for (const node of primary) {
      assert.ok(profile.modes?.[node], `${profileId} missing ${node}`)
    }
  }
})

test('WHAT[feature-ablation-004] ABL_004_station_profiles_track_segment_unablation', () => {
  const primary = nodesDoc.nodes.filter((node) => node.kind === 'package')
  const segmentEnd = (profileStation) => {
    if (profileStation <= 4) return 4
    if (profileStation <= 14) return 14
    if (profileStation <= 21) return 21
    if (profileStation <= 30) return 30
    if (profileStation <= 41) return 41
    if (profileStation <= 49) return 49
    if (profileStation <= 54) return 54
    return 56
  }
  for (let station = 5; station <= 56; station++) {
    const profile = profilesDoc.profiles[`station-${String(station).padStart(2, '0')}`]
    assert.ok(profile, `missing station profile ${station}`)
    const activeThrough = segmentEnd(station)
    for (const node of primary) {
      const mode = profile.modes[node.id]
      if (node.id === 'delegation' && station >= 15 && station < 42) {
        assert.equal(mode, 'borrowed')
        continue
      }
      if (node.id === 'managed-session-lifecycle' && station >= 15 && station < 42) {
        assert.equal(mode, 'borrowed')
        continue
      }
      if (node.station <= activeThrough) {
        assert.equal(mode, 'active', `${node.id} should be active at station ${station}`)
      } else {
        assert.equal(mode, 'ablated', `${node.id} should stay ablated before segment unlock ${station}`)
      }
    }
  }
})

test('WHAT[feature-ablation-004] ABL_004_production_default_is_all_active', () => {
  withEnv([['WANXIANGSHU_ABLATION_PROFILE', undefined]], () => {
    const result = Ablation.load()
    assert.equal(result.ok, true)
    assert.equal(result.profile, null)
    assert.equal(result.modes.delegation, 'active')
    assert.equal(result.modes['speculative-investigation'], 'active')
  })
})

test('WHAT[feature-ablation-004] ABL_004_station_05_ablates_downstream_packages', () => {
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

test('WHAT[feature-ablation-004] ABL_004_explicit_env_overrides_profile', () => {
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

test('WHAT[feature-ablation-004] ABL_004_station_15_borrows_sync_delegate_slice', () => {
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

test('WHAT[feature-ablation-004] ABL_004_station_42_activates_delegation', () => {
  withEnv([['WANXIANGSHU_ABLATION_PROFILE', 'station-42']], () => {
    const result = Ablation.load()
    assert.equal(result.ok, true)
    assert.equal(result.modes.delegation, 'active')
    assert.equal(result.modes['delegation.async-fork'], 'active')
    assert.equal(Ablation.allowsTool('fork'), true)
  })
})

test('WHAT[feature-ablation-004] ABL_004_station_56_is_full_production_surface', () => {
  withEnv([['WANXIANGSHU_ABLATION_PROFILE', 'station-56']], () => {
    const result = Ablation.load()
    assert.equal(result.ok, true)
    assert.equal(result.modes['change-integration'], 'active')
    assert.equal(Ablation.allowsPrimaryAgent('orchestrator'), true)
    assert.equal(Ablation.fissionVisible(), true)
  })
})

test('WHAT[feature-ablation-004] ABL_004_unknown_profile_fail_closed', () => {
  withEnv([['WANXIANGSHU_ABLATION_PROFILE', 'does-not-exist']], () => {
    const result = Ablation.load()
    assert.equal(result.ok, false)
    assert.equal(result.kind, 'UnknownProfile')
  })
})

test('WHAT[feature-ablation-004] ABL_004_load_exposes_manifest_fingerprint', () => {
  withEnv([['WANXIANGSHU_ABLATION_PROFILE', undefined]], () => {
    const result = Ablation.load()
    assert.equal(result.ok, true)
    assert.match(result.manifestFingerprint, /^[0-9a-f]{12}$/)
  })
})
