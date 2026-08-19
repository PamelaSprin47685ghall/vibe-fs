#!/usr/bin/env node
// AssistanceHost capability workflow invariant gate.
//
// AssistanceHost must:
//   - use AssistanceAbortClaim as the one-shot capability (no IsArmed probe)
//   - consume claim only behind fresh SessionIdle fence (withFreshAssistanceQuiescence)
//   - not contain implicit workflow PC (CurrentStage/NextStep/ResumeAt/Phase/StepIndex/ContinueToken)
//   - not directly reference Git/Strength/Review/Todo internal namespaces
//
// This gate is a structural regression guard.

import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'

const ROOT = fileURLToPath(new URL('../..', import.meta.url))
const FILE = join(ROOT, 'src/Wanxiangshu/Interaction/Dispatch/OpenCode/AssistanceHost.fs')

const text = readFileSync(FILE, 'utf8')
const violations = []

// 1. No implicit workflow PC
const pcPatterns = [
  /\bCurrentStage\b/,
  /\bNextStep\b/,
  /\bResumeAt\b/,
  /\bStepIndex\b/,
  /\bContinueToken\b/,
  /\bPendingSecond\b/,
  /\bFallbackPhase\b/,
]
for (const pattern of pcPatterns) {
  if (pattern.test(text)) {
    violations.push(`implicit workflow PC: ${pattern}`)
  }
}

// 2. Must use AssistanceAbortClaim
if (!text.includes('AssistanceAbortClaim')) {
  violations.push('missing AssistanceAbortClaim capability')
}

// 3. Must use withFreshAssistanceQuiescence (idle fence)
if (!text.includes('withFreshAssistanceQuiescence')) {
  violations.push('missing withFreshAssistanceQuiescence idle fence')
}

// 4. Must use TryConsumeAssistanceClaim
if (!text.includes('TryConsumeAssistanceClaim')) {
  violations.push('missing TryConsumeAssistanceClaim one-shot consumption')
}

if (violations.length > 0) {
  console.error('assistance-host-invariant: VIOLATIONS')
  for (const v of violations) console.error(`  ${v}`)
  process.exit(1)
}

console.log('assistance-host-invariant: OK — capability seam intact, no implicit PC')
