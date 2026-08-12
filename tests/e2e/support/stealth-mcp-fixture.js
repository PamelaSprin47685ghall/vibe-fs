#!/usr/bin/env node
import { createInterface } from 'node:readline'

if (process.env.STEALTH_BROWSER_MCP_FAIL === 'true') {
  console.error('Simulated MCP launch failure')
  process.exit(1)
}

const tools = [
  {
    name: 'get_debug_view',
    description: 'Return deterministic e2e browser debug state.',
    inputSchema: { type: 'object', properties: {}, additionalProperties: false },
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
      serverInfo: { name: 'e2e-stealth-browser-mcp', version: '0.1.0' },
    })
    return
  }
  if (method === 'notifications/initialized') return
  if (method === 'tools/list') {
    respond(id, { tools })
    return
  }
  if (method === 'tools/call') {
    if (params?.name !== 'get_debug_view') {
      respond(id, { isError: true, content: [{ type: 'text', text: `unknown tool: ${params?.name}` }] })
      return
    }
    respond(id, { content: [{ type: 'text', text: 'e2e stealth mcp debug view' }] })
    return
  }
  if (id !== undefined) write({ jsonrpc: '2.0', id, error: { code: -32601, message: `unknown method: ${method}` } })
})
