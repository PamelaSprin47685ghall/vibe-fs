const normalizePath = (value) => value.replace(/\\/g, '/').replace(/^\.\//, '')
const semanticSource = (value) => {
  const path = normalizePath(value)
  return path.endsWith('.fsi') ? path.slice(0, -1) : path
}
const byText = (left, right) => left.localeCompare(right)

const requireText = (value, label) => {
  if (typeof value !== 'string' || value.trim().length === 0) throw new Error(`${label} must be non-empty text`)
  return value.trim()
}

export function analyzeLocalityDependencies({ localities, declarationUses } = {}) {
  if (!Array.isArray(localities)) throw new Error('localities must be an array')
  if (!Array.isArray(declarationUses)) throw new Error('declarationUses must be an array')

  const localityById = new Map()
  const localityOfSource = new Map()
  for (const candidate of localities) {
    const id = requireText(candidate?.id, 'locality id')
    if (localityById.has(id)) throw new Error(`duplicate locality id: ${id}`)
    const sources = [...new Set((candidate.sources ?? []).map(semanticSource))].sort(byText)
    if (sources.length === 0) throw new Error(`${id}: locality must compile at least one source`)
    const references = [...new Set((candidate.references ?? []).map((value) => requireText(value, `${id} reference`)))].sort(byText)
    const locality = { id, owner: requireText(candidate.owner ?? id, `${id} owner`), sources, references }
    localityById.set(id, locality)
    for (const source of sources) {
      const previous = localityOfSource.get(source)
      if (previous) throw new Error(`${source}: compiled by multiple localities (${previous}, ${id})`)
      localityOfSource.set(source, id)
    }
  }

  for (const locality of localityById.values())
    for (const reference of locality.references)
      if (!localityById.has(reference)) throw new Error(`${locality.id}: unknown locality reference ${reference}`)

  const closureMemo = new Map()
  const closureOf = (id, visiting = new Set()) => {
    if (closureMemo.has(id)) return closureMemo.get(id)
    if (visiting.has(id)) throw new Error(`locality reference cycle includes ${id}`)
    const nextVisiting = new Set(visiting).add(id)
    const closure = new Set()
    for (const reference of localityById.get(id).references) {
      closure.add(reference)
      for (const transitive of closureOf(reference, nextVisiting)) closure.add(transitive)
    }
    closureMemo.set(id, closure)
    return closure
  }
  for (const id of localityById.keys()) closureOf(id)

  const normalizedUses = declarationUses
    .map((entry) => ({
      ...entry,
      consumerPath: semanticSource(requireText(entry?.consumerPath, 'compiler use consumerPath')),
      providerPaths: [...new Set((entry?.providerPaths ?? []).map(semanticSource))].sort(byText),
      symbol: typeof entry?.symbol === 'string' ? entry.symbol : '',
      isNamespace: entry?.isNamespace === true,
      line: Number.isInteger(entry?.line) ? entry.line : 0,
      column: Number.isInteger(entry?.column) ? entry.column : 0,
    }))
    .sort((left, right) =>
      `${left.consumerPath}\0${left.line}\0${left.column}\0${left.symbol}`.localeCompare(
        `${right.consumerPath}\0${right.line}\0${right.column}\0${right.symbol}`,
      ),
    )

  const edgeBySources = new Map()
  for (const use of normalizedUses) {
    if (use.isNamespace) continue
    const consumerLocality = localityOfSource.get(use.consumerPath)
    if (!consumerLocality) throw new Error(`${use.consumerPath}: compiler consumer has no locality`)
    const productionProviders = use.providerPaths.filter((path) => localityOfSource.has(path))
    if (productionProviders.length === 0) continue
    if (productionProviders.length > 1)
      throw new Error(`${use.consumerPath}:${use.line}:${use.column}: declaration resolves to multiple production sources`)
    const providerSource = productionProviders[0]
    const providerLocality = localityOfSource.get(providerSource)
    if (providerSource === use.consumerPath || providerLocality === consumerLocality) continue
    const key = `${use.consumerPath}\0${providerSource}`
    if (!edgeBySources.has(key)) {
      edgeBySources.set(key, {
        consumerSource: use.consumerPath,
        consumerLocality,
        providerSource,
        providerLocality,
        symbol: use.symbol,
        line: use.line,
        column: use.column,
      })
    }
  }

  const edges = [...edgeBySources.values()].sort((left, right) =>
    `${left.consumerLocality}\0${left.providerLocality}\0${left.consumerSource}\0${left.providerSource}`.localeCompare(
      `${right.consumerLocality}\0${right.providerLocality}\0${right.consumerSource}\0${right.providerSource}`,
    ),
  )
  const violations = edges
    .filter((edge) => !closureOf(edge.consumerLocality).has(edge.providerLocality))
    .map((edge) => ({ code: 'missing-closure-edge', ...edge }))

  return {
    edges,
    violations,
    census: {
      localities: localityById.size,
      sources: localityOfSource.size,
      actualSourceEdges: edges.length,
      missingClosureEdges: violations.length,
    },
  }
}
