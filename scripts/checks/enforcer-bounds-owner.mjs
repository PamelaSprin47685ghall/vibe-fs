#!/usr/bin/env node

import { readFileSync } from 'node:fs'
import { dirname, join, relative, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import { inspectEnforcerBoundsSources } from '../lib/enforcer-bounds-owner.mjs'
import { walk } from '../lib/walk.mjs'

const root = resolve(dirname(fileURLToPath(import.meta.url)), '../..')
const enforcerRoot = join(root, 'src/Wanxiangshu/Enforcer')
const entries = walk(enforcerRoot, ['.fs']).map((path) => ({
  path: relative(enforcerRoot, path),
  text: readFileSync(path, 'utf8'),
}))
const problems = inspectEnforcerBoundsSources(entries)

if (problems.length > 0) {
  console.error('enforcer-bounds-owner FAILED:')
  for (const problem of problems) console.error(`  - ${problem}`)
  process.exit(1)
}

console.log(`enforcer-bounds-owner: OK — ${entries.length} Enforcer production files, one bounds decision owner`)
