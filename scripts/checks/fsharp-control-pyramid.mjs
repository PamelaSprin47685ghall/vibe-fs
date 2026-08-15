#!/usr/bin/env node

import { existsSync, readFileSync } from 'node:fs'
import { relative, resolve } from 'node:path'
import { fileURLToPath, pathToFileURL } from 'node:url'
import { walk } from '../lib/walk.mjs'
import { CONTROL_PYRAMID_GUIDE } from './fsharp-control-pyramid-guide.mjs'

export { CONTROL_PYRAMID_GUIDE }

export const ROOT = fileURLToPath(new URL('../..', import.meta.url))
export const DEFAULT_SOURCE_ROOT = 'src/Wanxiangshu'
export const DEFAULT_BASELINE = 'scripts/checks/fsharp-control-pyramid-baseline.json'

const norm = (path) => path.replace(/\\/g, '/')

const indentWidth = (line) => {
  let width = 0
  for (const char of line) {
    if (char === ' ') width += 1
    else if (char === '\t') width += 4
    else break
  }
  return width
}

const classifyDecision = (code) => {
  const text = code.trim()
  if (/^match!\s/.test(text)) return 'match!'
  if (/^match\s/.test(text)) return 'match'
  if (/^function(?:\s|$)/.test(text)) return 'function'
  if (/^if\b.*\bthen\s*$/.test(text)) return 'if'
  if (/^try\s*$/.test(text)) return 'try'
  if (/^while\b.*\bdo\s*$/.test(text)) return 'while'
  if (/^for\b.*\bdo\s*$/.test(text)) return 'for'
  return undefined
}

const sameIndentContinuation = (region, code) => {
  const text = code.trimStart()
  if (region.kind === 'match' || region.kind === 'match!' || region.kind === 'function') {
    return text.startsWith('|')
  }
  if (region.kind === 'if') return /^elif\b|^else\b/.test(text)
  if (region.kind === 'try') return /^(with\b|finally\b|\|)/.test(text)
  return false
}

const sanitizeLines = (text) => {
  const lines = text.split('\n')
  const sanitized = []
  let blockDepth = 0
  let tripleString = false

  for (const raw of lines) {
    let output = ''
    let index = 0
    let quoted = false
    let verbatim = false

    while (index < raw.length) {
      if (tripleString) {
        const end = raw.indexOf('"""', index)
        if (end === -1) {
          output += ' '.repeat(raw.length - index)
          index = raw.length
          continue
        }
        output += ' '.repeat(end + 3 - index)
        index = end + 3
        tripleString = false
        continue
      }

      if (blockDepth > 0) {
        const open = raw.indexOf('(*', index)
        const close = raw.indexOf('*)', index)
        if (close === -1 && open === -1) {
          output += ' '.repeat(raw.length - index)
          index = raw.length
          continue
        }
        if (open !== -1 && (close === -1 || open < close)) {
          output += ' '.repeat(open + 2 - index)
          index = open + 2
          blockDepth += 1
          continue
        }
        output += ' '.repeat(close + 2 - index)
        index = close + 2
        blockDepth -= 1
        continue
      }

      if (quoted) {
        const char = raw[index]
        output += ' '
        if (verbatim) {
          if (char === '"') {
            if (raw[index + 1] === '"') {
              output += ' '
              index += 2
              continue
            }
            quoted = false
            verbatim = false
          }
          index += 1
          continue
        }
        if (char === '\\') {
          if (index + 1 < raw.length) output += ' '
          index += 2
          continue
        }
        if (char === '"') quoted = false
        index += 1
        continue
      }

      if (raw.startsWith('//', index)) {
        output += ' '.repeat(raw.length - index)
        index = raw.length
        continue
      }
      if (raw.startsWith('(*', index)) {
        output += '  '
        blockDepth = 1
        index += 2
        continue
      }
      if (raw.startsWith('"""', index)) {
        output += '   '
        tripleString = true
        index += 3
        continue
      }
      if (raw[index] === '"') {
        output += ' '
        quoted = true
        verbatim = index > 0 && raw[index - 1] === '@'
        index += 1
        continue
      }

      output += raw[index]
      index += 1
    }

    sanitized.push(output)
  }

  return sanitized
}

export const scanControlPyramidEntries = (entries) => {
  const violations = []

  for (const { file, text } of entries) {
    const rawLines = text.split('\n')
    const codeLines = sanitizeLines(text)
    const stack = []

    for (let index = 0; index < codeLines.length; index += 1) {
      const code = codeLines[index]
      if (!code.trim()) continue
      const indent = indentWidth(code)

      while (stack.length > 0) {
        const top = stack.at(-1)
        if (indent > top.indent) break
        if (indent === top.indent && sameIndentContinuation(top, code)) break
        stack.pop()
      }

      const kind = classifyDecision(code)
      if (!kind) continue

      const depth = stack.length + 1
      const region = { kind, indent, line: index + 1 }
      if (depth >= 2) {
        violations.push({
          kind: kind === 'match' || kind === 'match!' || kind === 'function' ? 'match-pyramid' : 'branch-pyramid',
          file: norm(file),
          line: index + 1,
          outerLine: stack[0].line,
          depth,
          chain: [...stack.map((item) => item.kind), kind],
          text: rawLines[index].trim(),
        })
      }
      stack.push(region)
    }
  }

  return violations
}

