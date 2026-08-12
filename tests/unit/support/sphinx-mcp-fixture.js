#!/usr/bin/env node
import { createInterface } from 'node:readline'

if (process.env.SPHINX_MCP_FAIL === 'true') {
  console.error('Simulated Sphinx MCP launch failure')
  process.exit(1)
}

const tools = [
  {
    name: 'start',
    description: 'Begin a Sphinx inquiry; returns handle and first yield or answer.',
    inputSchema: {
      type: 'object',
      properties: { question: { type: 'string' } },
      required: ['question'],
      additionalProperties: false,
    },
  },
  {
    name: 'resume',
    description: 'Continue a Sphinx inquiry with an observation for the given handle.',
    inputSchema: {
      type: 'object',
      properties: {
        handle: { type: 'string' },
        observation: { type: 'object' },
      },
      required: ['handle', 'observation'],
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
      serverInfo: { name: 'unit-sphinx-mcp', version: '0.1.0' },
    })
    return
  }
  if (method === 'notifications/initialized') return
  if (method === 'tools/list') {
    respond(id, { tools })
    return
  }
  if (method === 'tools/call') {
    const name = params?.name
    if (name === 'start') {
      respond(id, {
        content: [
          {
            type: 'text',
            text: JSON.stringify({
              handle: 'fixture-handle',
              status: 'yield',
              request: { kind: 'SemanticAssessmentRequest', question: params?.arguments?.question ?? '' },
            }),
          },
        ],
      })
      return
    }
    if (name === 'resume') {
      respond(id, {
        content: [
          {
            type: 'text',
            text: JSON.stringify({
              handle: params?.arguments?.handle ?? '',
              status: 'answered',
              answer: { text: 'fixture answer' },
            }),
          },
        ],
      })
      return
    }
    respond(id, { isError: true, content: [{ type: 'text', text: `unknown tool: ${name}` }] })
    return
  }
  if (id !== undefined) write({ jsonrpc: '2.0', id, error: { code: -32601, message: `unknown method: ${method}` } })
})
