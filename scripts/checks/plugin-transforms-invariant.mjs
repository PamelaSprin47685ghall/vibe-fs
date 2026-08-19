#!/usr/bin/env node
// PluginTransforms composition-root invariant gate.
//
// PluginTransforms is a Provider Transform Composition Root (host-boundary).
// It must:
//   - use static explicit ordering (no dynamic middleware list)
//   - not contain foreign domain decision helpers (decide/recover/classify/calculate/maintain)
//   - not introduce ITransformMiddleware or pipeline registration patterns
//
// This gate is a structural regression guard, not a semantic proof.

import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'

const ROOT = fileURLToPath(new URL('../..', import.meta.url))
const FILE = join(ROOT, 'src/Wanxiangshu/OpenCode/Plugin/PluginTransforms.fs')

const text = readFileSync(FILE, 'utf8')
const violations = []

// 1. No dynamic middleware/pipeline patterns
const dynamicPatterns = [
  /ITransformMiddleware/,
  /ITransform\b/,
  /pipeline\s*\.\s*(Insert|Add|Register|Remove)/,
  /List\.map\s+apply\b/,
  /List\.iter\s+apply\b/,
  /MiddlewarePipeline/,
  /DecoratorBase/,
  /IWorkflowDecorator/,
]
for (const pattern of dynamicPatterns) {
  if (pattern.test(text)) {
    violations.push(`dynamic pipeline pattern: ${pattern}`)
  }
}

// 2. No foreign domain decision helpers
const forbiddenHelpers = [
  /let\s+private\s+decide[A-Z]/,
  /let\s+private\s+recover[A-Z]/,
  /let\s+private\s+classify[A-Z]/,
  /let\s+private\s+calculate[A-Z]/,
  /let\s+private\s+maintain[A-Z]/,
]
for (const pattern of forbiddenHelpers) {
  if (pattern.test(text)) {
    violations.push(`foreign domain decision helper: ${pattern}`)
  }
}

if (violations.length > 0) {
  console.error('plugin-transforms-invariant: VIOLATIONS')
  for (const v of violations) console.error(`  ${v}`)
  process.exit(1)
}

console.log('plugin-transforms-invariant: OK — static composition, no dynamic pipeline, no foreign decisions')
