import { execFileSync } from 'node:child_process'
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'
import { countTokens, encode, vocabularySize } from 'gpt-tokenizer/encoding/o200k_base'

const defaultRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..')

export const CONFIDENCE_LEVEL_95 = 0.95
export const CONFIDENCE_QUANTILE_05 = 0.05

export const nextPowerOfTwo = (value) => 2 ** Math.ceil(Math.log2(Math.max(1, value)))

export const percentile = (values, quantile) => {
  const sorted = [...values].sort((left, right) => left - right)
  return sorted[Math.floor((sorted.length - 1) * quantile)]
}

// Log Gamma function via Lanczos approximation (accuracy ~ 1e-15)
export const logGamma = (z) => {
  const coefficients = [
    0.99999999999980993,
    676.5203681218851,
    -1259.1392167224028,
    771.32342877765313,
    -176.61502916214059,
    12.507343278686905,
    -0.138571095836526,
    9.9843695780195716e-6,
    1.5056327351493116e-7,
  ]
  if (z < 0.5) {
    return Math.log(Math.PI) - Math.log(Math.sin(Math.PI * z)) - logGamma(1 - z)
  }
  let base = z - 1
  let x = coefficients[0]
  for (let index = 1; index < coefficients.length; index += 1) {
    x += coefficients[index] / (base + index)
  }
  const t = base + 7.5
  return 0.5 * Math.log(2 * Math.PI) + (base + 0.5) * Math.log(t) - t + Math.log(x)
}

// Regularized incomplete Beta continued fraction
const betaContinuedFraction = (a, b, x) => {
  const maxIterations = 200
  const epsilon = 1e-15
  const fpmin = 1e-30
  const qab = a + b
  const qap = a + 1
  const qam = a - 1
  let c = 1.0
  let d = 1.0 - (qab * x) / qap
  if (Math.abs(d) < fpmin) d = fpmin
  d = 1.0 / d
  let h = d
  for (let m = 1; m <= maxIterations; m += 1) {
    const m2 = 2 * m
    let numerator = (m * (b - m) * x) / ((qam + m2) * (a + m2))
    d = 1.0 + numerator * d
    if (Math.abs(d) < fpmin) d = fpmin
    c = 1.0 + numerator / c
    if (Math.abs(c) < fpmin) c = fpmin
    d = 1.0 / d
    h *= d * c
    numerator = -((a + m) * (qab + m) * x) / ((a + m2) * (qap + m2))
    d = 1.0 + numerator * d
    if (Math.abs(d) < fpmin) d = fpmin
    c = 1.0 + numerator / c
    if (Math.abs(c) < fpmin) c = fpmin
    d = 1.0 / d
    const delta = d * c
    h *= delta
    if (Math.abs(delta - 1.0) < epsilon) break
  }
  return h
}

// Regularized incomplete Beta function I_x(a, b)
export const regularizedIncompleteBeta = (a, b, x) => {
  if (x <= 0) return 0
  if (x >= 1) return 1
  const logBetaFactor =
    logGamma(a + b) -
    logGamma(a) -
    logGamma(b) +
    a * Math.log(x) +
    b * Math.log(1 - x)
  const factor = Math.exp(logBetaFactor)
  if (x < (a + 1.0) / (a + b + 2.0)) {
    return (factor * betaContinuedFraction(a, b, x)) / a
  }
  return 1.0 - (factor * betaContinuedFraction(b, a, 1.0 - x)) / b
}

// Inverse regularized incomplete Beta function via high-precision bisection
export const betaQuantile = (a, b, probability, tolerance = 1e-14) => {
  if (probability <= 0) return 0
  if (probability >= 1) return 1
  let low = 0
  let high = 1
  for (let step = 0; step < 80; step += 1) {
    const mid = (low + high) / 2
    const current = regularizedIncompleteBeta(a, b, mid)
    if (Math.abs(current - probability) < tolerance) return mid
    if (current < probability) {
      low = mid
    } else {
      high = mid
    }
  }
  return (low + high) / 2
}

export const readableRepositoryTexts = (root = defaultRoot) => {
  const decoder = new TextDecoder('utf-8', { fatal: true })
  const paths = execFileSync('git', [
    '-C',
    root,
    'ls-files',
    '--cached',
    '--others',
    '--exclude-standard',
    '-z',
  ])
    .toString('utf8')
    .split('\0')
    .filter(Boolean)

  const readable = []

  for (const relPath of paths) {
    if (relPath === 'src/Wanxiangshu/FableBarrier.fs' || relPath.endsWith('/FableBarrier.fs')) continue
    try {
      readable.push([relPath, decoder.decode(readFileSync(path.join(root, relPath)))])
    } catch {
      // Strict UTF-8 is the definition of readable text for this calibration.
    }
  }

  return readable
}

