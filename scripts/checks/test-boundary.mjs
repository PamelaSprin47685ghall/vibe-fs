#!/usr/bin/env node

import { readFileSync } from 'node:fs'
import { join, relative, resolve } from 'node:path'
import { walk } from '../lib/walk.mjs'
import { BUILD_VERIFICATION_FILES } from '../lib/test-surface-scan.mjs'

const FABLE_PATTERN = /dist[/\\]fable_modules/

export const scanViolations = (root) => {
  const violations = []
  const scopes = [
    join(root, 'requirements'),
  ]

  for (const base of scopes) {
    for (const file of walk(base, ['.mjs', '.js'])) {
      const relativeFile = relative(root, file).replace(/\\/g, '/')
      if (BUILD_VERIFICATION_FILES.has(relativeFile)) continue
      for (const line of readFileSync(file, 'utf8').split('\n')) {
        if (FABLE_PATTERN.test(line)) violations.push(`${relativeFile}::${line.trim()}`)
      }
    }
  }

  return violations
}

const root = resolve(process.argv.find((arg) => arg.startsWith('--root='))?.slice('--root='.length) ?? '.')
const violations = scanViolations(root)

if (violations.length > 0) {
  console.error(`test-boundary: ${violations.length} direct Fable-module import(s) remain outside build verification:`)
  for (const violation of violations.sort()) console.error(`  ${violation}`)
  process.exit(1)
}

console.log('test-boundary: OK — zero direct Fable-module imports outside build verification')
