import assert from 'node:assert/strict'
import { execFileSync } from 'node:child_process'
import { join } from 'node:path'
import * as trace from '../../../../dist/Context/Trace/SemanticTraceSurface.js'
import { acceptAuthorityRoot, withExecutablePlugin } from '../../../verification-system/tests/support/plugin-fixture.mjs'

export const separator = '\0\uFEFF'
export const toolBody = 'Original file content\r\n'

export async function withGuidanceHost(status, action, identity = status) {
  await withExecutablePlugin(async (hooks, directory, _created, runtime) => {
    const sessionID = `ses-guidance-ingress-${identity}`
    const root = { info: { id: 'msg-root', sessionID, role: 'user', agent: 'manager', time: { created: 1 } }, parts: [{ id: 'part-root', type: 'text', text: 'read a file' }] }
    const result = { info: { id: 'msg-read', sessionID, role: 'assistant', agent: 'manager', parentID: root.info.id, finish: 'tool-calls', time: { created: 2, completed: 3 } }, parts: [{ id: 'part-read', type: 'tool', tool: 'read', callID: 'call-read', state: { status, input: { filePath: 'x' }, [status === 'error' ? 'error' : 'output']: toolBody, time: { start: 2, end: 3 } } }] }
    const running = { info: { id: 'msg-next', sessionID, role: 'assistant', agent: 'manager', parentID: root.info.id, providerID: 'provider', modelID: 'manager-model', time: { created: 4 } }, parts: [] }
    const raw = [root, result]
    raw.forEach(message => runtime.pushHostMessage(sessionID, message))
    runtime.pushHostMessage(sessionID, running)
    await acceptAuthorityRoot(runtime, sessionID, 'manager', root.info.id)
    await hooks['chat.message']({ sessionID, messageID: root.info.id, agent: 'manager' }, { message: root.info, parts: root.parts })
    const transform = async messages => {
      const output = { messages: structuredClone(messages) }
      await hooks['experimental.chat.messages.transform']({ sessionID }, output)
      return output.messages
    }
    const first = await transform(raw)
    const textOf = message => message.parts[0].state[status === 'error' ? 'error' : 'output']
    const presented = textOf(first.find(message => message.info.id === result.info.id))
    assert.ok(presented.startsWith(toolBody + separator))
    const guidance = presented.slice(toolBody.length + separator.length)
    const current = () => trace.currentProjection(runtime.journal, sessionID)
    const head = () => trace.headCursor(trace.snapshot(runtime.journal, sessionID))
    const cold = () => {
      const script = `
        import * as journal from ${JSON.stringify(new URL('../../../../dist/Persistence/Journal/Surface.js', import.meta.url).href)};
        import * as trace from ${JSON.stringify(new URL('../../../../dist/Context/Trace/SemanticTraceSurface.js', import.meta.url).href)};
        const opened = await journal.JournalSurface_boot(process.argv[1], 'guidance-cold', process.pid, new Date().toISOString());
        if (!opened.ok) throw new Error(JSON.stringify(opened.error));
        try { process.stdout.write(JSON.stringify({ pid: process.pid, projection: await trace.currentProjection(opened.journal, process.argv[2]) })); }
        finally { journal.JournalSurface_dispose(opened.journal); }
      `
      return JSON.parse(execFileSync(process.execPath, ['--input-type=module', '-e', script, join(directory, '.git'), sessionID], { encoding: 'utf8', env: { ...process.env, WXS_E2E_QUIET: '1' } }))
    }
    await action({ hooks, runtime, sessionID, raw, first, result, guidance, transform, current, head, cold, textOf,
      quote: async (text, { physicalPart = true, multiple = false } = {}) => {
        const message = { info: { id: 'msg-quote', sessionID, role: 'user', agent: 'manager', time: { created: 6 } }, parts: [
          ...(multiple ? [{ id: 'part-prior', type: 'text', text: 'Prior user part' }] : []),
          { ...(physicalPart ? { id: 'part-quote' } : {}), type: 'text', text },
        ] }
        runtime.pushHostMessage(sessionID, message)
        runtime.pushHostMessage(sessionID, { info: { ...running.info, id: 'msg-quoted-run', parentID: message.info.id, time: { created: 7 } }, parts: [] })
        await hooks['chat.message']({ sessionID, messageID: message.info.id, agent: 'manager' }, { message: message.info, parts: message.parts })
        const output = await transform([...raw, message])
        return { message, presented: output.find(item => item.info.id === message.info.id).parts.at(-1).text }
      },
    })
  })
}
