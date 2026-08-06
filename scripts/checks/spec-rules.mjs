const CLAUSE_LIKE_RE = /\b([A-Z][A-Z0-9]*-\d{3}(?:-[A-Z0-9-]+)?)\b/g
const NON_CLAUSE_IDENTIFIERS = new Set(['SHA-256'])

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

/** Compare README links for one fluid directory with its exact Markdown file set. */
export const fluidNavigationProblems = (navigation, directory, files) => {
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

/** Backward-compatible specialization used by callers and pure tests. */
export const statusNavigationProblems = (navigation, statusFiles) =>
  fluidNavigationProblems(navigation, 'status', statusFiles)
