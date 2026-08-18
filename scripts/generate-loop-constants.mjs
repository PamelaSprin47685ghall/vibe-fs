#!/usr/bin/env node
// Calibration generator for LoopDetector constants.
// Scans repository tracked strict UTF-8 files, fits a Beta distribution,
// calculates the 95% confidence singularity threshold, and writes LoopDetectorConstants.fs.

import { generateLoopConstantsFile } from './lib/calibrate-loop-detector.mjs'

const calibration = generateLoopConstantsFile()
console.log(
  `[loop-calibration] generated LoopDetectorConstants.fs: mean=${calibration.mean.toFixed(4)}, std=${calibration.std.toFixed(4)}, Beta(α=${calibration.betaAlpha.toFixed(4)}, β=${calibration.betaBeta.toFixed(4)}), threshold=${calibration.threshold.toFixed(4)}`,
)
