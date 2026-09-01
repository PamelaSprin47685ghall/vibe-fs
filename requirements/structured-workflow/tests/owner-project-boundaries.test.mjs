import assert from 'node:assert/strict'
import { readdirSync, readFileSync } from 'node:fs'
import { join, resolve } from 'node:path'
import test from 'node:test'
import { checkOwnerProjects } from '../../../scripts/checks/owner-projects.mjs'

const ROOT = resolve(import.meta.dirname, '../../..')
const SRC = join(ROOT, 'src/Wanxiangshu')

test('WHAT[STRUCTURED-WORKFLOW-011] flattened Fable emitter mirrors owner-locality source coverage', () => {
  const rootProject = readFileSync(join(SRC, 'Wanxiangshu.fsproj'), 'utf8')
  assert.match(rootProject, /<WanxiangshuEmitProject>true<\/WanxiangshuEmitProject>/)
  assert.doesNotMatch(rootProject, /<ProjectReference Include=/, 'emit project must not source-merge owner project graph')

  const ownerProjects = readdirSync(SRC).filter((name) => /^Wanxiangshu\.Owner\..+\.fsproj$/.test(name))
  assert.ok(ownerProjects.length > 1, '57.15 requires independent owner-locality projects')

  for (const project of ownerProjects) {
    const xml = readFileSync(join(SRC, project), 'utf8')
    assert.match(xml, /<WanxiangshuSemanticOwner>[^<]+<\/WanxiangshuSemanticOwner>/)
    assert.match(xml, /<WanxiangshuOwnerLocality>[^<]+<\/WanxiangshuOwnerLocality>/)
  }

  const props = readFileSync(join(SRC, 'Directory.Build.props'), 'utf8')
  assert.match(props, /<DisableTransitiveProjectReferences>true<\/DisableTransitiveProjectReferences>/)
})

test('WHAT[STRUCTURED-WORKFLOW-011] owner-locality project graph is complete, authorized, and acyclic', () => {
  const result = checkOwnerProjects()
  assert.equal(result.ok, true, result.violations.join('\n'))
  assert.ok(result.sourceCount > 0, 'owner-locality graph must cover production sources')
  assert.equal(result.contractLeakSourceCount, 0, 'published contract compile closure must contain no runtime/private source')
})
