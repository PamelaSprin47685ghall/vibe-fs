#!/usr/bin/env node
// Gate ⑬ (O-3): the omp entry module must hold no product decision.
//
// Rule O-3 (`proposals/host-neutral-interface/evidence/omp-feasibility.md`,
// "Decided rules") fixes the omp entry module:
//
//   "The omp entry module is hand-written and product-free, and the Fable-built
//    product is passed in as a plain `IHostPlugin` value (no reflection, no
//    dynamic import of product internals)."
//
//   "The module contains wiring only: it builds the child-session seam, calls
//    `activateOnOmp`, and holds no product decision (no admission rule, no
//    retry, no budget, no prose, no state)."
//
//   "it never reflects over it, never imports product internals, and never
//    looks up product symbols at runtime."
//
//   Acceptance criterion 5: "A review rule (gate) rejects any product decision
//    appearing in the entry module: the entry module contains no
//    admission/refusal/retry/budget/prose logic and imports no product module.
//    No such gate exists today — `cut36-omp-2` must add it as part of this
//    rule's implementation."
//
// DESIGN.md §1 rule 3 is the same prohibition in the product's own words: "An
// adapter may **not**: choose a model, admit/deny a turn or tool, decide
// retry/abort, compute budgets, render product prose, or keep product state
// (journals, ledgers, leases)."
//
// SCOPE. This gate reads ONLY the omp entry module and its interface, i.e. the
// single file under `src/Wanxiangshu/Composition/Omp/` that carries the
// `[<ExportDefault>]` attribute, plus the `.fsi` beside it. It never scans
// another lane's files; the OpenCode lane's own `[<ExportDefault>]` site is
// out of scope by construction.
//
// The gate fails closed: a missing entry module, an ambiguous
// `[<ExportDefault>]` site inside the omp composition folder, or a detector
// that no longer catches its own planted violation is an issue, never a
// silent pass.

