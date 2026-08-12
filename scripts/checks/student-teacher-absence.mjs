#!/usr/bin/env node
// Student/Teacher absence gate: production must not retain the Student–Teacher surface.
// Fail-closed on any forbidden token under src/Wanxiangshu (and resources/provider).
//
// Usage: node scripts/checks/student-teacher-absence.mjs

import { existsSync, readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import { walk } from '../lib/walk.mjs'

const PRODUCTION_ROOT = 'src/Wanxiangshu'
const PROVIDER_ROOT = 'resources/provider'

/** Forbidden production tokens — Student/Teacher role surface must be fully removed. */
export const FORBIDDEN_TOKENS = [
  'Role.Student',
  'Role.Teacher',
  'fast-student',
  'deep-student',
  'fast-teacher',
  'deep-teacher',
  'StudentLearn',
  'StudentCompile',
  'StudentQaStore',
  'StudentTeacherRuntime',
  'StudentTeacherTools',
  'StudentSkill',
  'SatelliteKind.Teacher',
  // Strength ownership is AttachmentKind.StrengthReplica (Universal); never SatelliteKind.Replica.
  'SatelliteKind.Replica',
]

const norm = (path) => path.replace(/\\/g, '/')

/**
 * @param {{ file: string, text: string }[]} entries
 * @returns {{ file: string, line: number, token: string, text: string }[]}
 */
export const scanEntries = (entries) => {
  const violations = []
  for (const { file, text } of entries) {
    const lines = text.split('\n')
    for (let i = 0; i < lines.length; i++) {
      const line = lines[i]
      for (const token of FORBIDDEN_TOKENS) {
        if (line.includes(token)) {
          violations.push({
            file: norm(file),
            line: i + 1,
            token,
            text: line.trim(),
          })
        }
      }
    }
  }
  return violations
}

const collectEntries = () => {
  if (!existsSync(PRODUCTION_ROOT)) {
    console.error(`student-teacher-absence: required directory '${PRODUCTION_ROOT}' does not exist`)
    process.exit(1)
  }

  const files = walk(PRODUCTION_ROOT, ['.fs'])
  if (existsSync(PROVIDER_ROOT)) {
    files.push(...walk(PROVIDER_ROOT, ['.md']))
  }

  return files.map((file) => ({
    file,
    text: readFileSync(file, 'utf8'),
  }))
}

const runCli = () => {
  const entries = collectEntries()
  const violations = scanEntries(entries)

  if (violations.length === 0) {
    console.log(
      `student-teacher-absence: OK — ${entries.length} files, ${FORBIDDEN_TOKENS.length} tokens`,
    )
    process.exit(0)
  }

  console.error(`student-teacher-absence: ${violations.length} violation(s)\n`)
  for (const v of violations) {
    console.error(`  ${v.file}:${v.line}  '${v.token}'`)
    console.error(`    ${v.text.slice(0, 160)}`)
  }
  process.exit(1)
}

if (
  process.argv[1] !== undefined &&
  resolve(fileURLToPath(import.meta.url)) === resolve(process.argv[1])
) {
  runCli()
}