const CLAUSE_LIKE_RE = /\b([A-Z][A-Z0-9]*-\d{3}(?:[A-Z]|-[A-Z0-9-]+)?)\b/g
const NON_CLAUSE_IDENTIFIERS = new Set(['SHA-256'])
const CLAUSE_HEADING_RE = /^#{1,6}\s+([A-Z][A-Z0-9]*-\d{3}(?:[A-Z]|-[A-Z0-9-]+)?)\b/gm

const escapedAlternation = (prefixes) =>
  prefixes.map((prefix) => prefix.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')).join('|')

/** Return unknown/suffixed clause-looking tokens with 1-based lines. */
export const unknownClauseReferences = (text, prefixes) => {
  const known = new Set(prefixes)
  const findings = []
  const lines = text.split('\n')

  lines.forEach((content, index) => {
    for (const match of content.matchAll(CLAUSE_LIKE_RE)) {
      const token = match[1]
      if (NON_CLAUSE_IDENTIFIERS.has(token)) continue

      const exact = /^([A-Z][A-Z0-9]*)-(\d{3})$/.exec(token)
      if (!exact || !known.has(exact[1])) {
        findings.push({ token, line: index + 1 })
      }
    }
  })

  return findings
}

/** Return every Clause-shaped Markdown heading, independent of known prefixes. */
export const clauseDefinitionHeadings = (text) => {
  const findings = []
  for (const match of text.matchAll(CLAUSE_HEADING_RE)) {
    findings.push({ id: match[1], line: text.slice(0, match.index).split('\n').length })
  }
  return findings
}

/** Return formal Clause headings while allowing non-product Change IDs such as CHG-NNN. */
export const formalClauseDefinitionHeadings = (text, prefixes) => {
  const known = new Set(prefixes)
  return clauseDefinitionHeadings(text).filter(({ id }) => known.has(id.split('-')[0]))
}

/** Return references to the retired workflow directories under docs. */
export const legacyWorkflowPathReferences = (text) => {
  const findings = []

  text.split('\n').forEach((content, index) => {
    const line = index + 1
    if (/(?:^|[^A-Za-z])docs\/proposal(?:\/|\b)/.test(content))
      findings.push({ token: 'docs/proposal/', line })
    if (/(?:^|[^A-Za-z])docs\/status(?:\/|\b)/.test(content))
      findings.push({ token: 'docs/status/', line })
  })

  return findings
}

/** Return references to the deleted archive/ tree (2026-08-14 cutover). */
export const archivePathReferences = (text) => {
  const findings = []

  text.split('\n').forEach((content, index) => {
    const match = /(?:^|[^A-Za-z])(archive\/[^\s`"')\]>]*)/.exec(content)
    if (match) findings.push({ token: match[1], line: index + 1 })
  })

  return findings
}

/** Return forbidden implementation/spec dependencies on lifecycle history. */
export const changeDependencyReferences = (text) => {
  const findings = []

  text.split('\n').forEach((content, index) => {
    const line = index + 1
    if (/(?:^|[^A-Za-z])changes\/proposed(?:\/|\b)/.test(content))
      findings.push({ token: 'changes/proposed/', line })
    if (/(?:^|[^A-Za-z])changes\/completed\/[^`)\n>]+\.md(?:\b|#)/.test(content))
      findings.push({ token: 'changes/completed/<file>.md', line })
  })

  return findings
}

/** Return relative Markdown link targets; URL schemes and document-local anchors are excluded. */
export const markdownLocalLinks = (text) => {
  const findings = []
  const link = /\]\((?:<([^>]+)>|([^\s)]+))\)/g

  for (const match of text.matchAll(link)) {
    const raw = match[1] ?? match[2]
    if (!raw || raw.startsWith('#') || /^[a-z][a-z0-9+.-]*:/i.test(raw)) continue
    const withoutFragment = raw.split('#')[0].split('?')[0]
    let target = withoutFragment
    try {
      target = decodeURIComponent(withoutFragment)
    } catch {
      // Invalid URI escaping remains a filesystem miss in the caller.
    }
    findings.push({ target, line: text.slice(0, match.index).split('\n').length })
  }

  return findings
}

/**
 * Return known-prefix clause references, expanding compact spellings:
 * `PROMPT-003/005/006` checks all three; `HOST-009..012` and `CTX-006…012`
 * check both endpoints. Ranges do not imply that every intermediate number exists.
 */
export const clauseReferences = (text, prefixes) => {
  const alternation = escapedAlternation(prefixes)
  const exact = new RegExp(`\\b(${alternation})-(\\d{3})\\b`, 'g')
  const slashTail = new RegExp(`\\b(${alternation})-\\d{3}((?:/\\d{3})+)\\b`, 'g')
  const rangeEnd = new RegExp(`\\b(${alternation})-\\d{3}(?:\\.\\.|…)(\\d{3})\\b`, 'g')
  const findings = []
  const seen = new Set()

  const add = (id, line) => {
    const key = `${line}:${id}`
    if (!seen.has(key)) {
      seen.add(key)
      findings.push({ id, line })
    }
  }

  text.split('\n').forEach((content, index) => {
    const line = index + 1

    for (const match of content.matchAll(exact)) add(`${match[1]}-${match[2]}`, line)

    for (const match of content.matchAll(slashTail)) {
      for (const suffix of match[2].split('/').filter(Boolean)) {
        add(`${match[1]}-${suffix}`, line)
      }
    }

    for (const match of content.matchAll(rangeEnd)) add(`${match[1]}-${match[2]}`, line)
  })

  return findings
}

