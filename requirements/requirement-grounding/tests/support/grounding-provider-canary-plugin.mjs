import assert from 'node:assert/strict'
import path from 'node:path'
import { pathToFileURL } from 'node:url'

const collectorUrl = process.env.WXS_GROUNDING_CANARY_COLLECTOR
const workspace = process.env.WXS_GROUNDING_CANARY_WORKSPACE
const sourcePath = process.env.WXS_GROUNDING_CANARY_SOURCE
const ownerRoot = process.env.WXS_GROUNDING_CANARY_DIST
for (const value of [collectorUrl, workspace, sourcePath, ownerRoot]) assert.ok(value, 'Grounding canary configuration is required')
const grounding = await import(pathToFileURL(`${ownerRoot}/OpenCode/Host/RequirementGroundingSurface.js`).href)
const pair = await import(pathToFileURL(`${ownerRoot}/OpenCode/Host/PairProgrammingThoughtSurface.js`).href)

const collect = async receipt => {
  const response = await fetch(collectorUrl, {
    method: 'POST', headers: { 'content-type': 'application/json' }, body: JSON.stringify(receipt),
  })
  assert.ok(response.ok, `Grounding collector rejected receipt: ${response.status}`)
}

export default {
  id: 'wanxiangshu-grounding-owner-provider-canary',
  async server() {
    const opened = await grounding.createJournal(path.join(workspace, '.git'))
    assert.equal(opened.ok, true, opened.error)
    return {
      async 'tool.execute.after'(input, output) {
        if (input.tool !== 'read' || input.args?.filePath !== sourcePath) return
        try {
          const decision = await grounding.observationDecision(opened.journal, workspace, input.sessionID, input.tool, input.args, output.output)
          assert.equal(decision.ok, true, decision.error)
          await collect({ kind: 'after', input, output, decision })
        } catch (error) {
          await collect({ kind: 'hook-error', stage: 'after', name: error.name, message: error.message })
          throw error
        }
      },
      async 'experimental.chat.messages.transform'(_input, output) {
        const raw = structuredClone(output.messages)
        const completed = raw.flatMap(message => (message.parts ?? []).map(part => ({ message, part })))
          .filter(({ part }) => part.type === 'tool' && part.tool === 'read' && part.state?.status === 'completed' && part.state.input?.filePath === sourcePath)
        if (!completed.length) return
        try {
          const sessionID = completed.at(-1).message.info.sessionID
          assert.ok(sessionID, 'the real Host result must retain its session identity')
          const paired = await pair.tryInject(sessionID, pair.text, raw)
          assert.equal(paired.ok, true, paired.error)
          const projected = await grounding.projectWithJournal(opened.journal, sessionID, paired.value)
          assert.equal(projected.ok, true, projected.error)
          await collect({
            kind: 'projection', sessionID, raw, paired: paired.value, projected: projected.value, guidance: pair.text,
            grounded: grounding.groundedIdentities(opened.journal, sessionID),
          })
          output.messages.length = 0
          output.messages.push(...projected.value)
        } catch (error) {
          await collect({ kind: 'hook-error', stage: 'projection', name: error.name, message: error.message })
          throw error
        }
      },
      dispose() { grounding.disposeJournal(opened.journal) },
    }
  },
}