import { existsSync, mkdirSync, readFileSync, readdirSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { dirname, join, relative, resolve } from 'node:path'
import { fileURLToPath, pathToFileURL } from 'node:url'

const here = dirname(fileURLToPath(import.meta.url))
const repositoryRoot = resolve(here, '../..')

/** Folder that owns the omp entry module. */
export const ENTRY_FOLDER = 'src/Wanxiangshu/Composition/Omp'

/**
 * Namespaces the entry module may name. Everything else is a product module.
 *
 * O-3: "imports no product module"; "never imports product internals"; the
 * wiring surface O-3 prescribes is exactly the neutral ports, the omp adapter
 * and the second-host plugin value: "the Fable-built product is passed in as a
 * plain `IHostPlugin` value".
 */
export const ALLOWED_NAMESPACES = [
  'System',
  'Fable.Core',
  'Wanxiangshu.Composition.Omp',
  'Wanxiangshu.Host.Ports',
  'Wanxiangshu.Host.SecondHost',
  'Wanxiangshu.Omp',
  'Wanxiangshu.Foundation.IClockPort',
  'Wanxiangshu.Foundation.ITimerPort',
  'Wanxiangshu.Process.NodeTiming',
]

const EXACT_TIME_TYPES = new Set([
  'Wanxiangshu.Foundation.IClockPort',
  'Wanxiangshu.Foundation.ITimerPort',
])
const NODE_TIMING = 'Wanxiangshu.Process.NodeTiming'

export const RAW_TIME = /\b(?:DateTimeOffset\.UtcNow|DateTime\.(?:Now|UtcNow)|Date\.now|setTimeout|timerTask)\b/g

/**
 * Decision vocabulary. O-3: "holds no product decision (no admission rule, no
 * retry, no budget, no prose, no state)". DESIGN.md §1 rule 3 lists the same
 * set: "admit/deny a turn or tool, decide retry/abort, compute budgets,
 * render product prose, or keep product state".
 */
export const DECISION_VOCABULARY =
  /\b(?:admit|admission|admitted|refuse|refusal|deny|denied|allow|allowed|quota|retry|retries|retrying|backoff|attempt|attempts|budget|budgets|tokenLimit|tokenBudget|maxTokens|limit|throttle|rateLimit|state|states)\w*/gi

/**
 * Prose lookup. DESIGN.md §1 rule 3: "render product prose" is a product
 * decision; the product's prose lives in the product's resource tree
 * (`src/Wanxiangshu/Resources/`), never in an adapter.
 */
export const RESOURCE_LOOKUP =
  /\b(?:PackageResources|PromptResources|PromptCatalog|Resources)\b|\b(?:File|Directory|IO|FileIO)\.[A-Za-z]+\b|\breadFile\b|\breadAllText\b/g

/**
 * Control flow that could carry a decision. O-3: "no admission rule, no
 * retry, no budget, no prose, no state" — a branch, a loop or a mutable
 * binding is where such a decision would live.
 */
export const DECISION_CONTROL_FLOW =
  /(?<![\w.])(?:if|match|try|while|for|do|failwith|raise)(?![\w])/g

/** Mutable state. O-3: "no ... state"; DESIGN.md §1 rule 3: "keep product state". */
export const MUTABLE_STATE = /\blet\s+mutable\b|\bmutable\b/g

/** Reflection / dynamic lookup. O-3: "no reflection", "never looks up product symbols at runtime". */
export const REFLECTION = /\b(?:Reflection|Assembly|Type\.GetType|Activator|GetType|DynamicInvoke|InvokeMember)\b|\btypeof\b/g

/** Mask F# comments only (strings are preserved): prose detection needs them. */
export function maskFSharpComments(source) {
  const masked = source.split('')
  const blank = (index) => {
    if (masked[index] !== '\n' && masked[index] !== '\r') masked[index] = ' '
  }
  const blankPair = (index) => {
    blank(index)
    blank(index + 1)
  }

  let index = 0
  while (index < source.length) {
    if (source.startsWith('//', index)) {
      while (index < source.length && source[index] !== '\n') blank(index++)
      continue
    }
    if (source.startsWith('(*', index)) {
      let depth = 1
      blankPair(index)
      index += 2
      while (index < source.length && depth > 0) {
        if (source.startsWith('(*', index)) {
          depth++
          blankPair(index)
          index += 2
        } else if (source.startsWith('*)', index)) {
          depth--
          blankPair(index)
          index += 2
        } else {
          blank(index++)
        }
      }
      continue
    }
    index++
  }
  return masked.join('')
}

/** Mask F# comments and string literals: code-only view for structural checks. */
export function maskFSharpCode(source) {
  const masked = source.split('')
  const blank = (index) => {
    if (masked[index] !== '\n' && masked[index] !== '\r') masked[index] = ' '
  }
  const blankPair = (index) => {
    blank(index)
    blank(index + 1)
  }
  let index = 0
  while (index < source.length) {
    if (source.startsWith('//', index)) {
      while (index < source.length && source[index] !== '\n') blank(index++)
      continue
    }
    if (source.startsWith('(*', index)) {
      let depth = 1
      blankPair(index)
      index += 2
      while (index < source.length && depth > 0) {
        if (source.startsWith('(*', index)) {
          depth++
          blankPair(index)
          index += 2
        } else if (source.startsWith('*)', index)) {
          depth--
          blankPair(index)
          index += 2
        } else {
          blank(index++)
        }
      }
      continue
    }
    if (source.startsWith('"""', index)) {
      blankPair(index)
      blankPair(index + 1)
      index += 3
      while (index < source.length && !source.startsWith('"""', index)) blank(index++)
      if (index < source.length) {
        blankPair(index)
        blankPair(index + 1)
        index += 3
      }
      continue
    }
    if (source.startsWith('@"', index)) {
      blankPair(index)
      index += 2
      while (index < source.length) {
        if (source.startsWith('""', index)) {
          blankPair(index)
          index += 2
        } else if (source[index] === '"') {
          blank(index++)
          break
        } else {
          blank(index++)
        }
      }
      continue
    }
    if (source[index] === '"') {
      blank(index++)
      while (index < source.length) {
        if (source[index] === '\\') {
          blank(index++)
          if (index < source.length) blank(index++)
        } else if (source[index] === '"') {
          blank(index++)
          break
        } else {
          blank(index++)
        }
      }
      continue
    }
    index++
  }
  return masked.join('')
}

/** 1-based line number of a character offset. */
const lineOf = (text, offset) => text.slice(0, offset).split('\n').length

/**
 * Locate the entry module: the single file under the omp composition folder
 * that carries `[<ExportDefault>]`. Fail closed when the site is missing or
 * ambiguous (the omp bundle must have exactly one default export).
 */
export function locateEntryModule(root) {
  const folder = join(root, ENTRY_FOLDER)
  if (!existsSync(folder)) {
    return { ok: false, reason: `omp composition folder is missing: ${ENTRY_FOLDER}` }
  }
  const files = readdirSync(folder)
    .filter((name) => name.endsWith('.fs'))
    .sort()
  const sites = files.filter((name) => /\[\s*<ExportDefault>\s*\]/.test(readFileSync(join(folder, name), 'utf8')))
  if (sites.length === 0) {
    return { ok: false, reason: `no [<ExportDefault>] entry module under ${ENTRY_FOLDER}` }
  }
  if (sites.length > 1) {
    return { ok: false, reason: `ambiguous omp entry module under ${ENTRY_FOLDER}: ${sites.join(', ')}` }
  }
  const implementation = join(folder, sites[0])
  const interfaceFile = implementation.replace(/\.fs$/, '.fsi')
  return {
    ok: true,
    implementation,
    interfaceFile: existsSync(interfaceFile) ? interfaceFile : null,
  }
}

/** True when a dotted name is inside the allowed (non-product) namespace set. */
export function isAllowedNamespace(name) {
  return ALLOWED_NAMESPACES.some(
    (allowed) => {
      if (EXACT_TIME_TYPES.has(allowed)) return name === allowed
      if (allowed === NODE_TIMING) return name === allowed || name.startsWith(`${allowed}.`)
      return name === allowed || allowed.startsWith(`${name}.`) || name.startsWith(`${allowed}.`)
    },
  )
}

/**
 * Analyze one entry-module source text.
 *
 * @param {string} text  the raw F# source
 * @param {string} path  repo-relative path, used in the issues
 * @returns {{code: string, path: string, line: number, message: string}[]}
 */
export function analyzeEntrySource(text, path) {
  const issues = []
  const commentFree = maskFSharpComments(text)
  const code = maskFSharpCode(text)

  // (a) Prose. O-3: "holds no product decision (no admission rule, no retry,
  //     no budget, no prose, no state)"; DESIGN.md §1 rule 3: an adapter may
  //     not "render product prose". A string literal in the entry module is
  //     rendered prose; the product's prose lives in its resource tree.
  const literalRe = /"""[\s\S]*?"""|@"(?:[^"]|"")*"|"(?:\\.|[^"\\])*"/g
  for (const match of commentFree.matchAll(literalRe)) {
    issues.push({
      code: 'omp-entry-prose',
      path,
      line: lineOf(commentFree, match.index),
      message: 'the omp entry module renders prose: O-3 forbids "no ... prose" in the entry module; product prose belongs to the product resource tree',
    })
  }

  // (b) Product import. O-3: "imports no product module"; "never imports
  //     product internals". Only the prescribed neutral wiring surface may be
  //     named.
  for (const match of code.matchAll(/\bWanxiangshu(?:\.[A-Za-z0-9_]+)*/g)) {
    const name = match[0]
    if (isAllowedNamespace(name)) continue
    issues.push({
      code: 'omp-entry-product-import',
      path,
      line: lineOf(code, match.index),
      message: `the omp entry module names product namespace '${name}': O-3 requires "imports no product module"`,
    })
  }

  // (c) Decision control flow. O-3: "no admission rule, no retry, no budget,
  //     no prose, no state" — a branch, a loop or a mutable binding is where
  //     such a decision would live.
  for (const match of code.matchAll(DECISION_CONTROL_FLOW)) {
    issues.push({
      code: 'omp-entry-decision-control-flow',
      path,
      line: lineOf(code, match.index),
      message: `the omp entry module contains control flow ('${match[0]}'): O-3 says the entry module "contains wiring only ... and holds no product decision"`,
    })
  }
  for (const match of code.matchAll(MUTABLE_STATE)) {
    issues.push({
      code: 'omp-entry-decision-state',
      path,
      line: lineOf(code, match.index),
      message: "the omp entry module keeps mutable state: O-3 says the entry module holds \"no ... state\"",
    })
  }

  // (d) Decision vocabulary. O-3: "no admission rule, no retry, no budget";
  //     DESIGN.md §1 rule 3: an adapter may not "admit/deny a turn or tool,
  //     decide retry/abort, compute budgets".
  for (const match of code.matchAll(DECISION_VOCABULARY)) {
    issues.push({
      code: 'omp-entry-decision-vocabulary',
      path,
      line: lineOf(code, match.index),
      message: `the omp entry module carries decision vocabulary ('${match[0]}'): O-3 forbids "no admission rule, no retry, no budget" in the entry module`,
    })
  }

  for (const match of commentFree.matchAll(RAW_TIME)) {
    issues.push({
      code: 'omp-entry-ambient-time',
      path,
      line: lineOf(commentFree, match.index),
      message: `the omp entry module accesses ambient time ('${match[0]}'); only clock and timer factory composition is allowed`,
    })
  }

  // (e) Resource / prose lookup. DESIGN.md §1 rule 3: "render product prose"
  //     is a core decision; the entry module must not reach into the product's
  //     resource tree.
  for (const match of code.matchAll(RESOURCE_LOOKUP)) {
    issues.push({
      code: 'omp-entry-resource-lookup',
      path,
      line: lineOf(code, match.index),
      message: `the omp entry module reaches into the product resource tree ('${match[0]}'): O-3 says the entry module "imports no product module"`,
    })
  }

  // (f) Reflection / runtime product lookup. O-3: "no reflection, no dynamic
  //     import of product internals"; "never looks up product symbols at
  //     runtime".
  for (const match of code.matchAll(REFLECTION)) {
    issues.push({
      code: 'omp-entry-reflection',
      path,
      line: lineOf(code, match.index),
      message: `the omp entry module performs runtime lookup ('${match[0]}'): O-3 says "no reflection" and "never looks up product symbols at runtime"`,
    })
  }

  return issues
}

/**
 * Analyze the entry module under a repository root.
 *
 * @param {string} root repository root (absolute)
 * @returns {{code: string, path?: string, line?: number, message: string}[]}
 */
export function analyzeEntryModule(root) {
  const located = locateEntryModule(root)
  if (!located.ok) {
    return [{ code: 'omp-entry-missing', message: located.reason }]
  }
  const issues = []
  const toRel = (absolute) => relative(root, absolute).replaceAll('\\', '/')
  issues.push(...analyzeEntrySource(readFileSync(located.implementation, 'utf8'), toRel(located.implementation)))
  if (located.interfaceFile) {
    issues.push(
      ...analyzeEntrySource(readFileSync(located.interfaceFile, 'utf8'), toRel(located.interfaceFile)),
    )
  }
  return issues
}

/**
 * A planted violation that the detector must catch, per forbidden class.
 * The self-test proves each class is non-vacuous.
 */
export const PLANTED_VIOLATIONS = [
  {
    code: 'omp-entry-prose',
    label: 'rendered prose',
    source: [
      'namespace Wanxiangshu.Composition.Omp',
      'module OmpExtension =',
      '    [<ExportDefault>]',
      '    let refuse (pi: obj) = "This turn is not admitted, please retry later."',
    ].join('\n'),
  },
  {
    code: 'omp-entry-product-import',
    label: 'product import',
    source: [
      'namespace Wanxiangshu.Composition.Omp',
      'open Wanxiangshu.Interaction.Admission',
      'module OmpExtension =',
      '    [<ExportDefault>]',
      '    let decide = ChatAdmissionIntent.decide',
    ].join('\n'),
  },
  {
    code: 'omp-entry-decision-control-flow',
    label: 'admission branch',
    source: [
      'namespace Wanxiangshu.Composition.Omp',
      'module OmpExtension =',
      '    [<ExportDefault>]',
      '    let decide admitted = if admitted then 1 else 0',
    ].join('\n'),
  },
  {
    code: 'omp-entry-decision-state',
    label: 'mutable state',
    source: [
      'namespace Wanxiangshu.Composition.Omp',
      'module OmpExtension =',
      '    [<ExportDefault>]',
      '    let mutable admitted = 0',
    ].join('\n'),
  },
  {
    code: 'omp-entry-decision-vocabulary',
    label: 'retry/budget decision',
    source: [
      'namespace Wanxiangshu.Composition.Omp',
      'module OmpExtension =',
      '    [<ExportDefault>]',
      '    let budget = 3',
    ].join('\n'),
  },
  {
    code: 'omp-entry-resource-lookup',
    label: 'product resource lookup',
    source: [
      'namespace Wanxiangshu.Composition.Omp',
      'open Wanxiangshu.Resources',
      'module OmpExtension =',
      '    [<ExportDefault>]',
      '    let prompt = PromptCatalog.lookup "admission"',
    ].join('\n'),
  },
  {
    code: 'omp-entry-reflection',
    label: 'runtime product lookup',
    source: [
      'namespace Wanxiangshu.Composition.Omp',
      'module OmpExtension =',
      '    [<ExportDefault>]',
      '    let product = Assembly.Load "Wanxiangshu"',
    ].join('\n'),
  },
]

/**
 * Self-test: prove the detector still fails on a planted violation of every
 * forbidden class and still passes on the real entry module.
 *
 * The planted file is written under `tmpDir` so the proof never touches the
 * working tree.
 *
 * @param {{root: string, tmpDir: string}} options
 * @returns {{ok: boolean, lines: string[], failures: string[]}}
 */
export function selfTest({ root, tmpDir }) {
  const lines = []
  const failures = []
  const folder = join(tmpDir, 'planted', ENTRY_FOLDER)
  mkdirSync(folder, { recursive: true })

  const real = locateEntryModule(root)
  if (!real.ok) {
    return { ok: false, lines: [`self-test: cannot locate the real entry module: ${real.reason}`], failures: [real.reason] }
  }

  lines.push(`self-test: real entry module ${relative(root, real.implementation).replaceAll('\\', '/')}`)

  for (const planted of PLANTED_VIOLATIONS) {
    const plantedRoot = join(tmpDir, 'planted')
    const plantedFile = join(folder, 'OmpExtension.fs')
    writeFileSync(plantedFile, `${planted.source}\n`)
    const issues = analyzeEntryModule(plantedRoot)
    const codes = issues.map((issue) => issue.code)
    const caught = codes.includes(planted.code)
    if (!caught) failures.push(`planted '${planted.label}' was not caught by ${planted.code}`)
    lines.push(
      `self-test: planted '${planted.label}' -> ${caught ? 'FAIL (rejected)' : 'PASS (NOT rejected)'}` +
        `${issues.length ? ` [${[...new Set(codes)].join(', ')}]` : ''}`,
    )
    rmIfPresent(plantedFile)
  }

  const realIssues = analyzeEntryModule(root)
  const realOk = realIssues.length === 0
  if (!realOk) failures.push(`the real entry module was rejected: ${realIssues.map((i) => i.message).join('; ')}`)
  lines.push(`self-test: real entry module -> ${realOk ? 'PASS (accepted)' : 'FAIL (rejected)'}`)

  return { ok: failures.length === 0, lines, failures }
}

const rmIfPresent = (path) => {
  if (existsSync(path)) rmSync(path, { recursive: true, force: true })
}

/** The check entry point used by `scripts/check.mjs`. */
export function check(context) {
  const root = context?.root ?? repositoryRoot
  const issues = analyzeEntryModule(root)
  // The planted proof always runs against the real tree, so a fixture run
  // (e.g. `--root <scratch>`) still proves the detectors are non-vacuous.
  // It runs in a scratch folder outside the working tree.
  const scratch = join(process.env.TMPDIR && process.env.TMPDIR.trim() !== '' ? process.env.TMPDIR : tmpdir(), 'omp-entry-decision-gate-self-test')
  const selfTestResult = selfTest({ root: repositoryRoot, tmpDir: scratch })
  rmIfPresent(scratch)
  if (!selfTestResult.ok) {
    issues.push({
      code: 'omp-entry-self-test',
      message: `the gate's own planted-violation proof failed: ${selfTestResult.failures.join('; ')}`,
    })
  }
  return { issues }
}

/** Standalone CLI. `--self-test` prints the planted proof only. */
export function runCli(argv = process.argv.slice(2)) {
  const rootIndex = argv.indexOf('--root')
  const root = rootIndex >= 0 && argv[rootIndex + 1] ? resolve(argv[rootIndex + 1]) : repositoryRoot
  if (argv.includes('--self-test')) {
    const tmpDir = process.env.TMPDIR && process.env.TMPDIR.trim() !== ''
      ? join(process.env.TMPDIR, 'omp-entry-decision-gate-self-test')
      : join(tmpdir(), 'omp-entry-decision-gate-self-test')
    mkdirSync(tmpDir, { recursive: true })
    const result = selfTest({ root, tmpDir })
    for (const line of result.lines) console.log(line)
    if (!result.ok) {
      console.error('omp-entry-decision-gate self-test FAILED:')
      for (const failure of result.failures) console.error(`  - ${failure}`)
      return 1
    }
    console.log('omp-entry-decision-gate self-test OK')
    return 0
  }

  const { issues } = check({ root })
  if (issues.length > 0) {
    console.error('omp-entry-decision-gate FAILED:')
    for (const issue of issues) {
      const loc = issue.path ? `${issue.path}${issue.line ? `:${issue.line}` : ''} ` : ''
      console.error(`  - ${loc}[${issue.code}] ${issue.message}`)
    }
    return 1
  }
  console.log('omp-entry-decision-gate OK')
  return 0
}

const isMain = process.argv[1] && pathToFileURL(resolve(process.argv[1])).href === import.meta.url
if (isMain) {
  const code = runCli()
  if (code !== 0) process.exit(code)
}
