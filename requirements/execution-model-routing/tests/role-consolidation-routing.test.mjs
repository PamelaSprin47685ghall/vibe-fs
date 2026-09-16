import assert from 'node:assert/strict'
import { readFile } from 'node:fs/promises'
import test from 'node:test'

const templateUrl = new URL('../../../resources/wanxiangshu.mjs', import.meta.url)

test('WHAT[EMR-018] routing supports new role collection (Engineer, DevOps) and decouples legacy slots', async () => {
  const source = await readFile(templateUrl, 'utf8')
  assert.match(source, /export default function route/)
  const { default: route } = await import(`${templateUrl.href}?test=${Date.now()}`)

  // Must route engineer to a valid target
  const engineerTarget = route('engineer', [], null)
  assert.ok(engineerTarget, 'route("engineer") must return a valid model target')
  assert.equal(typeof engineerTarget.model, 'string')

  // Must route devops to a valid target
  const devopsTarget = route('devops', [], null)
  assert.ok(devopsTarget, 'route("devops") must return a valid model target')
  assert.equal(typeof devopsTarget.model, 'string')

  // Deprecated roles must not have routing slots in the template (must return null)
  assert.equal(route('coder', [], null), null, 'coder slot must be decoupled and return null')
  assert.equal(route('inspector', [], null), null, 'inspector slot must be decoupled and return null')
  assert.equal(route('browser', [], null), null, 'browser slot must be decoupled and return null')
  assert.equal(route('inquiry', [], null), null, 'inquiry slot must be decoupled and return null')
  assert.equal(route('distiller', [], null), null, 'distiller slot must be decoupled and return null')
})

test('WHAT[EMR-019] fixed DevOps model binding is immutable and cannot be changed via resume', async () => {
  const { default: route } = await import(`${templateUrl.href}?test=${Date.now()}`)
  const boundDevopsTarget = { model: 'provider/fixed-devops', reasoning: 'high' }

  // On resume/continuation, previous target must be strictly preserved
  const resumedTarget = route('devops', [boundDevopsTarget], boundDevopsTarget)
  assert.deepEqual(resumedTarget, boundDevopsTarget, 'DevOps model binding must remain immutable on resume')
})
