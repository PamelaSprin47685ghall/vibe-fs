import assert from 'node:assert/strict'
import test from 'node:test'
import { rmSync } from 'node:fs'
import { join } from 'node:path'
import { integrationTest } from '../../verification-system/tests/support/tier-gate.mjs'
import { withRulebookPackage } from './support/resource-package.mjs'

integrationTest('WHAT[behavior-diagnosis-002] real resource loader refuses empty missing blank and invalid packaged rules', async () => {
  await withRulebookPackage(async ({ rulebook, write, surface }) => {
    assert.throws(() => surface.rules(), /rulebook empty/)
    write('sample-rule', 'enforcer.md', 'Detection body')
    assert.throws(() => surface.rules(), /main.md/)
    write('sample-rule', 'main.md', '  \n')
    assert.throws(() => surface.rules(), /main.md empty/)
    write('sample-rule', 'main.md', 'Main body')
    assert.equal(surface.rules()[0].name, 'sample-rule')
    write('sample-rule', 'enforcer.md', '\n\t')
    assert.throws(() => surface.rules(), /enforcer.md empty/)
    write('sample-rule', 'enforcer.md', 'Detection body')
    write('Invalid_Name', 'enforcer.md', 'Body')
    assert.throws(() => surface.rules(), /lower-kebab-case/)
    rmSync(join(rulebook, 'Invalid_Name'), { recursive: true })
    assert.equal(surface.rules().length, 1)
    rmSync(rulebook, { recursive: true })
    assert.throws(() => surface.rules(), /package resource missing/)
  })
})

test.todo('WHAT[behavior-diagnosis-002] GAP-112 actual plugin startup converts resource failure to process fail-fast; institutional preflight revision race commits nothing')
