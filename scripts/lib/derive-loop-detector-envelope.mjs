import { mkdirSync, writeFileSync } from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'
import { Worker } from 'node:worker_threads'
import { encode, vocabularySize } from 'gpt-tokenizer/encoding/o200k_base'
import { loopDetectorRepositoryTexts } from './loop-detector-repository-corpus.mjs'

const defaultRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..')
const halfLife = 256
// DG-003: empirical quantile envelope. Low-side 97.5% confidence (lower quantile p=0.025)
// and high-side 100% (upper quantile p=1.0, maximum corpus value for random anomaly threshold).
const lowerQuantileProbability = 0.025
const upperQuantileProbability = 1.0
const centralProbability = upperQuantileProbability - lowerQuantileProbability

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

const empiricalQuantile = (values, probability) => {
  if (values.length === 0 || !(probability > 0 && probability <= 1)) {
    throw new Error('Loop detector repository envelope has invalid empirical quantile input')
  }

  const rank = Math.ceil(probability * values.length)
  const index = Math.min(values.length - 1, rank - 1)
  const sorted = Float64Array.from(values).sort()
  return sorted[index]
}

const evaluateEnvelope = (offsets, lambda, normalPrior) => {
  let coefficient = 1
  const projected = new Float64Array(offsets.length)

  for (let index = 0; index < offsets.length; index += 1) {
    coefficient *= lambda
    projected[index] = coefficient * normalPrior + offsets[index]
  }

  return {
    minimum: empiricalQuantile(projected, lowerQuantileProbability),
    maximum: empiricalQuantile(projected, upperQuantileProbability),
  }
}

// encodeParallel splits the corpus only at safe line-boundary positions (after
// '\n', next byte printable non-'/' ASCII) so chunked encoding is token-identical
// to whole-stream encoding: '\n' otherwise merges into the preceding token.
const safeSplitPosition = (text, position) => {
  if (position === 0 || position >= text.length || text[position - 1] !== '\n') return false

  const code = text.charCodeAt(position)
  return code >= 0x21 && code <= 0x7e && code !== 0x2f
}

const chunkRanges = (text, targetChunkCount) => {
  if (targetChunkCount <= 1 || text.length === 0) return [{ begin: 0, end: text.length }]

  const chunks = []
  let begin = 0

  for (let index = 1; index < targetChunkCount; index += 1) {
    let position = Math.max(begin + 1, Math.trunc((text.length * index) / targetChunkCount))
    while (position < text.length && !safeSplitPosition(text, position)) position += 1
    if (position === text.length) break

    chunks.push({ begin, end: position })
    begin = position
  }

  chunks.push({ begin, end: text.length })
  return chunks
}

const encodeInProcess = (text) => Array.from(encode(text))

const encodeWithWorkerThreads = (text, chunks, threadCount) =>
  new Promise((resolve, reject) => {
    const results = new Array(chunks.length)
    let dispatched = 0
    let returned = 0
    let failed = false

    const workerSource = `const { parentPort } = require('node:worker_threads')
const { encode } = require('gpt-tokenizer/encoding/o200k_base')
parentPort.on('message', (message) => {
  if (message.done) {
    process.exit(0)
  }
  const tokens = encode(message.text)
  const packed = new Int32Array(tokens)
  parentPort.postMessage({ index: message.index, packed }, [packed.buffer])
})`

    const settle = (error) => {
      if (failed) return
      if (error) {
        failed = true
        reject(error)
        return
      }
      if (returned === chunks.length) {
        let total = 0
        for (const packed of results) total += packed.length
        const tokens = new Array(total)
        let cursor = 0
        for (const packed of results) {
          for (let i = 0; i < packed.length; i += 1) {
            tokens[cursor] = packed[i]
            cursor += 1
          }
        }
        resolve(tokens)
      }
    }

    const spawnWorker = () => {
      const worker = new Worker(workerSource, { eval: true })
      worker.unref()
      worker.on('message', (message) => {
        results[message.index] = message.packed
        returned += 1
        if (dispatched < chunks.length) {
          const index = dispatched
          dispatched += 1
          const { begin, end } = chunks[index]
          worker.postMessage({ index, text: text.slice(begin, end) })
        } else {
          worker.postMessage({ done: true })
          settle()
        }
      })
      worker.on('error', (error) => {
        settle(error)
        worker.terminate()
      })
      worker.on('exit', (code) => {
        if (code !== 0 && !failed) {
          settle(new Error(`loop detector tokenize worker exited with ${code}`))
        }
      })
      return worker
    }

    const poolSize = Math.min(threadCount, chunks.length)
    for (let workerIndex = 0; workerIndex < poolSize; workerIndex += 1) {
      const worker = spawnWorker()
      const index = dispatched
      dispatched += 1
      const { begin, end } = chunks[index]
      worker.postMessage({ index, text: text.slice(begin, end) })
    }
  })

export const encodeParallel = async (text, workerCount = 0) => {
  if (text.length === 0) return []

  if (workerCount === 0) {
    workerCount = Math.max(1, typeof process.availableParallelism === 'function' ? process.availableParallelism() : 1)
  }
  if (workerCount <= 1) return encodeInProcess(text)

  // Split at 8x candidate positions so the pool stays saturated when the
  // corpus has few safe line boundaries; surplus chunks are work-stolen.
  const chunks = chunkRanges(text, workerCount * 8)
  if (chunks.length <= 1) return encodeInProcess(text)

  return encodeWithWorkerThreads(text, chunks, workerCount)
}

export const deriveLoopDetectorEnvelope = async (root = defaultRoot) => {
  const texts = loopDetectorRepositoryTexts(root)
  const lambda = 2 ** (-1 / halfLife)
  const tokens = await encodeParallel(texts.join('\n'))

  const replay = replayAffine(tokens, lambda)
  const normalPrior = solveNormalPrior(replay)
  const envelope = evaluateEnvelope(replay.offsets, lambda, normalPrior)

  return {
    vocabularySize,
    halfLife,
    lambda,
    centralProbability,
    lowerQuantileProbability,
    upperQuantileProbability,
    normalPrior,
    minimum: envelope.minimum,
    maximum: envelope.maximum,
    corpusTokens: tokens.length,
  }
}

const artifactSource = (envelope) => `// Generated from the current repository SSOT by scripts/build.mjs.
// Ephemeral build input; never hand-edit or copy these values into tracked source.
export const vocabularySize = ${envelope.vocabularySize}
export const halfLife = ${envelope.halfLife.toFixed(1)}
export const lambda = ${envelope.lambda.toFixed(16)}
export const centralProbability = ${envelope.centralProbability.toFixed(3)}
export const lowerQuantileProbability = ${envelope.lowerQuantileProbability.toFixed(3)}
export const upperQuantileProbability = ${envelope.upperQuantileProbability.toFixed(3)}
export const normalWeightedDistinctCount = ${envelope.normalPrior.toFixed(14)}
export const minimumWeightedDistinctCount = ${envelope.minimum.toFixed(14)}
export const maximumWeightedDistinctCount = ${envelope.maximum.toFixed(14)}
export const corpusTokens = ${envelope.corpusTokens}
`

export const writeLoopDetectorEnvelopeArtifact = async (root = defaultRoot) => {
  const envelope = await deriveLoopDetectorEnvelope(root)
  const targetDirectory = path.join(root, 'dist/Execution/Session')
  mkdirSync(targetDirectory, { recursive: true })
  writeFileSync(path.join(targetDirectory, 'LoopDetectorEnvelope.js'), artifactSource(envelope), 'utf8')
  return envelope
}