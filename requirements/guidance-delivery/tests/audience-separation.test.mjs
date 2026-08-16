// Detection vs remediation audience separation (boundary card OWNS:
// detection material and remediation material face different audiences,
// share one semantic identity, and do not leak each other's responsibilities).
//
// enforcer.md is Blogger system content (classification authority); main.md is
// Host-adopted guidance for Main. The authored corpus and the composed
// effective system must keep the two wings apart: main.md-only sections never
// reach the Blogger system prompt.
import assert from 'node:assert/strict'
import test from 'node:test'
import { readdirSync, readFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import * as enforcer from '../../../dist/Enforcer/Surface.js'

const ROOT = join(dirname(fileURLToPath(import.meta.url)), '../../..')
const RULEBOOK = join(ROOT, 'resources/enforcer')

const tipNames = () =>
  readdirSync(RULEBOOK, { withFileTypes: true })
    .filter((entry) => entry.isDirectory())
    .map((entry) => entry.name)
    .sort()

test('WHAT[GD-008] AUDIENCE_001_main_md_sections_never_enter_blogger_system_prompt', () => {
  const composed = enforcer.composeBloggerSystemPrompt('base', 'en')

  // `## What To Do Now` is the main.md-only remediation section (0/120
  // enforcer.md contain it). The Blogger system must never see it.
  assert.equal(composed.includes('## What To Do Now'), false)
  // The `# Enforcer Tip` marker is the Main-only delivery header.
  assert.equal(composed.includes('# Enforcer Tip'), false)
})

test('WHAT[GD-008] AUDIENCE_002_corpus_level_detection_and_remediation_do_not_leak', () => {
  for (const name of tipNames()) {
    const enforcerText = readFileSync(join(RULEBOOK, name, 'enforcer.md'), 'utf8')
    const mainText = readFileSync(join(RULEBOOK, name, 'main.md'), 'utf8')

    // Remediation protocol never re-does classification (Rulebook §22):
    // main.md must not carry the detection-only trigger section.
    assert.equal(
      mainText.includes('## Trigger When'),
      false,
      `${name}/main.md must not re-run detection (no "Trigger When" section)`,
    )
    // Detection doc never becomes a repair manual (Rulebook §21):
    // enforcer.md must not carry the remediation action section.
    assert.equal(
      enforcerText.includes('## What To Do Now'),
      false,
      `${name}/enforcer.md must not contain remediation instructions`,
    )
  }
})

test('WHAT[GD-008] AUDIENCE_003_previous_tip_history_is_not_main_authority', () => {
  // ENFORCER-071 Y side: Blogger's own history is rendered as low-trust
  // previous_enforcer_tip ([[do_not_exec]], role=assistant) — it must not be
  // repurposed as Main instruction. The Main surface is TipGuidance only.
  const tipNamesList = tipNames()
  assert.ok(tipNamesList.length === 120)
  for (const name of tipNamesList) {
    const mainText = readFileSync(join(RULEBOOK, name, 'main.md'), 'utf8')
    assert.equal(
      mainText.includes('[[do_not_exec]]'),
      false,
      `${name}/main.md must not carry the Blogger low-trust history block`,
    )
  }
})
