import assert from 'node:assert/strict'
import test from 'node:test'
import { parse as parseToml } from 'smol-toml'
import { render as renderFork } from '../../../dist/Execution/Delegation/Fork/Surface.js'
import * as join from '../../../dist/Execution/Delegation/Fork/JoinSurface.js'

// WHAT[delegation-034] Bidirectional renderer smoke:
// - Parent->child: main prose (task assignment) renders at TOP as `# ` comment lines;
//   caller carry/material is NEVER forwarded; parent task appears exactly once.
// - Child->parent: main prose (child report) renders at TOP as `# ` comment lines;
//   carry renders as TOML data fields (`[[returned]]`); child report is NEVER
//   quoted in a data field.
// - Both directions: parseable TOML; exact text not double-escaped.

test('WHAT[delegation-034] parent->child: current prose and requirements only, inherited carry is absent', () => {
  const multilineTask = 'First line of assignment.\nSecond line with "quotes" & \\backslashes.\nThird line.'
  const carryEvidence = 'Durable material evidence from prior work.'
  const payloadContent = 'Raw reference payload string.'

  for (const lang of ['en', 'zh-CN']) {
    const document = renderFork(lang, {
      Assignment: multilineTask,
      CommissionerRecord: [carryEvidence],
      Attachment: 'peer-work-record-token',
      RootRequirements: ['Preserve existing invariants.'],
      Payload: payloadContent,
    })

    // 1. Parseable TOML
    const parsed = parseToml(document)

    // 2. Multiline prose at top as `# ` comments
    assert.ok(document.startsWith('# First line of assignment.\n'), 'document must start with first prose comment')
    assert.match(document, /^# Second line with "quotes" & \\backslashes\.$/m, 'second line must be exact comment')
    assert.match(document, /^# Third line\.$/m, 'third line must be exact comment')

    // 3. Exact text not double-escaped in comments (no escaped quotes inside `# ` lines)
    assert.ok(document.includes('# Second line with "quotes" & \\backslashes.'))
    assert.ok(!document.includes('\\"quotes\\"'), 'comment line must not escape double quotes')

    // 4. Clean break: no parent history can enter the data plane.
    assert.deepEqual(parsed, {}, 'fork child request must have NO historical data plane')
    for (const historical of [payloadContent, carryEvidence, 'peer-work-record-token']) {
      assert.ok(!document.includes(historical), 'historical material must be absent')
    }
    assert.match(document, /^# Preserve existing invariants\.$/m)

    // 5. Parent task appears exactly once across the whole document
    const matches = document.match(/First line of assignment/g)
    assert.equal(matches?.length, 1, 'parent task assignment must appear exactly once')

    // 6. Assignment text is NEVER wrapped in a TOML data field
    assert.equal(parsed.Assignment, undefined, 'Assignment must not be a data field')
    assert.equal(parsed.assignment, undefined, 'assignment must not be a data field')
    assert.equal(parsed.task, undefined, 'task must not be a data field')
  }
})

test('WHAT[delegation-034] child->parent: multiline report at top as comments, carry as fields, never data-field quoted', () => {
  const multilineReport = 'Investigation report:\n1. Found the root cause in persistence.\n2. Verified the fix locally.\n\nAll green.'
  const carryItem1 = 'patch: diff --git a/file b/file'
  const carryItem2 = 'log: [2026-10-23] verified'

  for (const lang of ['english', 'zh-CN']) {
    const wire = join.renderBatch(lang, [{
      kind: 'completed',
      agentId: 'agent-42',
      agentName: 'Ada',
      role: 'Engineer',
      runId: 'run-42',
      body: multilineReport,
      returned: [{ text: carryItem1 }, { text: carryItem2 }],
    }])

    // 1. Parseable TOML
    const parsed = parseToml(wire)

    // 2. Byname line is present as FYI comment header
    assert.match(wire, /Ada/, 'wire must name the completed agent')

    // 3. Child report renders as `# ` comment lines
    assert.match(wire, /^# Investigation report:$/m, 'first line of report must be a comment')
    assert.match(wire, /^# 1\. Found the root cause in persistence\.$/m, 'second line must be a comment')
    assert.match(wire, /^# 2\. Verified the fix locally\.$/m, 'third line must be a comment')
    assert.match(wire, /^# All green\.$/m, 'final line must be a comment')

    // 4. Exact text: no double-escaping inside comments
    assert.ok(wire.includes('# 1. Found the root cause in persistence.'))

    // 5. Carry material renders as data fields (`[[returned]]`) after top prose
    assert.deepEqual(parsed.returned, [
      { text: carryItem1 },
      { text: carryItem2 },
    ], 'only carry material must populate the returned data table')

    // 6. Child report is NEVER data-field quoted (not in any triple-quoted or single-quoted TOML field)
    assert.ok(!wire.includes('"""\nInvestigation report'), 'child report must never be triple-quoted')
    assert.ok(!wire.includes("'''\nInvestigation report"), 'child report must never be single-quoted multiline')
    assert.equal(parsed.report, undefined, 'report must not be a data field')
    assert.equal(parsed.body, undefined, 'body must not be a data field')
    assert.equal(parsed.text, undefined, 'text must not be a top-level data field')

    // 7. Carry does NOT duplicate the child report
    for (const item of parsed.returned) {
      assert.ok(!item.text.includes('Investigation report'), 'carry item must not contain child report')
    }
  }
})
