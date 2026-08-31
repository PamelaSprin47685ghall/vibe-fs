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
// Forbidden foreign internal namespace opens in composition roots (AGENTS.md Chapter 26)
const forbiddenInternalOpens = [
  /^\s*open\s+Wanxiangshu\.Mission\.Review\.Judgement\b/m,
  /^\s*open\s+Wanxiangshu\.Mission\.Obligation\.Todo(?!\.OpenCode)\b/m,
  /^\s*open\s+Wanxiangshu\.Mission\.Finality(?!\.OpenCode)\b/m,
  /^\s*open\s+Wanxiangshu\.Mission\.Manager\.Life\b/m,
  /^\s*open\s+Wanxiangshu\.Strength\.Prediction\b/m,
  /^\s*open\s+Wanxiangshu\.Strength\.Replica\b/m,
  /^\s*open\s+Wanxiangshu\.Enforcer\.Guidance\b/m,
  /^\s*open\s+Wanxiangshu\.Enforcer\.Cycle\b/m,
  /^\s*open\s+Wanxiangshu\.Context\.Trace\b/m,
]

const fragmentedChatAdmissionPatterns = [
  /PromptIngress\.create(?:Decision)?Hook/,
  /ModelRouting\.routeChatExecution/,
  /AcquireAndCommitRoutedExecution/,
  /SessionExecutionBinding\.acceptRoutedExecution/,
  /SessionExecutionBinding\.acceptExternalExecution/,
  /SessionExecutionBinding\.acceptPromptExecution/,
  /ModelRouting\.projectRoutedModel/,
]

let hasViolations = false

for (const file of FILES) {
  const text = readFileSync(file, 'utf8')
  const relPath = file.replace(ROOT + '/', '')
  const violations = []

  const patterns = [...domainPolicyPatterns, ...pcPatterns, ...forbiddenInternalOpens]
  if (relPath.endsWith('HostSignalBootstrap.fs')) patterns.push(...fragmentedChatAdmissionPatterns)

  for (const pattern of patterns) {
    if (pattern.test(text)) {
      violations.push(`forbidden pattern: ${pattern}`)
    }
  }

  if (relPath.endsWith('HostSignalBootstrap.fs')) {
    for (const required of [/ChatAdmissionTransaction\.production/, /ChatAdmissionTransaction\.execute/]) {
      if (!required.test(text)) violations.push(`missing required pattern: ${required}`)
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
