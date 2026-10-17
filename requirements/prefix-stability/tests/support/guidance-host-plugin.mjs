import { appendFileSync } from 'node:fs'
import plugin from 'wanxiangshu'
import * as shared from '../../../../dist/OpenCode/Host/WorkspaceSharedJournal.js'
import * as journal from '../../../../dist/Persistence/Journal/Surface.js'
import * as trace from '../../../../dist/Context/Trace/SemanticTraceSurface.js'

export default {
  id: 'guidance-ingress-observer',
  async server(input) {
    const hooks = await plugin.server(input)
    return {
      ...hooks,
      'experimental.chat.messages.transform': async (hookInput, output) => {
        const before = structuredClone(output.messages)
        await hooks['experimental.chat.messages.transform'](hookInput, output)
        const sessionID = before.find(message => message.info?.sessionID)?.info.sessionID
        const opened = await shared.acquireSharedForWorkspace(input.directory, process.pid, new Date().toISOString())
        if (!opened.ok) throw new Error(JSON.stringify(opened.error))
        try {
          appendFileSync(process.env.WXS_GUIDANCE_OBSERVATIONS, JSON.stringify({ sessionID, before, after: output.messages, projection: await trace.currentProjection(opened.journal, sessionID) }) + '\n')
        } finally {
          journal.JournalSurface_dispose(opened.journal)
        }
      },
    }
  },
}
