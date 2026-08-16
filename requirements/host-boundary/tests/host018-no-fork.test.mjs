// host-boundary HOST-BOUNDARY-018: the product default is NOT to fork OpenCode;
// only existing Hook/SDK surfaces are used (ARCH-003). This contract test pins
// the packaging + composition-root facts that would break if a Host fork or a
// private Host API dependency were introduced.
//
// Given: the F# project file, the composition root, and the interop surface.
// When: inspecting package references, plugin entry, and import emissions.
// Expected: no Host package reference (only FSharp.Core/Fable.Core/helpers);
//       SpikePlugin only assembles wiring modules; interop imports only the
//       public @opencode-ai/plugin/tool module.
// Forbidden: referencing/forging an OpenCode source package, or importing a
//       non-public Host module path.

import assert from 'node:assert/strict'
import test from 'node:test'
import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'

const ROOT = fileURLToPath(new URL('../../..', import.meta.url))
const read = (path) => readFileSync(join(ROOT, path), 'utf8')

const fsproj = read('src/Wanxiangshu/Wanxiangshu.fsproj')
const spike = read('src/Wanxiangshu/OpenCode/Plugin/SpikePlugin.fs')
const interop = read('src/Wanxiangshu/OpenCode/Host/PluginHostInterop.fs')

test('WHAT[HOST-BOUNDARY-018] project references no OpenCode Host source package', () => {
  const packageRefs = [...fsproj.matchAll(/<PackageReference Include="([^"]+)"[^/]*\/>/g)].map((m) => m[1])
  assert.deepEqual(
    packageRefs,
    ['FSharp.Core', 'Fable.Core', 'FsToolkit.ErrorHandling', 'Thoth.Json'],
    'no OpenCode package reference: the product must not fork or vendor the Host',
  )
  assert.doesNotMatch(fsproj, /<ProjectReference[^>]*[Oo]pen[Cc]ode/, 'no project reference into Host sources')
})

test('WHAT[HOST-BOUNDARY-018] composition root only assembles existing Hook/SDK wiring', () => {
  // SpikePlugin is a pure assembly of wiring modules; any business logic or a
  // Host patch would appear here as a direct call/body.
  assert.match(spike, /PluginBoot\.create/)
  assert.match(spike, /PluginHostWiring\.create/)
  assert.match(spike, /PluginSessionWiring\.attach/)
  assert.match(spike, /PluginTransforms\.create/)
  assert.match(spike, /PluginHooks\.create/)
  assert.doesNotMatch(spike, /client\.session|SendPrompt|beginProviderAttempt/, 'no Host business-path call in the plugin entry')
})

test('WHAT[HOST-BOUNDARY-018] interop imports only the public @opencode-ai/plugin module', () => {
  const imports = [...interop.matchAll(/import\(['"]([^'"]+)['"]\)/g)].map((m) => m[1])
  for (const spec of imports) {
    assert.match(spec, /^@opencode-ai\/plugin(\/|$)/, `import spec must be the public plugin SDK, got: ${spec}`)
  }
  assert.ok(imports.length >= 1, 'interop must import the public plugin SDK surface')
})
