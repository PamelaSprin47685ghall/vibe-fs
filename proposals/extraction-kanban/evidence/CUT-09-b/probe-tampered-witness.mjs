import { readdirSync, readFileSync, writeFileSync } from 'node:fs'
import { join } from 'node:path'
import * as journal from '../../../../dist/Persistence/Journal/Surface.js'
import * as lifecycle from '../../../../dist/Mission/Lifecycle/Surface.js'
import { withSharedJournal, acceptOwner } from '../../../../requirements/interaction-authority/tests/support/authority.mjs'
const variant = process.argv[2]
await withSharedJournal('probe2', async (handle, reopen, dir) => {
  const store = journal.JournalSurface_eventStore(handle)
  const s = 'probe-s'
  await acceptOwner(handle, s)
  await lifecycle.seedOpen(store, 'S4', s, dir, 1n); await lifecycle.seedReview(store, s, 'Promoted'); await lifecycle.seedDeliver(store, s, 'run', 'c1', 2n); await lifecycle.seedDeliver(store, s, 'run', 'c2', 3n)
  const ev = join(dir, '.Herdchestra', 'wanxiangshu', 'events')
  journal.JournalSurface_dispose(handle)
  for (const f of readdirSync(ev).filter(f=>f.endsWith('.ndjson'))) {
    const p = join(ev, f); const lines = readFileSync(p, 'utf8').split('\n')
    writeFileSync(p, lines.map((l) => {
      if (!l.includes('AuthorityLogicalRunClosed')) return l
      const o = JSON.parse(l); const pl = o.payload.Fact[1][1][1]
      const [, life, fact] = JSON.parse(pl.ClosureWitness)
      pl.ClosureWitness = variant === 'legacy' ? fact : variant === 'foreign' ? JSON.stringify(['ManagedLogicalRunTerminal', pl.LogicalRunId[1], fact]) : JSON.stringify(['LifeCompleted', 'other-session', fact])
      return JSON.stringify(o)
    }).join('\n'))
  }
  const r = await journal.JournalSurface_acquireSharedForWorkspace(dir, process.pid, '2026-10-14T00:00:00Z')
  console.log(variant, 'reopen ok=', r.ok, r.ok ? '' : JSON.stringify(r.error).slice(0, 400))
  if (r.ok) { const pa = journal.JournalSurface_snapshot(r.journal).sessionProjections[s]?.promptAuthority; console.log('active', JSON.stringify(pa?.activeLogicalRun)?.slice(0, 120)); journal.JournalSurface_dispose(r.journal) }
}).catch(e => console.log('threw', String(e).slice(0, 300)))
