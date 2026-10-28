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
    module: 'Owner/Surface.js',
    source: 'src/Wanxiangshu/Owner/Surface.fs',
  }
  write(entry.source, 'module Owner.Surface\nlet value = 1\n')
  write('src/Wanxiangshu/Wanxiangshu.Owner.owner.fsproj', '<Project><ItemGroup><Compile Include="Owner/Surface.fs"/></ItemGroup></Project>')
  write('dist/' + entry.module, 'export const value = 1\n')
  return { root, write, entry }
}
