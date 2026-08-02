#!/usr/bin/env node
// scripts/budget-gate.mjs — no timing budget may live outside the single source.
//
// VERIFY-004 §「兜底值必须集中定义，不得散落为字面量」, plus the last entry of its
// 禁止退化清单: 「延长静默窗口或测试超时以掩盖竞态」. A budget spelled as a literal at its
// call site can be raised by one character in one file, which is exactly how that degradation
// happens without anyone reviewing it. Centralizing does not prevent the raise — nothing
// static can — but it makes the raise a diff against a named value with a rationale next to
// it, and against the value-pin assertion in `tests/gate-budget-cases.mjs`.
//
// ── the discriminator: magnitude IS the semantic line ───────────────────────
//
// The hard question for a gate like this is telling a forbidden literal budget apart from a
// legitimate polling slice. The unprovable version of that question is "is this literal
// governed by an imported bound", which needs dataflow this repo has no business attempting.
//
// The provable version is magnitude. A slice must poll faster than the budget that bounds it,
// or it races that budget: `canary-driver.mjs` slices at 500ms under a 3000ms silence budget,
// the listen poll runs at 50ms, the socket retry at 30ms. So a legitimate slice is below
// 1000ms by construction. A slice that genuinely needs 1000ms or more is not a slice — it is
// itself a budget, and it costs one line in `time-budget.js` to say so.
//
// ── no exemption channel ───────────────────────────────────────────────────
//
// There is deliberately no `// budget-gate-ignore` comment. Package W is replacing four
// gates that existed, were green, and caught nothing; the `epochCold` exemption passed
// precisely the mutations it existed to catch. An exemption channel is the mechanism by
// which a gate turns into decoration.
//
//   node scripts/budget-gate.mjs            report; exit 1 on any violation
//
// Exit code is the whole interface — there is no --write, because deciding WHICH name a
// literal belongs under is the author's judgement, not a rewrite.

import { readFileSync } from 'node:fs'
import { walk } from './repo-scan.mjs'
import * as budget from '../testkit/opencode/time-budget.js'

const BUDGET_MODULE = 'testkit/opencode/time-budget.js'

// tests-mjs/fixtures/ holds deliberately hung test doubles whose literals are the subject
// under test rather than a budget the harness obeys. It does not exist yet; naming it here
// keeps the exclusion from being written under pressure later, when the temptation would be
// to widen it instead.
const FIXTURE_ROOT = 'tests-mjs/fixtures/'

const SCOPE = [
  { root: 'testkit', extensions: ['.js', '.mjs'] },
  { root: 'scripts', extensions: ['.mjs'] },
  { root: 'tests-mjs/runner.mjs', extensions: ['.mjs'] },
]

const THRESHOLD_MS = budget.LITERAL_BUDGET_THRESHOLD_MS

/**
 * A name ending this way announces itself as a bound, so what it is assigned is a budget.
 *
 * The word suffixes are case-insensitive because this tree spells them three ways
 * (`SUITE_BACKSTOP_MS`, `startTimeoutMs`, a bare `timeout` key). The millisecond suffix is not:
 * `_MS` and `killGraceMs` are units, while a lowercase `ms` ending is usually an English plural
 * (`items`, `params`) and matching it would flag arithmetic as a budget.
 */
const declaresBudget = (name) =>
  /(?:TIMEOUT|WINDOW|DEADLINE|BUDGET|SILENCE)$/i.test(name) || /_MS$|[a-z0-9]Ms$/.test(name)

/** How many milliseconds a `10s` / `3000ms` / `5 minutes` phrase states. */
const MS_PER_UNIT = { ms: 1, s: 1000, sec: 1000, secs: 1000, second: 1000, seconds: 1000, min: 60000, minute: 60000, minutes: 60000 }

const DURATION_IN_PROSE = new RegExp(`(?<![\\d.])(\\d+)\\s*(${Object.keys(MS_PER_UNIT).join('|')})\\b`, 'g')

const TABLE = Object.entries(budget).filter(([, value]) => typeof value === 'number')

// ── masking: a literal only counts where it is code ─────────────────────────

