// releaseOnly: true
import assert from 'node:assert/strict'
import test from 'node:test'
import { encode } from 'gpt-tokenizer/encoding/o200k_base'

import * as loopDetector from '../../../../dist/Execution/Session/LoopDetectorSurface.js'
import {
  deriveLoopDetectorEnvelope,
  loadLoopDetectorRepositoryCorpusV1,
  writeLoopDetectorEnvelopeArtifact,
} from '../../../../scripts/lib/derive-loop-detector-envelope.mjs'

const close = (actual, expected, tolerance = 1e-9) =>
  assert.ok(Math.abs(actual - expected) <= tolerance, `${actual} != ${expected}`)

const lowerQuantileProbability = 0.025
const upperQuantileProbability = 1.0

const empiricalQuantile = (values, probability) => {
  const rank = Math.ceil(probability * values.length)
  const index = Math.min(values.length - 1, rank - 1)
  return Float64Array.from(values).sort()[index]
}

const referenceEnvelope = (tokens, lambda, initialValue) => {
  const lastSeen = new Map()
  let weightedDistinctTokens = initialValue
  let sum = 0
  const trajectory = new Float64Array(tokens.length)

  for (let index = 0; index < tokens.length; index += 1) {
    const step = index + 1
    const token = tokens[index]
    const previous = lastSeen.get(token)
    weightedDistinctTokens =
      lambda * weightedDistinctTokens +
      1 -
      (previous === undefined ? 0 : lambda ** (step - previous))
    lastSeen.set(token, step)
    trajectory[index] = weightedDistinctTokens
    sum += weightedDistinctTokens
  }

  return {
    mean: sum / tokens.length,
    minimum: empiricalQuantile(trajectory, lowerQuantileProbability),
    maximum: empiricalQuantile(trajectory, upperQuantileProbability),
  }
}

test('WHAT[DG-004] LOOP_004_runtime_envelope_is_freshly_derived_from_the_current_repository_without_numeric_snapshots', async () => {
  let generatedBytes = null
  const derived = await writeLoopDetectorEnvelopeArtifact(undefined, {
    writeArtifact: (_target, bytes) => { generatedBytes = bytes },
  })

  assert.ok(Buffer.isBuffer(generatedBytes) && generatedBytes.length > 0)
  assert.equal(derived.halfLife, 256)
  close(derived.centralProbability, 0.975)
  close(derived.lowerQuantileProbability, 0.025)
  close(derived.upperQuantileProbability, 1.0)
  close(loopDetector.halfLife, derived.halfLife)
  close(loopDetector.lambda, derived.lambda)
  close(loopDetector.normalWeightedDistinctCount, derived.normalPrior)
  close(loopDetector.centralProbability, derived.centralProbability)
  close(loopDetector.lowerQuantileProbability, derived.lowerQuantileProbability)
  close(loopDetector.upperQuantileProbability, derived.upperQuantileProbability)
  close(loopDetector.minimumWeightedDistinctCount, derived.minimum)
  close(loopDetector.maximumWeightedDistinctCount, derived.maximum)

  const tokens = encode(loadLoopDetectorRepositoryCorpusV1().texts.join('\n'))
  const reference = referenceEnvelope(tokens, derived.lambda, derived.normalPrior)
  close(reference.mean, derived.normalPrior)
  close(reference.minimum, derived.minimum)
  close(reference.maximum, derived.maximum)
})
