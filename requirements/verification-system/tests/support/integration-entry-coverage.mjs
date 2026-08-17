const normalize = (file) => String(file).replaceAll('\\', '/')

const duplicatesOf = (values) => {
  const seen = new Set()
  const duplicates = new Set()
  for (const value of values) {
    if (seen.has(value)) duplicates.add(value)
    else seen.add(value)
  }
  return [...duplicates].sort()
}

/**
 * VERIFY-004/009: every integration test must be reachable from the
 * authoritative integration entry exactly once, and every wired path must
 * resolve to a discovered integration test. Tests owned by a child entrypoint
 * are declared as an exact set (`childOwnedTests`) — not a broad prefix — so
 * the parent delegates only the tests the child actually runs, and a missing,
 * stale, or duplicate child test makes the entry fail closed.
 */
export const assessIntegrationEntryCoverage = ({
  discoveredTests,
  wiredTests,
  childOwnedTests = [],
}) => {
  const childOwned = new Set(childOwnedTests.map(normalize))
  const discovered = discoveredTests
    .map(normalize)
    .filter((file) => !childOwned.has(file))
    .sort()
  const wired = wiredTests.map(normalize).sort()
  const discoveredSet = new Set(discovered)
  const wiredSet = new Set(wired)
  const missingFromEntry = discovered.filter((file) => !wiredSet.has(file))
  const staleEntry = wired.filter((file) => !discoveredSet.has(file))
  const duplicateWiring = duplicatesOf(wired)

  return {
    ok: missingFromEntry.length === 0 && staleEntry.length === 0 && duplicateWiring.length === 0,
    missingFromEntry,
    staleEntry,
    duplicateWiring,
  }
}
