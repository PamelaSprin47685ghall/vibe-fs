/**
 * P4U2 GATE-NO-MIGRATOR RED fixture: one-shot legacy migrator + projection equivalence.
 * Amendment G3.5-A forbids LegacyProjection≡NewProjection suites and wanxiangshu-next
 * NDJSON → EventStore importers (leave-unread clean-break only).
 */

import { readFileSync } from 'node:fs'
import { join } from 'node:path'

export const LegacyMigrator = {
  readLegacyJournal(runtimeDir) {
    return readFileSync(join(runtimeDir, 'wanxiangshu-next', 'runtimes', 'r.ndjson'), 'utf8')
  },
  async run(legacyNdjson) {
    // Forbidden shape: claim legacy/new projection equivalence after import.
    const LegacyProjection = foldLegacy(legacyNdjson)
    const NewProjection = foldEventStore(importToEventStore(legacyNdjson))
    return LegacyProjection == NewProjection
  },
}

function foldLegacy() {
  return {}
}
function importToEventStore() {
  return []
}
function foldEventStore() {
  return {}
}
