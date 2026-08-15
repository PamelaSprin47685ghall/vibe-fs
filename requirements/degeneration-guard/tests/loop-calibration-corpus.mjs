import { execFileSync } from 'node:child_process'
import { readFileSync } from 'node:fs'
import { fileURLToPath } from 'node:url'

export const repositoryRoot = fileURLToPath(new URL('../../../', import.meta.url))

export const readableRepositoryTexts = () => {
  const decoder = new TextDecoder('utf-8', { fatal: true })
  const paths = execFileSync('git', [
    '-C',
    repositoryRoot,
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

  for (const path of paths) {
    try {
      readable.push([path, decoder.decode(readFileSync(`${repositoryRoot}/${path}`))])
    } catch {
      // Strict UTF-8 is the definition of readable text for this calibration.
    }
  }

  return readable
}

export const nextPowerOfTwo = (value) => 2 ** Math.ceil(Math.log2(Math.max(1, value)))

export const percentile = (values, quantile) => {
  const sorted = [...values].sort((left, right) => left - right)
  return sorted[Math.floor((sorted.length - 1) * quantile)]
}

export const weightedDistinctMinimum = (tokens, halfLife) => {
  const lambda = 2 ** (-1 / halfLife)
  const lastSeen = new Map()
  let weightedDistinct = 1 / (1 - lambda)
  let minimum = weightedDistinct

  for (let index = 0; index < tokens.length; index += 1) {
    const step = index + 1
    const token = tokens[index]
    const previous = lastSeen.get(token)
    weightedDistinct =
      lambda * weightedDistinct + 1 - (previous === undefined ? 0 : lambda ** (step - previous))
    lastSeen.set(token, step)
    minimum = Math.min(minimum, weightedDistinct)
  }

  return minimum
}
