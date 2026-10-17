import * as journal from '../../../../dist/Persistence/Journal/Surface.js'
import * as record from '../../../../dist/Mission/WorkRecord/Surface.js'

const [directory, session, encodedRange] = process.argv.slice(2)
const opened = await journal.JournalSurface_boot(directory, 'completed-record-cold', process.pid, new Date().toISOString())
if (!opened.ok) throw new Error(JSON.stringify(opened.error))
try {
  const value = await record.lifecycleWorkRecordBounded(opened.journal, session, JSON.parse(encodedRange))
  process.stdout.write(JSON.stringify(value))
} finally {
  journal.JournalSurface_dispose(opened.journal)
}
