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
