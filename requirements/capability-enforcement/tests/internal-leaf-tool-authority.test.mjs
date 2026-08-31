// ENF-006 regression: an internal-only tool is admitted by its own authority.
//
// The Bookkeeper is an internal leaf whose prompt is HostInternal, so its
// session never carries a public PromptAuthority profile and never resolves a
// public Role. When the execute gate resolved a public Role for EVERY tool, the
// Bookkeeper's own `js-bookkeeper` was denied `tool/registry/denied-unestablished`
// before its admission ran: the tool was open to nobody, the CaseFinalize
// transaction committed the unreshaped draft Q/A, and the canonical Case never
// existed for `fetch` to resolve.

import assert from 'node:assert/strict'
import test from 'node:test'

import { admissionAuthority, privateAttachmentAdmits, rolePredicate } from '../../../dist/OpenCode/Tools/ToolRegistrySurface.js'
import { allRoleLabels } from '../../../dist/Foundation/RolesSurface.js'

const OFFICE_TOOLS = ['inspect', 'fetch', 'judge', 'join', 'chronicle', 'run', 'fork']

test('WHAT[ENF-006] internal_leaf_tool_declares_attachment_authority_not_a_public_office', () => {
  assert.equal(admissionAuthority('js-bookkeeper'), 'private-attachment')
  for (const tool of OFFICE_TOOLS) {
    assert.equal(admissionAuthority(tool), 'office', `${tool} is an office tool`)
  }
  assert.equal(admissionAuthority('no-such-tool'), 'unknown')
})

test('WHAT[ENF-006] internal_leaf_tool_is_invisible_to_every_public_office_role', () => {
  assert.ok(allRoleLabels.length > 0)
  for (const role of allRoleLabels) {
    assert.equal(rolePredicate('js-bookkeeper', role), false, `js-bookkeeper must stay invisible to ${role}`)
  }
})

test('WHAT[ENF-006] attachment_authority_is_fail_closed_without_an_attached_transaction', () => {
  assert.equal(privateAttachmentAdmits('js-bookkeeper', 'ses-never-attached'), false)
  assert.equal(privateAttachmentAdmits('js-bookkeeper', ''), false)
})

test('WHAT[ENF-006] an_office_tool_can_never_be_admitted_through_the_attachment_path', () => {
  for (const tool of OFFICE_TOOLS) {
    assert.equal(privateAttachmentAdmits(tool, 'ses-any'), false, `${tool} must not take the attachment path`)
  }
  assert.equal(privateAttachmentAdmits('no-such-tool', 'ses-any'), false)
})
