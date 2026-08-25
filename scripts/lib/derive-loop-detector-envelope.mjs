import { mkdirSync, writeFileSync } from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'
import { encode, vocabularySize } from 'gpt-tokenizer/encoding/o200k_base'
import { loopDetectorRepositoryTexts } from './loop-detector-repository-corpus.mjs'

const defaultRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..')
const halfLife = 256

const replayAffine = (tokens, lambda) => {
  if (tokens.length === 0) throw new Error('Loop detector repository corpus has no tokens')

  const lastSeen = new Map()
  const offsets = new Float64Array(tokens.length)
  let coefficient = 1
  let offset = 0
  let coefficientSum = 0
  let offsetSum = 0

  for (let index = 0; index < tokens.length; index += 1) {
    const step = index + 1
    const token = tokens[index]
    const previous = lastSeen.get(token)
    const replacement = 1 - (previous === undefined ? 0 : lambda ** (step - previous))
    coefficient *= lambda
    offset = lambda * offset + replacement
    lastSeen.set(token, step)
    offsets[index] = offset
    coefficientSum += coefficient
    offsetSum += offset
  }

  return { offsets, coefficientSum, offsetSum }
}

const solveNormalPrior = (replay) => {
  const meanCoefficient = replay.coefficientSum / replay.offsets.length
  const meanOffset = replay.offsetSum / replay.offsets.length
  if (!(meanCoefficient < 1)) throw new Error('Loop detector repository corpus has invalid affine coefficient')
  return meanOffset / (1 - meanCoefficient)
}

const evaluateEnvelope = (offsets, lambda, normalPrior) => {
  let coefficient = 1
  let minimum = Number.POSITIVE_INFINITY
  let maximum = Number.NEGATIVE_INFINITY

  for (const offset of offsets) {
    coefficient *= lambda
    const weightedDistinct = coefficient * normalPrior + offset
    minimum = Math.min(minimum, weightedDistinct)
    maximum = Math.max(maximum, weightedDistinct)
  }

  return { minimum, maximum }
}

export const deriveLoopDetectorEnvelope = (root = defaultRoot) => {
  const texts = loopDetectorRepositoryTexts(root)
  const lambda = 2 ** (-1 / halfLife)
  const tokens = encode(texts.join('\n'))

  const replay = replayAffine(tokens, lambda)
  const normalPrior = solveNormalPrior(replay)
  const envelope = evaluateEnvelope(replay.offsets, lambda, normalPrior)

  return {
    vocabularySize,
    halfLife,
    lambda,
    normalPrior,
    minimum: envelope.minimum,
    maximum: envelope.maximum,
  }
}

const artifactSource = (envelope) => `// Generated from the current repository SSOT by scripts/build.mjs.
// Ephemeral build input; never hand-edit or copy these values into tracked source.
export const vocabularySize = ${envelope.vocabularySize}
export const halfLife = ${envelope.halfLife.toFixed(1)}
export const lambda = ${envelope.lambda.toFixed(16)}
export const normalWeightedDistinctCount = ${envelope.normalPrior.toFixed(14)}
export const minimumWeightedDistinctCount = ${envelope.minimum.toFixed(14)}
export const maximumWeightedDistinctCount = ${envelope.maximum.toFixed(14)}
`

export const writeLoopDetectorEnvelopeArtifact = (root = defaultRoot) => {
  const envelope = deriveLoopDetectorEnvelope(root)
  const targetDirectory = path.join(root, 'dist/Execution/Session')
  mkdirSync(targetDirectory, { recursive: true })
  writeFileSync(path.join(targetDirectory, 'LoopDetectorEnvelope.js'), artifactSource(envelope), 'utf8')
  return envelope
}
