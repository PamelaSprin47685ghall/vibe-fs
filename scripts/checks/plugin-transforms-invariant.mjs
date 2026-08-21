#!/usr/bin/env node
// PluginTransforms composition-root invariant gate.
//
// PluginTransforms is a Provider Transform Composition Root (host-boundary).
// It must:
//   - use static explicit ordering (no dynamic middleware list)
//   - not contain foreign domain decision helpers (decide/recover/classify/calculate/maintain)
//   - not introduce ITransformMiddleware or pipeline registration patterns
//   - preserve the fixed semantic ordering of normalTransform's 12 named steps
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

// 3. normalTransform semantic ordering lock
const orderingSteps = [
  'beginPhysicalProviderAttemptForTransform',
  'tryBindOrAbort',
  'StrengthReplay.applyBeforeXTrace',
  'XTracePipeline.applyPipeline',
  'applyCompanionForOrdinaryMaterial',
  'XWire.applyTransform',
  'EnforcerContinuation.applyContinuation',
  'StrengthSpeculate.tryApply',
  'PairProgrammingThoughtTransform.maybeInjectGuideline',
  'projectOrTerminate',
  'BloggerChronicleText.maybeInject',
  'sanitizeMessages',
]

const allLines = text.split('\n')
const normalTransformStart = allLines.findIndex(l => /^\s*let\s+(?:private\s+)?normalTransform\b/.test(l))
if (normalTransformStart < 0) {
  violations.push('ordering: normalTransform function not found')
} else {
  const startIndent = allLines[normalTransformStart].length - allLines[normalTransformStart].trimStart().length
  let normalTransformEnd = allLines.length
  for (let i = normalTransformStart + 1; i < allLines.length; i++) {
    const line = allLines[i]
    if (line.trim() === '') continue
    const indent = line.length - line.trimStart().length
    if (indent <= startIndent && /^\s*let\s/.test(line)) {
      normalTransformEnd = i
      break
    }
  }

  const bodyLines = allLines.slice(normalTransformStart, normalTransformEnd)
  const stepLines = []
  for (let i = 0; i < orderingSteps.length; i++) {
    const step = orderingSteps[i]
    const foundIdx = bodyLines.findIndex(l => l.includes(step))
    if (foundIdx < 0) {
      violations.push(`ordering step ${i + 1} not found: ${step}`)
      stepLines.push(-1)
    } else {
      stepLines.push(normalTransformStart + foundIdx + 1)
    }
  }

  for (let i = 0; i < stepLines.length - 1; i++) {
    if (stepLines[i] < 0 || stepLines[i + 1] < 0) continue
    if (stepLines[i] >= stepLines[i + 1]) {
      violations.push(
        `ordering violation: step ${i + 1} (${orderingSteps[i]} at line ${stepLines[i]}) must precede step ${i + 2} (${orderingSteps[i + 1]} at line ${stepLines[i + 1]})`
      )
    }
  }
}

// 4. Explicit TransformMode dispatch — composition root must use typed mode
// Reject implicit helper dispatch patterns that hide mode decision.
const implicitModeHelpers = [
  /let\s+private\s+strengthReplicaRuntime\b/,
  /let\s+private\s+isExplicitResumeProviderMaterial\b/,
  /let\s+private\s+requireReplicaHandled\b/,
  /let\s+private\s+ordinaryProviderTransform\b/,
]
for (const pattern of implicitModeHelpers) {
  if (pattern.test(text)) {
    violations.push(`implicit mode helper still present: ${pattern} — use TransformMode + determineTransformMode + explicit match`)
  }
}
if (!/type\s+private\s+TransformMode\b/.test(text)) {
  violations.push('missing type private TransformMode — composition root must declare explicit mode DU')
}
if (!/let\s+private\s+determineTransformMode\b/.test(text)) {
  violations.push('missing determineTransformMode — mode decision must be named and typed')
}
if (!/match\s+determineTransformMode\b/.test(text)) {
  violations.push('missing explicit match determineTransformMode — dispatch must be match on TransformMode')
}
const modeCases = ['ExplicitResumeDisclosure', 'StrengthReplica', 'Ordinary']
for (const c of modeCases) {
  if (!text.includes(c)) {
    violations.push(`TransformMode case not wired in dispatch: ${c}`)
  }
}

if (violations.length > 0) {
  console.error('plugin-transforms-invariant: VIOLATIONS')
  for (const v of violations) console.error(`  ${v}`)
  process.exit(1)
}

console.log('plugin-transforms-invariant: OK — static composition, no dynamic pipeline, no foreign decisions, ordering locked, TransformMode explicit')