/**
 * Replace every string literal, comment body, and regex body with spaces, preserving offsets.
 *
 * Offsets have to survive because violations are reported by line, and a report that cannot name
 * file and line is a report nobody acts on. Two passes over the same source would disagree about
 * which quote opened a string, so this walks it once and returns both views: `code` for the
 * literal rules, `strings` for the anti-duplication rule.
 *
 * Comments are masked out of BOTH views. `setInterval(() => {}, 1000)` inside a fixture string,
 * and a prose sentence citing a measurement, are not budgets — and a gate that flagged its own
 * explanation would be worked around within a day.
 *
 * Regex bodies are masked and NOT recorded as strings, for two reasons measured in this tree.
 * They contain digits that are not durations (`/\d{4,}/`), and 78 lines in scope contain a regex
 * holding a quote character (`/"""|'''/g` in `toml-format.mjs`, `/\\/g` in `shock-audit.mjs`).
 * Without this branch that quote opens a phantom string that masks the rest of the line, so a
 * budget written after such a regex would be invisible to every rule — the silent false
 * negative that makes a gate decorative.
 */
function maskSource(source) {
  const code = source.split('')
  const strings = []
  let index = 0
  let lastMeaningful = ''

  const blank = (from, to) => {
    for (let at = from; at < to; at += 1) if (code[at] !== '\n') code[at] = ' '
  }

  // A `/` starts a regex only where a value may begin. After an operand — identifier, literal,
  // or closing bracket — it is division. This is the standard heuristic and it is sufficient
  // here because the alternative reading of a mistake is symmetric: guessing regex where there
  // is division masks an expression, guessing division where there is a regex is what the
  // quote-in-regex hazard above already proves unsafe.
  const regexMayStart = () => lastMeaningful === '' || /[([{,;:=!&|?+\-*/%~^<>]/.test(lastMeaningful)

  while (index < source.length) {
    const char = source[index]

    if (char === '/' && source[index + 1] === '/') {
      let end = source.indexOf('\n', index)
      if (end === -1) end = source.length
      blank(index, end)
      index = end
      continue
    }
    if (char === '/' && source[index + 1] === '*') {
      let end = source.indexOf('*/', index + 2)
      end = end === -1 ? source.length : end + 2
      blank(index, end)
      index = end
      continue
    }
    if (char === '/' && regexMayStart()) {
      let at = index + 1
      let inClass = false
      while (at < source.length && source[at] !== '\n') {
        if (source[at] === '\\') {
          at += 2
          continue
        }
        if (source[at] === '[') inClass = true
        else if (source[at] === ']') inClass = false
        else if (source[at] === '/' && !inClass) break
        at += 1
      }
      // An unterminated `/` on one line was division after all, so leave it alone.
      if (source[at] === '/') {
        blank(index, at + 1)
        index = at + 1
        lastMeaningful = ')'
        continue
      }
    }
    if (char === '"' || char === "'" || char === '`') {
      const quote = char
      let at = index + 1
      while (at < source.length) {
        if (source[at] === '\\') {
          at += 2
          continue
        }
        if (source[at] === quote) break
        if (quote !== '`' && source[at] === '\n') break
        at += 1
      }
      const end = Math.min(at + 1, source.length)
      strings.push({ offset: index, text: source.slice(index, end) })
      blank(index, end)
      index = end
      lastMeaningful = ')'
      continue
    }

    if (!/\s/.test(char)) lastMeaningful = char
    index += 1
  }

  return { code: code.join(''), strings }
}

const lineOf = (source, offset) => source.slice(0, offset).split('\n').length

// ── the rules ───────────────────────────────────────────────────────────────

/**
 * The literal rule: a number in a timing POSITION.
 *
 * Three positions, because those are the three ways this repo actually spells a budget: an
 * argument to a timer primitive, the value of a `timeout`/`timeoutMs` key or default, and the
 * initializer of a name that already claims to be a bound. A literal reaching a timer through a
 * variable is not matched here — the variable's own declaration is, which is the same violation
 * one line earlier.
 */
const TIMING_POSITIONS = [
  {
    pattern: /\b(?:setTimeout|setInterval)\s*\([^,()]*(?:\([^()]*\)[^,()]*)*,\s*(\d+)\s*\)/g,
    explain: 'passed straight to a timer primitive',
  },
  {
    pattern: /\bAbortSignal\.timeout\s*\(\s*(\d+)\s*\)/g,
    explain: 'passed straight to AbortSignal.timeout',
  },
  {
    pattern: /(?:^|[^\w$])[\w$]*[Tt]imeout(?:Ms)?\s*(?::|=|\|\||\?\?)\s*(\d+)\b/g,
    explain: 'a timeout property, parameter default, or fallback',
  },
]

/** An assignment to a name; the expression it is assigned is scanned separately. */
const ASSIGNMENT = /(?:^|[^\w$.!<>=+\-*/%&|^])([A-Za-z_$][\w$]*)\s*=(?!=|>)/g

/** A number that is its own token, not part of an identifier or a property path. */
const STANDALONE_NUMBER = /(?<![\w$.])(\d+)(?![\w$.])/g

/**
 * The expression assigned at `from`, ending where the assignment does.
 *
 * A flat regex cannot do this, and both failure directions were measured. Running to end of line
 * made `{ termGraceMs = 500, killGraceMs = 1000 }` credit the 1000 to `termGraceMs` — and the
 * message's whole job is naming the constant to fix, so a wrong name sends the author to edit a
 * value that is already correct. Stopping at the first comma instead lost
 * `parsePositiveInt(env, 90000, name)` entirely, because that budget sits past a comma INSIDE
 * the call — a false negative on one of the six literals this package exists to remove.
 *
 * So a comma ends the assignment only at bracket depth zero.
 */
function assignedExpression(code, from) {
  let depth = 0

  for (let at = from; at < code.length; at += 1) {
    const char = code[at]
    if (char === '\n' || char === ';') return code.slice(from, at)
    if (char === '(' || char === '[' || char === '{') depth += 1
    else if (char === ')' || char === ']' || char === '}') {
      if (depth === 0) return code.slice(from, at)
      depth -= 1
    } else if (char === ',' && depth === 0) return code.slice(from, at)
  }

  return code.slice(from)
}

function literalViolations(file, code) {
  const found = new Map()

  // One literal, one violation. `const startTimeout = opts.startTimeoutMs || 5000` sits in two
  // timing positions at once, and reporting it twice would inflate the count a reader uses to
  // judge whether the migration is finished.
  const report = (line, value, explain) => {
    const key = `${line}:${value}`
    if (value < THRESHOLD_MS || found.has(key)) return
    found.set(key, {
      file,
      line,
      detail: `${value} is ${explain}; ${THRESHOLD_MS}ms or more is a budget, not a poll slice`,
    })
  }

  for (const { pattern, explain } of TIMING_POSITIONS) {
    pattern.lastIndex = 0
    let match
    while ((match = pattern.exec(code)) !== null) {
      report(lineOf(code, match.index), Number(match[1]), explain)
    }
  }

  ASSIGNMENT.lastIndex = 0
  let assignment
  while ((assignment = ASSIGNMENT.exec(code)) !== null) {
    const name = assignment[1]
    if (!declaresBudget(name)) continue

    const expression = assignedExpression(code, ASSIGNMENT.lastIndex)

    STANDALONE_NUMBER.lastIndex = 0
    let number
    while ((number = STANDALONE_NUMBER.exec(expression)) !== null) {
      report(
        lineOf(code, assignment.index),
        Number(number[1]),
        `assigned to ${name}, a name that declares itself a bound`,
      )
    }
  }

  return [...found.values()]
}

/**
 * The anti-duplication rule: no string may restate a table value as a duration.
 *
 * Derived from the table rather than a hand-written list of forbidden phrases, so a budget
 * added later is covered without anyone remembering this rule exists. Measured motivation:
 * two failure messages read "within 10s" while the timer read 10000, so the budget and the
 * sentence describing it were two facts that could drift — and the sentence is the half an
 * operator reads. Interpolating the constant is the fix, and it is one character longer.
 *
 * The match requires a UNIT, not a bare integer, and that limit is deliberate rather than
 * conservative. A first version compared bare digit runs against each table value and against
 * that value divided by 1000, as the charter described; run against the real tree it produced
 * 935 hits, because dividing 1000ms by 1000 makes the forbidden digit run `1` and every
 * string in the harness contains a `1` somewhere — `'http://127.0.0.1:9999/v1'`, `'$1'`,
 * `'2025-01-01'`. A rule that fires on almost every line is not a strict rule, it is a rule
 * that will be deleted. The unit is what makes a number a restated duration instead of a
 * coincidence, so the unit is what the rule keys on. The residual gap — a budget restated as
 * a bare integer with no unit — is accepted and named here rather than hidden.
 */
function stringViolations(file, source, strings) {
  const found = []

  for (const { offset, text } of strings) {
    DURATION_IN_PROSE.lastIndex = 0
    let match
    while ((match = DURATION_IN_PROSE.exec(text)) !== null) {
      const stated = Number(match[1]) * MS_PER_UNIT[match[2]]
      const names = TABLE.filter(([, value]) => value === stated).map(([name]) => name)
      if (names.length === 0) continue
      found.push({
        file,
        line: lineOf(source, offset + match.index),
        detail: `string restates ${names.join('/')} as '${match[0]}'; interpolate the constant instead`,
      })
    }
  }

  return found
}

// ── audit ───────────────────────────────────────────────────────────────────

/**
 * Every violation in `files`, plus the anti-drift verdict over the same set.
 *
 * Exported as a function rather than run at import time so the gate's own cases can point it
 * at a temp tree containing known violations. A gate that can only be exercised by editing the
 * real repo is a gate whose negative behaviour nobody ever verifies — which is how the four
 * pseudo-gates package W is replacing stayed green.
 */
export function auditBudgets(files) {
  const violations = []
  const referenced = new Set()

  for (const file of files) {
    const source = readFileSync(file, 'utf8')
    const { code, strings } = maskSource(source)

    violations.push(...literalViolations(file, code))
    violations.push(...stringViolations(file, source, strings))

    for (const [name] of TABLE) if (code.includes(name)) referenced.add(name)
  }

  // The anti-drift rule: a budget nobody consumes.
  //
  // The failure this repo keeps producing is not a wrong gate but a gate with no call site —
  // `buildAttemptExecutionProfile` sat at zero callers while its clause read CONTRADICTS. A
  // constant in the table that nothing imports is the same shape: it looks like the single
  // source and governs nothing.
  for (const [name, value] of TABLE) {
    if (referenced.has(name)) continue
    violations.push({
      file: BUDGET_MODULE,
      line: 0,
      detail: `${name} (${value}) is referenced nowhere in scope — a budget with no call site governs nothing`,
    })
  }

  return violations.sort((a, b) => (a.file === b.file ? a.line - b.line : a.file < b.file ? -1 : 1))
}

/** The real tree this gate governs. */
export function scopedFiles() {
  return SCOPE.flatMap(({ root, extensions }) => walk(root, extensions))
    .filter((file) => !file.endsWith('time-budget.js'))
    .filter((file) => !file.includes(FIXTURE_ROOT))
}

// ── cli ─────────────────────────────────────────────────────────────────────

const isMain = process.argv[1] !== undefined && import.meta.url.endsWith(process.argv[1].replace(/^.*?(scripts\/)/, '$1'))

if (isMain) {
  const files = scopedFiles()

  if (files.length === 0) {
    console.error('budget-gate: scanned no files — the scope patterns no longer match the tree')
    process.exit(1)
  }

  const violations = auditBudgets(files)

  if (violations.length === 0) {
    console.log(`budget-gate: OK — ${TABLE.length} budget(s), ${files.length} file(s), no scattered literals`)
    process.exit(0)
  }

  console.error(`budget-gate: ${violations.length} line(s) declare a timing budget outside ${BUDGET_MODULE}\n`)
  for (const { file, line, detail } of violations) console.error(`  ${file}:${line} ${detail}`)
  console.error(`\n  fix: name the value in ${BUDGET_MODULE} and import it. There is no exemption comment.`)
  process.exit(1)
}