export const calibrateFromRepository = (root = defaultRoot) => {
  const readable = readableRepositoryTexts(root)
  const nonEmptyLineTokenLengths = []
  for (const [, text] of readable) {
    for (const line of text.split(/\r?\n/)) {
      if (line.trim().length > 0) nonEmptyLineTokenLengths.push(countTokens(line))
    }
  }

  const p99LineTokens = percentile(nonEmptyLineTokenLengths, 0.99)
  const halfLife = nextPowerOfTwo(p99LineTokens)
  const lambda = 2 ** (-1 / halfLife)
  const maxSupport = 1 / (1 - lambda)
  const theoreticalLoop = 1.0
  const supportSpan = maxSupport - theoreticalLoop

  const concatenatedText = readable.map(([, text]) => text).join('\n')
  const tokens = encode(concatenatedText)

  const lastSeen = new Map()
  let weightedDistinct = maxSupport
  let minimum = weightedDistinct
  let sum = 0
  let sumSq = 0

  for (let index = 0; index < tokens.length; index += 1) {
    const step = index + 1
    const token = tokens[index]
    const previous = lastSeen.get(token)
    weightedDistinct =
      lambda * weightedDistinct + 1 - (previous === undefined ? 0 : lambda ** (step - previous))
    lastSeen.set(token, step)
    if (weightedDistinct < minimum) minimum = weightedDistinct
    sum += weightedDistinct
    sumSq += weightedDistinct * weightedDistinct
  }

  const tokenCount = tokens.length
  const mean = sum / tokenCount
  const variance = sumSq / tokenCount - mean * mean
  const std = Math.sqrt(variance)

  // Beta distribution fit on scaled support u = (D - 1) / (M - 1) in [0, 1]
  const meanU = (mean - theoreticalLoop) / supportSpan
  const varianceU = variance / (supportSpan * supportSpan)
  const sumAlphaBeta = (meanU * (1 - meanU)) / varianceU - 1
  const betaAlpha = meanU * sumAlphaBeta
  const betaBeta = (1 - meanU) * sumAlphaBeta

  // 95% confidence lower tail singularity (5th percentile of Beta distribution)
  const quantileP05 = CONFIDENCE_QUANTILE_05
  const betaQuantileU = betaQuantile(betaAlpha, betaBeta, quantileP05)
  const threshold = theoreticalLoop + supportSpan * betaQuantileU
  const normal = mean

  return {
    vocabularySize,
    halfLife,
    lambda,
    maxSupport,
    tokenCount,
    mean,
    variance,
    std,
    minimum,
    betaAlpha,
    betaBeta,
    confidenceLevel: CONFIDENCE_LEVEL_95,
    confidenceQuantile: quantileP05,
    betaQuantileU,
    threshold,
    normal,
    theoreticalLoop,
  }
}

export const generateLoopDetectorCalibrationArtifactSource = (calibration) => {
  return `// Generated by scripts/build.mjs from the current repository corpus.
// This file is a build artifact. Do not copy these values into production source.
export const vocabularySize = ${calibration.vocabularySize}
export const halfLife = ${calibration.halfLife.toFixed(1)}
export const lambda = ${calibration.lambda.toFixed(16)}
export const maxSupport = ${calibration.maxSupport.toFixed(14)}
export const distributionMean = ${calibration.mean.toFixed(14)}
export const distributionVariance = ${calibration.variance.toFixed(14)}
export const distributionStd = ${calibration.std.toFixed(14)}
export const betaAlpha = ${calibration.betaAlpha.toFixed(14)}
export const betaBeta = ${calibration.betaBeta.toFixed(14)}
export const confidenceLevel = ${calibration.confidenceLevel.toFixed(2)}
export const confidenceQuantile = ${calibration.confidenceQuantile.toFixed(2)}
export const betaQuantileU = ${calibration.betaQuantileU.toFixed(16)}
export const normalWeightedDistinctCount = ${calibration.normal.toFixed(14)}
export const theoreticalLoopWeightedDistinctCount = ${calibration.theoreticalLoop.toFixed(1)}
export const loopWeightedDistinctThreshold = ${calibration.threshold.toFixed(14)}
`
}

export const writeLoopDetectorCalibrationArtifact = (root = defaultRoot, calibration) => {
  const selectedCalibration = calibration ?? calibrateFromRepository(root)
  const source = generateLoopDetectorCalibrationArtifactSource(selectedCalibration)
  const targetDirectory = path.join(root, 'dist/Execution/Session')
  const targetPath = path.join(targetDirectory, 'LoopDetectorCalibration.js')
  mkdirSync(targetDirectory, { recursive: true })
  writeFileSync(targetPath, source, 'utf8')
  return selectedCalibration
}
