// requirements/verification-system/tests/support-cleaning-gates.test.mjs
// 自保测试：验证清理 Gate 8/9 后保留的 Gate 9b (adapter-namespace-folder) 依然能有效识别命名空间与目录不匹配的情况。
// 注意：本测试按工程规程编写，由 DevOps 执行，本角色不执行运行。

import assert from 'node:assert/strict'
import test from 'node:test'
import { scanArchitecture } from '../../../scripts/checks/architecture.mjs'

test('scanArchitecture: Gate 9b correctly flags adapter code living outside OpenCode folder', () => {
  const mockFiles = ['src/Wanxiangshu/Foundation/SneakyAdapter.fs']
  const mockSources = {
    'src/Wanxiangshu/Foundation/SneakyAdapter.fs': 'namespace Wanxiangshu.Foundation.OpenCode\n\ntype Foo = int'
  }
  const read = (path) => mockSources[path] ?? ''
  const { violations } = scanArchitecture(mockFiles, read)

  const g9bViolations = violations.filter((v) => v.gate === 'adapter-namespace-folder')
  assert.equal(g9bViolations.length, 1)
  assert.match(g9bViolations[0].message, /declares adapter namespace .* outside an OpenCode\/ folder/)
})

test('scanArchitecture: Gate 9b correctly flags non-adapter code living inside OpenCode folder', () => {
  const mockFiles = ['src/Wanxiangshu/OpenCode/Something.fs']
  const mockSources = {
    'src/Wanxiangshu/OpenCode/Something.fs': 'namespace Wanxiangshu.Domain.Something\n\ntype Bar = string'
  }
  const read = (path) => mockSources[path] ?? ''
  const { violations } = scanArchitecture(mockFiles, read)

  const g9bViolations = violations.filter((v) => v.gate === 'adapter-namespace-folder')
  assert.equal(g9bViolations.length, 1)
  assert.match(g9bViolations[0].message, /lives under an OpenCode\/ adapter folder but declares/)
})
