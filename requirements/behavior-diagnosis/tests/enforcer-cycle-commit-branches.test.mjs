// PERSIST-010 cycle commit prechecks at the Enforcer/Blog owner boundary.
import assert from 'node:assert/strict'
import test from 'node:test'
import * as enforcer from '../../../dist/Enforcer/Surface.js'
import * as blog from '../../../dist/Enforcer/BlogSurface.js'

test('WHAT[BD-013] ENFORCER_commit_classification_exposes_named_semantic_branches', () => {
  assert.equal(blog.classifyCommit({ callCount: 0, providerRun: 'run', tip: 'primitive-obsession' }).branch, 'ProtocolRepair')
  assert.equal(blog.classifyCommit({ callCount: 1, providerRun: '', tip: 'primitive-obsession' }).branch, 'Fatal')
  assert.equal(blog.classifyCommit({ callCount: 1, providerRun: 'run', tip: '' }).branch, 'ProtocolRepair')
  assert.equal(blog.classifyCommit({ callCount: 1, providerRun: 'run', tip: 'not-a-field' }).branch, 'ProtocolRepair')
  assert.equal(blog.classifyCommit({ callCount: 1, providerRun: 'run', tip: 'primitive-obsession' }).branch, 'Committed')
  assert.equal(enforcer.validateProviderRun('run').ok, true)
})
