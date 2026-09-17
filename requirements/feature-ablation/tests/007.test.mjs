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

test('WHAT[ABL-007] ABL_007_load_exposes_manifest_fingerprint', () => {
  withEnv([['WANXIANGSHU_ABLATION_PROFILE', undefined]], () => {
    const result = Ablation.load()
    assert.equal(result.ok, true)
    assert.match(result.manifestFingerprint, /^[0-9a-f]{12}$/)
  })
})
