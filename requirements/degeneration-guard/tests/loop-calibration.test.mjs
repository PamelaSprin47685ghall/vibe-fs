import assert from 'node:assert/strict'
import { existsSync, readFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import test from 'node:test'
import { fileURLToPath } from 'node:url'
import { countTokens } from 'gpt-tokenizer/encoding/o200k_base'

import * as loopDetector from '../../../dist/Execution/Session/LoopDetectorSurface.js'
import {
  calibrateFromTexts,
  nextPowerOfTwo,
  percentile,
  readableRepositoryTexts,
} from './loop-calibration-corpus.mjs'

const close = (actual, expected, tolerance = 1e-9) =>
  assert.ok(Math.abs(actual - expected) <= tolerance, `${actual} != ${expected}`)

const root = join(dirname(fileURLToPath(import.meta.url)), '../../..')

test('WHAT[DG-004] LOOP_004_repository_text_directly_calibrates_normal_extrema', () => {
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
  assert.equal(loopDetector.halfLife, calibratedHalfLife)
  close(loopDetector.lambda, 2 ** (-1 / calibratedHalfLife))

  // One immutable in-memory snapshot proves the corpus law. Comparing a fresh
  // second filesystem read with a prior build artifact would test workspace
  // timing instead of the calibration algorithm.
  const calibrated = calibrateFromTexts(readable)
  assert.ok(calibrated.tokenCount > 2_000_000, `repository tokens=${calibrated.tokenCount}`)
  assert.equal(calibrated.vocabularySize, loopDetector.vocabularySize)
  assert.equal(calibrated.halfLife, calibratedHalfLife)
  close(calibrated.lambda, 2 ** (-1 / calibratedHalfLife))
  assert.ok(calibrated.minimum < calibrated.normal)
  assert.ok(calibrated.normal < calibrated.maximum)
  assert.ok(calibrated.maximum < calibrated.maxSupport)
})

test('WHAT[DG-004] LOOP_004_calibration_has_no_probability_fit_or_tracked_snapshot', () => {
  const trackedConstants = join(root, 'src/Wanxiangshu/Execution/Session/LoopDetectorConstants.fs')
  assert.equal(existsSync(trackedConstants), false, 'tracked calibration snapshot must not exist')

  const detectorSource = readFileSync(
    join(root, 'src/Wanxiangshu/Execution/Session/LoopDetector.fs'),
    'utf8',
  )
  assert.match(detectorSource, /#wanxiangshu-loop-detector-calibration/)
  assert.doesNotMatch(detectorSource, /beta|quantile|confidence|variance|standardDeviation/i)

  const calibrationSource = readFileSync(join(root, 'scripts/lib/calibrate-loop-detector.mjs'), 'utf8')
  assert.doesNotMatch(calibrationSource, /beta|quantile|confidence|variance|standardDeviation/i)

  const buildSource = readFileSync(join(root, 'scripts/build.mjs'), 'utf8')
  assert.match(buildSource, /writeLoopDetectorCalibrationArtifact\(root, loopDetectorCalibration\)/)

  const generatedArtifact = join(root, 'dist/Execution/Session/LoopDetectorCalibration.js')
  assert.equal(existsSync(generatedArtifact), true, 'build must emit calibration as JS only')
  const generated = readFileSync(generatedArtifact, 'utf8')
  assert.match(generated, /minimumWeightedDistinctCount/)
  assert.match(generated, /maximumWeightedDistinctCount/)
  assert.doesNotMatch(generated, /beta|quantile|confidence|variance|distributionStd/i)
})
