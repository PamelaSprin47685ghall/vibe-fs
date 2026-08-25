import { mkdirSync, writeFileSync } from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'
import { encode, vocabularySize } from 'gpt-tokenizer/encoding/o200k_base'
import { loopDetectorRepositoryTexts } from './loop-detector-repository-corpus.mjs'

const defaultRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..')
const halfLife = 256

const replay = (tokens, lambda, initialValue) => {
  const lastSeen = new Map()
  let weightedDistinct = initialValue
  let minimum = Number.POSITIVE_INFINITY
  let maximum = Number.NEGATIVE_INFINITY
  let sum = 0

  for (let index = 0; index < tokens.length; index += 1) {
    const step = index + 1
    const token = tokens[index]
    const previous = lastSeen.get(token)
    weightedDistinct =
      lambda * weightedDistinct + 1 - (previous === undefined ? 0 : lambda ** (step - previous))
    lastSeen.set(token, step)
    minimum = Math.min(minimum, weightedDistinct)
    maximum = Math.max(maximum, weightedDistinct)
    sum += weightedDistinct
  }

  if (tokens.length === 0) throw new Error('Loop detector repository corpus has no tokens')
  return { mean: sum / tokens.length, minimum, maximum }
}

export const deriveLoopDetectorEnvelope = (root = defaultRoot) => {
  const texts = loopDetectorRepositoryTexts(root)
  const lambda = 2 ** (-1 / halfLife)
  const maxSupport = 1 / (1 - lambda)
  const tokens = encode(texts.join('\n'))

  const normalPrior = replay(tokens, lambda, maxSupport).mean
  const envelope = replay(tokens, lambda, normalPrior)

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
