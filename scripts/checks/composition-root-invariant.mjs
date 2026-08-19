#!/usr/bin/env node
// Composition-root wiring invariant gate.
//
// HostSignalBootstrap and ToolRegistry are composition roots (host-boundary).
// They must:
//   - only wire/construct/subscribe/route — not decide domain policy
//   - not contain implicit workflow PC
//   - not match on foreign internal domain types to make business decisions
//
// This gate checks for the absence of domain policy decision patterns.

import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'

const ROOT = fileURLToPath(new URL('../..', import.meta.url))

const FILES = [
  join(ROOT, 'src/Wanxiangshu/OpenCode/Host/HostSignalBootstrap.fs'),
  join(ROOT, 'src/Wanxiangshu/OpenCode/Tools/ToolRegistry.fs'),
]

// Patterns that indicate domain policy decisions in a composition root
const domainPolicyPatterns = [
  /\bdecideModelPolicy\b/,
  /\bdecideRecoverySemantics\b/,
  /\bdecideFissionPolicy\b/,
  /\bdecideFinality\b/,
  /\bdecideAssistanceSuccessor\b/,
  /\bdecideStrengthMeaning\b/,
  /\bcalculateFinality\b/,
  /\bclassifyRecovery\b/,
]

// Implicit workflow PC
const pcPatterns = [
  /\bCurrentStage\b/,
  /\bNextStep\b/,
  /\bResumeAt\b/,
  /\bStepIndex\b/,
  /\bContinueToken\b/,
  /\bPendingSecondRetry\b/,
  /\bFallbackPhase\b/,
]

let hasViolations = false

for (const file of FILES) {
  const text = readFileSync(file, 'utf8')
  const relPath = file.replace(ROOT + '/', '')
  const violations = []

  for (const pattern of [...domainPolicyPatterns, ...pcPatterns]) {
    if (pattern.test(text)) {
      violations.push(`forbidden pattern: ${pattern}`)
    }
  }

  if (violations.length > 0) {
    console.error(`composition-root-invariant: ${relPath}`)
    for (const v of violations) console.error(`  ${v}`)
    hasViolations = true
  }
}

if (hasViolations) process.exit(1)

console.log('composition-root-invariant: OK — HostSignalBootstrap and ToolRegistry are wiring-only')
