import assert from 'node:assert/strict'
import test from 'node:test'
import { countTokens } from 'gpt-tokenizer/encoding/o200k_base'

import * as loopDetector from '../../../dist/Execution/Session/LoopDetectorSurface.js'
import {
  calibrateFromRepository,
  nextPowerOfTwo,
  percentile,
  readableRepositoryTexts,
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
  assert.equal(p99LineTokens, 60)
  assert.equal(calibratedHalfLife, 64)
  assert.equal(loopDetector.halfLife, calibratedHalfLife)
  close(loopDetector.lambda, 2 ** (-1 / calibratedHalfLife))

  const calibrated = calibrateFromRepository()
  assert.ok(calibrated.tokenCount > 2_000_000, `repository tokens=${calibrated.tokenCount}`)

  assert.equal(loopDetector.vocabularySize, calibrated.vocabularySize)
  assert.equal(loopDetector.halfLife, calibrated.halfLife)
  close(loopDetector.lambda, calibrated.lambda)
  close(loopDetector.maxSupport, calibrated.maxSupport)
  close(loopDetector.distributionMean, calibrated.mean, 1e-6)
  close(loopDetector.distributionVariance, calibrated.variance, 1e-6)
  close(loopDetector.distributionStd, calibrated.std, 1e-6)
  close(loopDetector.betaAlpha, calibrated.betaAlpha, 1e-6)
  close(loopDetector.betaBeta, calibrated.betaBeta, 1e-6)
  close(loopDetector.confidenceLevel, calibrated.confidenceLevel)
  close(loopDetector.confidenceQuantile, calibrated.confidenceQuantile)
  close(loopDetector.normalWeightedDistinctCount, calibrated.normal, 1e-6)
  close(loopDetector.theoreticalLoopWeightedDistinctCount, calibrated.theoreticalLoop)
  close(loopDetector.loopWeightedDistinctThreshold, calibrated.threshold, 1e-6)

  assert.ok(loopDetector.normalWeightedDistinctCount > loopDetector.loopWeightedDistinctThreshold)
  assert.ok(
    loopDetector.loopWeightedDistinctThreshold >
      loopDetector.theoreticalLoopWeightedDistinctCount,
  )
})
