// requirements/distribution/tests/fixtures/create-bad-archive.mjs
// Helper to construct a synthetic bad archive fixture containing
// path traversal, duplicate members, or banned members for negative tests.

import fs from 'node:fs'
import os from 'node:os'
import path from 'node:path'
import * as tar from 'tar'

export async function createBadArchive(outputPath, options = {}) {
  const tmp = fs.mkdtempSync(path.join(os.tmpdir(), 'wx-bad-archive-'))
  const src = path.join(tmp, 'src')
  fs.mkdirSync(path.join(src, 'package'), { recursive: true })

  fs.writeFileSync(path.join(src, 'package', 'package.json'), '{"name":"wanxiangshu"}')
  fs.writeFileSync(path.join(src, 'package', 'README.md'), '# Wanxiangshu')

  if (options.withInfiltrated) {
    fs.mkdirSync(path.join(src, 'package', 'src'), { recursive: true })
    fs.writeFileSync(path.join(src, 'package', 'src', 'App.fs'), '// source leak')
  }

  if (options.withSymlink) {
    fs.symlinkSync('README.md', path.join(src, 'package', 'symlink.txt'))
  }

  await tar.c(
    {
      gzip: true,
      file: outputPath,
      cwd: src,
    },
    ['package'],
  )

  fs.rmSync(tmp, { recursive: true, force: true })
}
