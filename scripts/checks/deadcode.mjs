#!/usr/bin/env node

import { readFileSync } from 'node:fs'
import { relative, resolve } from 'node:path'
import { fileURLToPath, pathToFileURL } from 'node:url'
import { walk } from '../lib/walk.mjs'

export const ROOT = fileURLToPath(new URL('../..', import.meta.url))
export const DEFAULT_SOURCE_ROOT = 'src/Wanxiangshu'
export const DEFAULT_BASELINE = 'scripts/checks/deadcode-baseline.json'

const bindingPattern = /^\s*let\s+private\s+([A-Za-z_][A-Za-z0-9_']*)\b/gm
const identifier = (name) =>
  new RegExp(`(^|[^A-Za-z0-9_'])${name.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')}(?=$|[^A-Za-z0-9_'])`, 'g')

const norm = (value) => value.replace(/\\/g, '/')
const keyOf = ({ file, binding }) => `${file}::${binding}`

export async function scanDeadBindings(sourceRoot, repositoryRoot = sourceRoot) {
  const absoluteRoot = resolve(sourceRoot)
  const files = walk(absoluteRoot, ['.fs'])
  const texts = files.map((file) => [file, readFileSync(file, 'utf8')])
  const repositoryText = texts.map(([, text]) => text).join('\n')
  const hits = []

  for (const [file, text] of texts) {
    bindingPattern.lastIndex = 0
    for (const match of text.matchAll(bindingPattern)) {
      const binding = match[1]
      const occurrences = [...repositoryText.matchAll(identifier(binding))].length
      if (occurrences !== 1) continue
      const line = text.slice(0, match.index).split('\n').length
      hits.push({ file: norm(relative(resolve(repositoryRoot), file)), line, binding })
    }
  }

  return hits.sort((left, right) => keyOf(left).localeCompare(keyOf(right)))
}

export function evaluateDeadBindings(hits, baseline) {
  const current = new Map(hits.map((hit) => [keyOf(hit), hit]))
  const expected = new Set(baseline)
  return {
    regressions: [...current.entries()]
      .filter(([key]) => !expected.has(key))
      .map(([key, hit]) => ({ key, ...hit })),
    improvements: [...expected].filter((key) => !current.has(key)).sort(),
  }
}

const readBaseline = (path) => {
  const value = JSON.parse(readFileSync(resolve(ROOT, path), 'utf8'))
  if (value?.version !== 1 || !Array.isArray(value.bindings)) {
    throw new Error('deadcode: baseline must be { version: 1, bindings: string[] }')
  }
  return value.bindings
}

const parseArgs = (argv) => {
  const options = { root: DEFAULT_SOURCE_ROOT, baseline: DEFAULT_BASELINE }
  for (const arg of argv) {
    if (arg.startsWith('--root=')) options.root = arg.slice('--root='.length)
    else if (arg.startsWith('--baseline=')) options.baseline = arg.slice('--baseline='.length)
    else throw new Error(`deadcode: unknown argument ${arg}`)
  }
  return options
}

const runCli = async () => {
  try {
    const options = parseArgs(process.argv.slice(2))
    const hits = await scanDeadBindings(resolve(ROOT, options.root), ROOT)
    const result = evaluateDeadBindings(hits, readBaseline(options.baseline))
    if (result.regressions.length > 0) {
      for (const hit of result.regressions) console.error(`${hit.file}:${hit.line}: dead private binding ${hit.binding}`)
      process.exit(1)
    }
    const improvement = result.improvements.length > 0
      ? `; ${result.improvements.length} baseline item(s) disappeared — lower baseline before merge`
      : ''
    console.log(`deadcode: clean (debt=${hits.length}${improvement})`)
  } catch (error) {
    console.error(error.message)
    process.exit(2)
  }
}

if (process.argv[1] && import.meta.url === pathToFileURL(resolve(process.argv[1])).href) await runCli()
