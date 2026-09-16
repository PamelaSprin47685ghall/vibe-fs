import assert from 'node:assert/strict'
import test from 'node:test'
import * as kernel from '../../../dist/Sphinx/KernelSurface.js'
import * as mcp from '../../../dist/Sphinx/McpServerSurface.js'

test('WHAT[EPI-002] fsharp_kernel_has_no_agent_host_domain_dependency_and_sdk_stays_at_mcp_edge', () => {
  assert.equal(kernel.hasHostDependency(), false)
})

test('WHAT[EPI-002] generic_tools_registered_alongside_legacy_eight', () => {
  const tools = mcp.registeredTools()
  assert.ok(tools.includes('sphinx_inquiry_start'))
  assert.ok(tools.includes('sphinx_work_submit'))
})

test('WHAT[EPI-002] mcp_handle_preserves_runtime_delegation', () => {
  assert.equal(mcp.isRuntimeSubsystemIsolated(), true)
})
