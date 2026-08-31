#!/usr/bin/env node

import { existsSync, readFileSync } from 'node:fs'
import { dirname, join, relative, resolve } from 'node:path'
import { fileURLToPath, pathToFileURL } from 'node:url'
import { walk } from '../lib/walk.mjs'

const here = dirname(fileURLToPath(import.meta.url))
const repositoryRoot = resolve(here, '../..')
const normalize = (path) => path.replaceAll('\\', '/')

const sourceFiles = (root) => {
  const sourceRoot = join(root, 'src/Wanxiangshu')
  if (!existsSync(sourceRoot)) return []
  return [...walk(sourceRoot)]
    .filter((path) => path.endsWith('.fs'))
    .map((path) => ({ path: normalize(relative(root, path)), text: readFileSync(path, 'utf8') }))
}

export const scanRetryOwnership = (root) => {
  const files = sourceFiles(root)
  const violations = []
  const owners = files.filter(({ text }) => /module\s+ExecutionFailurePolicy\s*=/.test(text))

  if (owners.length !== 1 || owners[0]?.path !== 'src/Wanxiangshu/Execution/Failure/Policy.fs') {
    violations.push(`ExecutionFailurePolicy owner must be exactly src/Wanxiangshu/Execution/Failure/Policy.fs; found ${owners.map(({ path }) => path).join(', ') || 'none'}`)
  }

  const model = files.find(({ path }) => path === 'src/Wanxiangshu/Execution/Failure/Model.fs')?.text ?? ''
  if (!/type\s+ProviderRecoveryAuthorization\s+private/.test(model)) {
    violations.push('ProviderRecoveryAuthorization must have a private constructor')
  }

  for (const { path, text } of files) {
    if (/\b(?:record|admit)ConfirmedFailure\b/.test(text) && !path.endsWith('Surface.fs')) {
      violations.push(`${path}: confirmed provider failure bypasses typed policy authorization`)
    }

    if (/FallbackLedger\.(?:record|admit)AuthorizedFailure/.test(text)) {
      const allowed =
        path === 'src/Wanxiangshu/Participant/Provider/Attempt/Fallback/Workflow.fs' ||
        path === 'src/Wanxiangshu/Participant/Provider/Attempt/Fallback/HandleSurface.fs' ||
        path === 'src/Wanxiangshu/Participant/Provider/Attempt/Fallback/Surface.fs'
      if (!allowed) violations.push(`${path}: unauthorized fallback ledger caller`)
    }

    const retryScope =
      path.startsWith('src/Wanxiangshu/Execution/Failure/') ||
      path.startsWith('src/Wanxiangshu/Participant/Provider/Attempt/') ||
      path.startsWith('src/Wanxiangshu/Interaction/Dispatch/') ||
      path.startsWith('src/Wanxiangshu/Execution/Session/Recovery/') ||
      path === 'src/Wanxiangshu/Composition/Turn/Scheduler.fs' ||
      path === 'src/Wanxiangshu/OpenCode/Signals/HostSignal.fs'
    if (!retryScope) continue

    const lines = text.split('\n')
    for (let index = 0; index < lines.length; index += 1) {
      const line = lines[index]
      const decisionWindow = lines.slice(Math.max(0, index - 3), index + 4).join('\n')
      if (
        /(?:Contains|IndexOf|StartsWith|EndsWith|Regex|IsMatch)\s*\(/.test(line) &&
        /retry|fallback|transient|permanent|queue\s*full|acceptance\s*unknown/i.test(decisionWindow)
      ) {
        violations.push(`${path}:${index + 1}: retry/fallback classification must not parse text`)
      }

      if (!/SendPrompt\s*\(|sendContinuation\s+|recordAuthorizedFailure\s+/.test(line)) continue
      const ownerWindow = lines.slice(Math.max(0, index - 24), index + 1).join('\n')
      if (/\bwhile\b|\bfor\b[^\n]*\bdo\b/.test(ownerWindow)) {
        violations.push(`${path}:${index + 1}: physical attempt is nested under a local retry loop`)
      }
    }
  }

  for (const hostPath of [
    'src/Wanxiangshu/OpenCode/Signals/HostSignal.fs',
    'src/Wanxiangshu/Composition/Turn/Scheduler.fs',
    'src/Wanxiangshu/Execution/Session/Recovery/Workflow.fs',
    'src/Wanxiangshu/Interaction/Dispatch/Send.fs',
  ]) {
    const text = files.find(({ path }) => path === hostPath)?.text ?? ''
    if (/FallbackLedger\.|RetryFreshAttempt\s*\(|AdvanceFallback\s*\(/.test(text)) {
      violations.push(`${hostPath}: infrastructure signal/send/recovery boundary must not license retry or fallback`)
    }
  }

  return violations
}

const isMain = process.argv[1] && pathToFileURL(resolve(process.argv[1])).href === import.meta.url
if (isMain) {
  const violations = scanRetryOwnership(repositoryRoot)
  if (violations.length > 0) {
    console.error('retry-owner FAILED:')
    for (const violation of violations) console.error(`  - ${violation}`)
    process.exit(1)
  }
  console.log('retry-owner OK')
}
