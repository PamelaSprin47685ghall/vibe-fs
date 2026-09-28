import assert from 'node:assert/strict'
import test from 'node:test'
import {mkdtempSync, realpathSync, rmSync} from 'node:fs'
import {tmpdir} from 'node:os'
import {join} from 'node:path'
import {fileURLToPath} from 'node:url'
import {Client} from '@modelcontextprotocol/sdk/client/index.js'
import {StdioClientTransport} from '@modelcontextprotocol/sdk/client/stdio.js'
import {Surface_isTool as isTool} from '../../../dist/Sphinx/V2/Wire/Surface.js'
import {WATCHDOG_TIMEOUT_MS} from '../../verification-system/tests/e2e/support/time-budget.js'

const allowed = ['sphinx_inquiry_start', 'sphinx_work_next', 'sphinx_work_submit', 'sphinx_inquiry_status', 'sphinx_inquiry_cancel', 'sphinx_inquiry_export', 'sphinx_goal_amend']

test('WHAT[sphinx-v2-036] actual tool contract admits the v2 names and rejects retired stage aliases', () => {
  for (const name of allowed) assert.equal(isTool(name), true, name)
  for (const name of ['sphinx_assess', 'sphinx_propose', 'sphinx_investigate', 'sphinx_synthesize', 'sphinx', 'sphinx_inquiry_start_extra', 'SPHINX_WORK_NEXT']) {
    assert.equal(isTool(name), false, name)
  }
})

test('WHAT[sphinx-v2-036] the actual stdio server starts and publishes all seven tool schemas', async () => {
  const directory = mkdtempSync(join(tmpdir(), 'sphinx-v2-mcp-'))
  const client = new Client({name: 'requirements-sphinx-v2', version: '1'})
  const transport = new StdioClientTransport({
    command: process.execPath,
    args: [realpathSync(fileURLToPath(new URL('../../../dist/Sphinx/V2/ServeEntry.js', import.meta.url)))],
    env: {SPHINX_COMMON_DIR: directory},
    stderr: 'pipe',
  })
  let diagnostics = ''
  transport.stderr.setEncoding('utf8')
  transport.stderr.on('data', chunk => { diagnostics += chunk })
  try {
    await client.connect(transport, {timeout: WATCHDOG_TIMEOUT_MS})
    const result = await client.listTools({}, {timeout: WATCHDOG_TIMEOUT_MS})
    assert.deepEqual(result.tools.map(tool => tool.name).sort(), [...allowed].sort())
    for (const tool of result.tools) assert.equal(tool.inputSchema.properties.commandId.type, 'string')
  } catch (error) {
    throw new Error(`Sphinx MCP startup failed: ${diagnostics}`, {cause: error})
  } finally {
    await client.close()
    await transport.close()
    rmSync(directory, {recursive: true, force: true})
  }
})

test.todo('WHAT[sphinx-v2-036] registered MCP tools route their actual arguments through the single runtime and status/export create no work or state changes')
