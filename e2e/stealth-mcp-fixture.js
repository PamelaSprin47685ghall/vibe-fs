#!/usr/bin/env node
import { Server } from '@modelcontextprotocol/sdk/server/index.js';
import { StdioServerTransport } from '@modelcontextprotocol/sdk/server/stdio.js';
import { CallToolRequestSchema, ListToolsRequestSchema } from '@modelcontextprotocol/sdk/types.js';

const server = new Server(
  { name: 'e2e-stealth-browser-mcp', version: '0.1.0' },
  { capabilities: { tools: {} } },
);

server.setRequestHandler(ListToolsRequestSchema, async () => ({
  tools: [
    {
      name: 'get_debug_view',
      description: 'Return deterministic e2e browser debug state.',
      inputSchema: { type: 'object', properties: {}, additionalProperties: false },
    },
  ],
}));

server.setRequestHandler(CallToolRequestSchema, async (request) => {
  if (request.params.name !== 'get_debug_view') {
    return { isError: true, content: [{ type: 'text', text: `unknown tool: ${request.params.name}` }] };
  }
  return { content: [{ type: 'text', text: 'e2e stealth mcp debug view' }] };
});

await server.connect(new StdioServerTransport());
