#!/usr/bin/env node
import { createInterface } from 'node:readline'

if (process.env.SEMBLE_MCP_FAIL === 'true') {
  console.error('Simulated Semble MCP launch failure')
  process.exit(1)
}

const tools = [
  {
    name: 'search',
    description: 'Return deterministic semantic search hits.',
    inputSchema: {
      type: 'object',
      properties: {
        query: { type: 'string' },
        repo: { type: 'string' },
        top_k: { type: 'number' },
        max_snippet_lines: { type: 'number' },
      },
      additionalProperties: false,
    },
  },
]

const write = (payload) => {
  process.stdout.write(`${JSON.stringify(payload)}\n`)
}

const respond = (id, result) => write({ jsonrpc: '2.0', id, result })

const rl = createInterface({ input: process.stdin })
rl.on('line', (line) => {
  if (!line.trim()) return
  let message
  try {
    message = JSON.parse(line)
  } catch {
    return
  }
  const { id, method, params } = message
  if (method === 'initialize') {
    respond(id, {
      protocolVersion: '2024-11-05',
      capabilities: { tools: {} },
      serverInfo: { name: 'unit-semble-mcp', version: '0.1.0' },
    })
    return
  }
  if (method === 'notifications/initialized') return
  if (method === 'tools/list') {
    respond(id, { tools })
    return
  }
  if (method === 'tools/call') {
    if (params?.name !== 'search') {
      respond(id, { isError: true, content: [{ type: 'text', text: `unknown tool: ${params?.name}` }] })
      return
    }
    const args = params?.arguments ?? {}
    respond(id, {
      content: [
        {
          type: 'text',
          text: JSON.stringify({
            results: [
              {
                file_path: 'src/Example.fs',
                start_line: 10,
                end_line: 20,
                content: `query=${args.query};repo=${args.repo};top_k=${args.top_k};max_snippet_lines=${args.max_snippet_lines}`,
                score: 0.91,
                total_lines: 40,
              },
            ],
          }),
        },
      ],
    })
    return
  }
  if (id !== undefined) write({ jsonrpc: '2.0', id, error: { code: -32601, message: `unknown method: ${method}` } })
})
