#!/usr/bin/env node

import { readFileSync } from 'node:fs'
import { dirname, join, relative, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import { inspectEnforcerBoundsSources } from '../lib/enforcer-bounds-owner.mjs'
import { walk } from '../lib/walk.mjs'

const root = resolve(dirname(fileURLToPath(import.meta.url)), '../..')
const enforcerRoot = join(root, 'src/Wanxiangshu/Enforcer')

export function check(context) {
  const r = context?.root ?? root
  const base = join(r, 'src/Wanxiangshu/Enforcer')
  let entries
  if (context?.productionFiles) {
    entries = context.productionFiles()
      .filter(({ file }) => file.startsWith('src/Wanxiangshu/Enforcer/') && file.endsWith('.fs'))
      .map(({ file, text }) => ({
        path: file.slice('src/Wanxiangshu/Enforcer/'.length),
        text,
      }))
  } else {
    entries = walk(base, ['.fs']).map((path) => ({
      path: relative(base, path),
      text: readFileSync(path, 'utf8'),
    }))
  }
  const problems = inspectEnforcerBoundsSources(entries)
  return {
    issues: problems.map((problem) => ({
      code: 'enforcer-bounds-violation',
      message: problem,
    })),
    entriesCount: entries.length,
  }
}

export function runCli() {
  const result = check()
  if (result.issues.length > 0) {
    console.error('enforcer-bounds-owner FAILED:')
    for (const problem of result.issues) console.error(`  - ${problem.message}`)
    return 1
  }
  console.log(`enforcer-bounds-owner: OK — ${result.entriesCount} Enforcer production files, one bounds decision owner`)
  return 0
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const code = runCli()
  if (code !== 0) process.exit(code)
}
