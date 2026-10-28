import { mkdirSync, mkdtempSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { dirname, join } from 'node:path'

export function createManifestFixture(t) {
  const root = mkdtempSync(join(tmpdir(), 'surface-manifest-'))
  t.after(() => rmSync(root, { recursive: true, force: true }))
  const write = (file, text) => {
    const target = join(root, file)
    mkdirSync(dirname(target), { recursive: true })
    writeFileSync(target, text)
    return target
  }
  const entry = {
    module: 'Owner/Surface.js', owner: 'owner', laws: ['owner-001'],
    source: 'src/Wanxiangshu/Owner/Surface.fs', representation: 'json', kind: 'pure',
  }
  const testFile = 'requirements/consumer/tests/001.test.mjs'
  const importPath = '../../../' + 'dist/' + entry.module
  write('requirements/owner/WHAT.md', '# owner — WHAT\n\n## [001] Contract\n')
  write(entry.source, 'module Owner.Surface\nlet value = 1\n')
  write('src/Wanxiangshu/Wanxiangshu.Owner.owner.fsproj', '<Project><ItemGroup><Compile Include="Owner/Surface.fs"/></ItemGroup></Project>')
  write('dist/' + entry.module, 'export const value = 1\n')
  write(testFile, `import { value } from ${JSON.stringify(importPath)}\n`)
  return { root, write, entry, testFile, importPath }
}
