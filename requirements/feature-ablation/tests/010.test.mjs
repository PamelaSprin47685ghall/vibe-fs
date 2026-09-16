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

test('WHAT[ABL-010] ABL_010_station_05_hides_browser_and_inquiry_agents', () => {
  withEnv([['WANXIANGSHU_ABLATION_PROFILE', 'station-05']], () => {
    Ablation.load()
    assert.equal(Ablation.allowsPrimaryAgent('browser'), false)
    assert.equal(Ablation.allowsPrimaryAgent('inquiry'), false)
  })
})
