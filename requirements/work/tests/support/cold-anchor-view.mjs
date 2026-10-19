import assert from 'node:assert/strict'
import * as journal from '../../../../dist/Persistence/Journal/Surface.js'
import * as anchor from '../../../../dist/Repository/Programming/Js/Anchor/AnchorSurface.js'
import * as trace from '../../../../dist/Context/Trace/SemanticTraceSurface.js'

const [directory, sessionId, messageId, encodedText] = process.argv.slice(2)
const text = JSON.parse(encodedText)
const opened = await journal.JournalSurface_acquireSharedForWorkspace(directory, process.pid, new Date().toISOString())
assert.equal(opened.ok, true, JSON.stringify(opened.error))
try {
  const checkpoints = journal.JournalSurface_snapshot(opened.journal).viewCheckpoints
  const anchorsBefore = anchor.anchors(sessionId)
  const declaration = anchor.takeView(sessionId)
  anchor.numberMessage('unrelated-session', 'unrelated-message')
  const renumberedAnchor = anchor.numberMessage(sessionId, messageId)
  const projected = anchor.applyView(sessionId, [{ id: messageId, text }])
  const original = await trace.currentProjection(opened.journal, sessionId)
  console.log(JSON.stringify({
    checkpoints, anchorsBefore, declaration, renumberedAnchor,
    entries: projected.entries.map(entry => ({ kind: entry.kind, text: entry.message.text })),
    userOriginals: original.messages.filter(message => message.role === 'user').flatMap(message => message.parts).filter(part => part.kind === 'text').map(part => part.text),
  }))
} finally {
  journal.JournalSurface_dispose(opened.journal)
}
