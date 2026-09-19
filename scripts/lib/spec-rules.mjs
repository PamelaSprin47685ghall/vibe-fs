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

/**
 * Fail-closed duplicate clause check extracted from the retired spec gate.
 *
 * Pure: takes WHAT.md entries `{ file, pkg, text }`, returns violation objects
 * `{ file, line, msg }`. Reports every repeated clause ID and every prefix
 * owned by more than one package; the first definition wins, all later ones
 * are violations. Callers decide what to feed (live tree or temp fixture).
 */
export const duplicateClauseDefinitions = (entries) => {
  const definitions = new Map() // id -> { file, line }
  const prefixOwner = new Map() // PREFIX -> pkg
  const findings = []
  for (const { file, pkg, text } of entries) {
    for (const { id, line } of clauseDefinitionHeadings(text)) {
      const previous = definitions.get(id)
      if (previous) {
        findings.push({
          file,
          line,
          msg: `条款 ID 重复定义：${id}（已在 ${previous.file}:${previous.line} 定义）`,
        })
        continue
      }
      definitions.set(id, { file, line })
      const prefix = id.split('-')[0]
      const owner = prefixOwner.get(prefix)
      if (owner && owner !== pkg)
        findings.push({ file, line, msg: `前缀 ${prefix}- 被多包定义：${owner} 与 ${pkg}` })
      else prefixOwner.set(prefix, pkg)
    }
  }
  return findings
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

