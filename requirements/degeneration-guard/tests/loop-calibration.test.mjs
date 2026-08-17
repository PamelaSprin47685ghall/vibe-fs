import assert from 'node:assert/strict'
import test from 'node:test'
import { countTokens, encode } from 'gpt-tokenizer/encoding/o200k_base'

import * as loopDetector from '../../../dist/Execution/Session/LoopDetectorSurface.js'
import {
  nextPowerOfTwo,
  percentile,
  readableRepositoryTexts,
  weightedDistinctMinimum,
} from './loop-calibration-corpus.mjs'

const close = (actual, expected, tolerance = 1e-9) =>
  assert.ok(Math.abs(actual - expected) <= tolerance, `${actual} != ${expected}`)

test('WHAT[DG-004] LOOP_004_repository_text_calibrates_every_detector_constant', () => {
  const readable = readableRepositoryTexts()
  assert.ok(readable.length > 2000, `readable files=${readable.length}`)

  const nonEmptyLineTokenLengths = []
  for (const [, text] of readable) {
    for (const line of text.split(/\r?\n/)) {
      if (line.trim().length > 0) nonEmptyLineTokenLengths.push(countTokens(line))
    }
  }

  const p99LineTokens = percentile(nonEmptyLineTokenLengths, 0.99)
  const calibratedHalfLife = nextPowerOfTwo(p99LineTokens)
  assert.equal(p99LineTokens, 57)
  assert.equal(calibratedHalfLife, 64)
  assert.equal(loopDetector.halfLife, calibratedHalfLife)
  close(loopDetector.lambda, 2 ** (-1 / calibratedHalfLife))

  const repositoryTokens = encode(readable.map(([, text]) => text).join('\n'))
  assert.ok(repositoryTokens.length > 2_000_000, `repository tokens=${repositoryTokens.length}`)

  const normal = weightedDistinctMinimum(repositoryTokens, calibratedHalfLife)
  const abnormal = 1
  const midpoint = (normal + abnormal) / 2

  close(loopDetector.normalWeightedDistinctCount, normal, 1e-6)
  close(loopDetector.theoreticalLoopWeightedDistinctCount, abnormal)
  close(loopDetector.loopWeightedDistinctThreshold, midpoint, 1e-6)
  assert.ok(normal > midpoint)
  assert.ok(midpoint > abnormal)
})
