import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import test from 'node:test'
import * as Ablation from '../../../dist/Ablation/Surface.js'
import { withAblationEnv } from './support/ablation-fixture.mjs'

const profiles = JSON.parse(readFileSync(new URL('../../../resources/ablation/profiles.json', import.meta.url), 'utf8')).profiles
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
  })
})

test('WHAT[feature-ablation-004] an unknown profile is rejected', () => {
  withEnv([['WANXIANGSHU_ABLATION_PROFILE', 'does-not-exist']], () => {
    const result = Ablation.load()
    assert.equal(result.ok, false)
    assert.equal(result.kind, 'UnknownProfile')
  })
})
