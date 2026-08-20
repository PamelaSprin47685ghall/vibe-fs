import { fileURLToPath } from 'node:url'

export {
  calibrateFromTexts,
  calibrateFromRepository,
  nextPowerOfTwo,
  percentile,
  readableRepositoryTexts,
} from '../../../scripts/lib/calibrate-loop-detector.mjs'

export const repositoryRoot = fileURLToPath(new URL('../../../', import.meta.url))
