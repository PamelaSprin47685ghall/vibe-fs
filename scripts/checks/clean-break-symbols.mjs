#!/usr/bin/env node
// Production src/ must not carry the retired clean-break symbols.
// AgentJournal stays legal. Whether AgentJournal.appendAgent is a lifecycle write is not a regex decision.

import { readFileSync } from 'node:fs'
import { relative, resolve } from 'node:path'
import { pathToFileURL } from 'node:url'
import { walk } from '../lib/walk.mjs'

export const SCAN_ROOT = 'src'
export const SCRIPT_SUFFIX = 'checks/clean-break-symbols.mjs'

export const FORBIDDEN = Object.freeze([
  { id: 'QualityCertificate', re: /\bQualityCertificate\b/ },
  { id: 'WorkspaceSnapshotId', re: /\bWorkspaceSnapshotId\b/ },
  { id: 'WorkspaceSnapshot', re: /\bWorkspaceSnapshot\b/ },
  { id: 'suicide', re: /\bsuicide\b/ },
  { id: 'RoadOpened', re: /\bRoadOpened\b/ },
  { id: 'AssessmentCommitted', re: /\bAssessmentCommitted\b/ },
  { id: 'RetirementConfirmationCommitted', re: /\bRetirementConfirmationCommitted\b/ },
])

const norm = (p) => p.replace(/\\/g, '/')

export const registrationGap = (checkList) => {
  const listed = (checkList ?? []).some((script) => norm(String(script)).endsWith(SCRIPT_SUFFIX))
  if (listed) return null
  return {
    code: 'scan-not-run',
    path: SCRIPT_SUFFIX,
    message: 'clean-break symbol gate is not in the check list',
  }
}

export function assess(observation) {
  if (!observation || observation.ran !== true) {
    return {
      ran: false,
      ok: false,
      issues: [{
        code: 'scan-not-run',
        path: SCAN_ROOT,
        message: 'clean-break symbol scan did not run',
      }],
    }
  }

  if (observation.readError) {
    return {
      ran: true,
      ok: false,
      issues: [{
        code: 'read-failed',
        path: observation.readError.path ?? SCAN_ROOT,
        message: observation.readError.message ?? 'clean-break symbol scan could not read src/',
      }],
    }
  }

  const files = observation.files ?? []
  if (files.length === 0) {
    return {
      ran: true,
      ok: false,
      issues: [{
        code: 'scan-empty',
        path: SCAN_ROOT,
        message: 'clean-break symbol scan found no files under src/',
      }],
    }
  }

  const issues = []
  for (const file of files) {
    if (file.readError) {
      issues.push({
        code: 'read-failed',
        path: file.path,
        message: file.readError,
      })
      continue
    }
    const lines = String(file.text ?? '').split('\n')
    for (let i = 0; i < lines.length; i++) {
      for (const token of FORBIDDEN) {
        if (token.re.test(lines[i])) {
          issues.push({
            code: token.id,
            path: file.path,
            line: i + 1,
            message: token.id + ' in production source',
          })
        }
      }
    }
  }

  return { ran: true, ok: issues.length === 0, issues }
}

export function check(context) {
  const repoRoot = resolve(context?.root ?? process.cwd())
  const srcRoot = resolve(repoRoot, SCAN_ROOT)
  let paths
  try {
    paths = walk(srcRoot)
  } catch (err) {
    return assess({
      ran: true,
      readError: { path: SCAN_ROOT, message: err.message },
    })
  }

  const files = []
  for (const absolute of paths) {
    const rel = norm(relative(repoRoot, absolute))
    try {
      const text = context?.readText ? context.readText(rel) : readFileSync(absolute, 'utf8')
      if (typeof text !== 'string') {
        files.push({ path: rel, readError: 'clean-break symbol scan read a non-string' })
      } else {
        files.push({ path: rel, text })
      }
    } catch (err) {
      files.push({ path: rel, readError: err.message })
    }
  }
  return assess({ ran: true, files })
}

const isMain =
  process.argv[1] !== undefined &&
  pathToFileURL(resolve(process.argv[1])).href === import.meta.url

if (isMain) {
  const result = check()
  if (!result.ok) {
    console.error('clean-break-symbols: ' + result.issues.length + ' issue(s)')
    for (const issue of result.issues) {
      const loc = issue.line ? issue.path + ':' + issue.line : issue.path
      console.error('  ' + loc + ' [' + issue.code + '] ' + issue.message)
    }
    process.exit(1)
  }
  console.log('clean-break-symbols: OK')
}
