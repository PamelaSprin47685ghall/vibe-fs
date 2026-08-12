import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js'
import { StdioServerTransport } from '@modelcontextprotocol/sdk/server/stdio.js'
import { fileURLToPath } from 'node:url'
import path from 'node:path'
import { z } from 'zod'
import { createSessionStore } from './session.js'

const store = createSessionStore()

function asJsonContent(payload) {
  return {
    content: [{ type: 'text', text: JSON.stringify(payload) }],
  }
}

export function createSphinxMcpServer(sessionStore = store) {
  const server = new McpServer({
    name: 'sphinx',
    version: '0.1.0',
  })

  server.registerTool(
    'start',
    {
      title: 'Start Sphinx inquiry',
      description: 'Begin an epistemic inquiry. Returns opaque handle plus yield/answered/error payload.',
      inputSchema: {
        question: z.string().describe('Root question'),
      },
    },
    async ({ question }) => asJsonContent(sessionStore.start(question)),
  )

  server.registerTool(
    'resume',
    {
      title: 'Resume Sphinx inquiry',
      description: 'Continue an inquiry with a structured observation. Requires the same handle.',
      inputSchema: {
        handle: z.string().describe('Opaque inquiry handle from start'),
        observation: z.record(z.string(), z.any()).describe('Structured semantic observation object'),
      },
    },
    async ({ handle, observation }) => asJsonContent(sessionStore.resume(handle, observation)),
  )

  return server
}

export async function serveStdio(sessionStore = store) {
  const server = createSphinxMcpServer(sessionStore)
  const transport = new StdioServerTransport()
  await server.connect(transport)
  return server
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  serveStdio().catch((error) => {
    console.error(error)
    process.exit(1)
  })
}