/** Compare README links for one directory with its exact Markdown file set. */
export const navigationProblems = (navigation, directory, files) => {
  const expected = new Set(files)
  const linked = new Map()
  const escapedDirectory = directory.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')
  const link = new RegExp(`\\]\\((?:<)?(${escapedDirectory}\\/[^)>]+\\.md)(?:>)?\\)`, 'g')

  for (const match of navigation.matchAll(link)) {
    const line = navigation.slice(0, match.index).split('\n').length
    linked.set(match[1], line)
  }

  return {
    missing: [...expected].filter((file) => !linked.has(file)).sort(),
    stale: [...linked]
      .filter(([file]) => !expected.has(file))
      .map(([file, line]) => ({ file, line }))
      .sort((a, b) => a.file.localeCompare(b.file)),
  }
}

// ── Active change-file body contract (REQUIREMENT-SYSTEM-013) ───────────────

const ACTIVE_ALLOWED_SECTIONS = new Set([
  'Original proposal',
  'Work origin',
  'Remaining work',
  'Completion criteria',
  'Blockers',
  'Amendments',
  'Final outcome',
])

const ACTIVE_FORBIDDEN_SECTION_RE =
  /^(Progress|Commits?|Commit log|Code snapshot|Completion percentage|Diff|Changelog)$/i

const isChangeIdentity = (title) => /^CHG-\d+/i.test(title)

/**
 * Validate an Active change-file body against REQUIREMENT-SYSTEM-013.
 *
 * Pure: takes file text, returns violation objects `{ rule, line, msg }`.
 * Does NOT read `changes/active/`, does NOT infer lifecycle status from prose
 * — the caller decides what to feed (live file, fixture, or direct string).
 *
 * Rules:
 *   frozen-origin    — must carry an `Original proposal` or `Work origin` heading
 *   unknown-section  — every section heading must be in the Active whitelist
 *   forbidden-section — no Progress / Commits / Code snapshot / Completion
 *                      percentage / Diff / Changelog headings (named refinement)
 *
 * "Frozen" (original text not rewritten over time) needs version history and
 * remains human review; this validator checks the structural boundary only.
 */
export const activeBodyViolations = (text) => {
  const findings = []
  const headings = []

  text.split('\n').forEach((content, index) => {
    const match = /^(#{1,6})\s+(.+?)\s*$/.exec(content)
    if (match) headings.push({ title: match[2], line: index + 1 })
  })

  const sections = headings.filter((h) => !isChangeIdentity(h.title))

  const hasOrigin = sections.some(
    (h) => h.title === 'Original proposal' || h.title === 'Work origin',
  )
  if (!hasOrigin) {
    findings.push({
      rule: 'frozen-origin',
      line: 0,
      msg: 'Active must carry a frozen Original proposal / Work origin heading',
    })
  }

  for (const h of sections) {
    if (ACTIVE_FORBIDDEN_SECTION_RE.test(h.title)) {
      findings.push({
        rule: 'forbidden-section',
        line: h.line,
        msg: `Active forbids progress/commit/code-snapshot section: ${h.title}`,
      })
    } else if (!ACTIVE_ALLOWED_SECTIONS.has(h.title)) {
      findings.push({
        rule: 'unknown-section',
        line: h.line,
        msg: `Active section "${h.title}" is not in the allowed whitelist`,
      })
    }
  }

  return findings
}

const originSection = (text) => {
  const lines = text.split('\n')
  const start = lines.findIndex((line) => /^#{1,6}\s+(Original proposal|Work origin)\s*$/.test(line))
  if (start < 0) return null

  const body = []
  for (let index = start + 1; index < lines.length; index += 1) {
    if (/^#{1,6}\s+/.test(lines[index])) break
    body.push(lines[index])
  }
  return {
    start: start + 1,
    body: body.join('\n').replace(/\n+$/, ''),
  }
}

/**
 * Compare the frozen origin section across two revisions of an Active file.
 *
 * Pure: callers provide the previous and current text. The function does not
 * inspect git history or infer lifecycle state from a path.
 */
export const frozenOriginViolations = (before, after) => {
  const previous = originSection(before)
  const current = originSection(after)
  const findings = []

  if (!previous || !current) {
    findings.push({
      rule: 'frozen-origin-missing',
      line: current?.start ?? 0,
      msg: 'Both Active revisions must carry an Original proposal / Work origin section',
    })
    return findings
  }

  if (previous.body !== current.body) {
    findings.push({
      rule: 'frozen-origin-mutated',
      line: current.start,
      msg: 'Active Original proposal / Work origin text must remain byte-identical',
    })
  }

  return findings
}
