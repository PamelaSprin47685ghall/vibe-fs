import assert from 'node:assert/strict'
import { readFile } from 'node:fs/promises'
import test from 'node:test'

const templateUrl = new URL('../../../resources/wanxiangshu.mjs', import.meta.url)

test('WHAT[EMR-019] fixed DevOps model binding is immutable and cannot be changed via resume', async () => {
  const { default: route } = await import(`${templateUrl.href}?test=${Date.now()}`)
  const boundDevopsTarget = { model: 'provider/fixed-devops', reasoning: 'high' }

  // On resume/continuation, previous target must be strictly preserved
  const resumedTarget = route('devops', [boundDevopsTarget], boundDevopsTarget)
  assert.deepEqual(resumedTarget, boundDevopsTarget, 'DevOps model binding must remain immutable on resume')
})