export const collectControlPyramidEntries = (repoRoot = ROOT, sourceRoot = DEFAULT_SOURCE_ROOT) => {
  const absoluteRoot = resolve(repoRoot, sourceRoot)
  return walk(absoluteRoot, ['.fs']).map((absolute) => ({
    file: norm(relative(repoRoot, absolute)),
    text: readFileSync(absolute, 'utf8'),
  }))
}

const countsByFile = (hits) => {
  const counts = new Map()
  for (const hit of hits) counts.set(hit.file, (counts.get(hit.file) ?? 0) + 1)
  return counts
}

export const makeBaseline = (hits) => ({
  version: 1,
  files: Object.fromEntries([...countsByFile(hits)].sort(([a], [b]) => a.localeCompare(b))),
})

export const evaluateBaseline = (hits, baseline) => {
  if (baseline?.version !== 1 || typeof baseline.files !== 'object' || baseline.files === null) {
    throw new Error('fsharp-control-pyramid: baseline must be { version: 1, files: { ... } }')
  }

  const current = countsByFile(hits)
  const files = new Set([...Object.keys(baseline.files), ...current.keys()])
  const regressions = []
  const improvements = []

  for (const file of [...files].sort()) {
    const expected = baseline.files[file] ?? 0
    const actual = current.get(file) ?? 0
    if (!Number.isInteger(expected) || expected < 0) {
      throw new Error(`fsharp-control-pyramid: invalid baseline count for ${file}: ${expected}`)
    }
    if (actual > expected) {
      regressions.push({
        file,
        baseline: expected,
        current: actual,
        hits: hits.filter((hit) => hit.file === file),
      })
    } else if (actual < expected) {
      improvements.push({ file, baseline: expected, current: actual })
    }
  }

  return {
    regressions,
    improvements,
    currentTotal: hits.length,
    baselineTotal: Object.values(baseline.files).reduce((sum, count) => sum + count, 0),
  }
}

export const renderFailure = (hits, heading = `fsharp-control-pyramid: ${hits.length} violation(s)`) => {
  const lines = [heading, '']
  for (const hit of hits) {
    lines.push(
      `  [${hit.kind}] ${hit.file}:${hit.line} depth=${hit.depth} chain=${hit.chain.join(' → ')}`,
      `    ${hit.text}`,
      '',
    )
  }
  lines.push(CONTROL_PYRAMID_GUIDE.trimStart())
  return lines.join('\n')
}

const parseArgs = (argv) => {
  const options = {
    root: DEFAULT_SOURCE_ROOT,
    baseline: undefined,
    explain: false,
    showAll: false,
    snapshot: false,
  }

  for (const arg of argv) {
    if (arg === '--explain') options.explain = true
    else if (arg === '--show-all') options.showAll = true
    else if (arg === '--snapshot') options.snapshot = true
    else if (arg.startsWith('--root=')) options.root = arg.slice('--root='.length)
    else if (arg.startsWith('--baseline=')) options.baseline = arg.slice('--baseline='.length)
    else throw new Error(`fsharp-control-pyramid: unknown argument ${arg}`)
  }
  return options
}

const readBaseline = (repoRoot, path) => {
  const absolute = resolve(repoRoot, path)
  if (!existsSync(absolute)) throw new Error(`fsharp-control-pyramid: missing baseline ${path}`)
  return JSON.parse(readFileSync(absolute, 'utf8'))
}

const runCli = () => {
  let options
  try {
    options = parseArgs(process.argv.slice(2))
  } catch (error) {
    console.error(error.message)
    process.exit(2)
  }

  if (options.explain) {
    console.log(CONTROL_PYRAMID_GUIDE.trim())
    return
  }

  const entries = collectControlPyramidEntries(ROOT, options.root)
  const hits = scanControlPyramidEntries(entries)

  if (options.snapshot) {
    console.log(JSON.stringify(makeBaseline(hits), null, 2))
    return
  }

  if (options.showAll || !options.baseline) {
    if (hits.length === 0) {
      console.log('fsharp-control-pyramid: clean (0 nested decisions)')
      return
    }
    console.error(renderFailure(hits))
    process.exit(1)
  }

  let result
  try {
    result = evaluateBaseline(hits, readBaseline(ROOT, options.baseline))
  } catch (error) {
    console.error(error.message)
    process.exit(2)
  }

  if (result.regressions.length > 0) {
    const regressedHits = result.regressions.flatMap((entry) => entry.hits)
    const summary = result.regressions
      .map((entry) => `${entry.file}: ${entry.baseline} → ${entry.current}`)
      .join(', ')
    console.error(
      renderFailure(
        regressedHits,
        `fsharp-control-pyramid: regression (${summary}); total debt ${result.currentTotal}/${result.baselineTotal}`,
      ),
    )
    process.exit(1)
  }

  const improvement = result.improvements.length
    ? `; ${result.improvements.length} file(s) improved — lower baseline before merge`
    : ''
  console.log(
    `fsharp-control-pyramid: clean (debt=${result.currentTotal}, baseline=${result.baselineTotal}${improvement})`,
  )
}

if (process.argv[1] && import.meta.url === pathToFileURL(resolve(process.argv[1])).href) runCli()
