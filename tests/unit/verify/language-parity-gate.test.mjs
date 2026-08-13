/**
 * ARCH-016 Gate C — provider language parity + AC20 identifier isomorphism.
 * ARCH-016 Gate F — office capability integrity.
 */
import assert from 'node:assert/strict'
import { mkdirSync, mkdtempSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join, resolve } from 'node:path'
import test from 'node:test'
import {
  LOCALE_FILES,
  PROVIDER_ROOT,
  extractCodeSpans,
  extractPlaceholders,
  extractProtocolIdentifiers,
  listSemanticResourceDirs,
  scanIdentifierParity,
  scanOfficeCapabilityIntegrity,
  scanParity,
  scanPlaceholderParity,
  scanProviderResourcesHook,
  scanRepo,
  scanSemanticAnchorCatalog,
  scanSemanticAnchorParity,
  scanToolDescriptionAnchorCatalog,
  scanToolDescriptionAnchorParity,
} from '../../../scripts/checks/language-parity-gate.mjs'
import {
  OFFICE_CAPABILITY_ANCHORS,
  TOOL_DESCRIPTION_ANCHORS,
} from '../../../scripts/checks/semantic-anchors.mjs'

const GOOD_HOOK = `
module ProviderResources =
    let requireLanguagePair semanticPath =
        for lang in [ ProviderLanguage.English; ProviderLanguage.SimplifiedChinese ] do
            if not (exists lang semanticPath) then failwith "missing"
    let resourceFileName lang = "en.md"
`

const makeProviderFixture = () => {
  const dir = mkdtempSync(join(tmpdir(), 'lang-parity-'))
  const providerAbs = join(dir, PROVIDER_ROOT)
  return {
    dir,
    providerAbs,
    writePair: (semantic, en, zh) => {
      const base = join(providerAbs, semantic)
      mkdirSync(base, { recursive: true })
      writeFileSync(join(base, 'en.md'), en)
      writeFileSync(join(base, 'zh-CN.md'), zh)
    },
    dispose: () => rmSync(dir, { recursive: true, force: true }),
  }
}

test('gate_c_documents_locale_leaves', () => {
  assert.deepEqual(LOCALE_FILES, ['en.md', 'zh-CN.md'])
  assert.equal(PROVIDER_ROOT, 'resources/provider')
})

test('gate_c_parity_detects_missing_zh_cn', () => {
  const providerAbs = '/tmp/provider'
  const violations = scanParity(['role/manager'], providerAbs)
  assert.ok(violations.some((v) => v.code === 'missing-en' || v.code === 'missing-zh-cn'))
})

test('gate_c_parity_detects_missing_en', () => {
  const violations = scanParity(['role/manager'], resolve(process.cwd(), PROVIDER_ROOT))
  assert.equal(violations.length, 0)
})

test('gate_c_provider_resources_hook_required', () => {
  assert.equal(scanProviderResourcesHook(GOOD_HOOK).length, 0)
  assert.ok(scanProviderResourcesHook('module ProviderResources = let x = 1').some((v) => v.code === 'missing-require-language-pair'))
})

test('gate_c_repo_lists_role_semantic_dirs', () => {
  const root = resolve(process.cwd())
  const semanticDirs = listSemanticResourceDirs(resolve(root, PROVIDER_ROOT))
  assert.ok(semanticDirs.includes('role/manager'))
  assert.ok(semanticDirs.includes('role/coder'))
})

test('gate_c_repo_scan_is_green', () => {
  const result = scanRepo()
  assert.equal(result.ok, true, JSON.stringify(result.violations, null, 2))
})

test('ac20_extract_code_spans_skips_fenced_blocks', () => {
  const text = 'Use `exit_code`.\n\n```\ntranslated_should_ignore\n```\nAlso `deadline_seconds`.'
  assert.deepEqual([...extractCodeSpans(text)].sort(), ['deadline_seconds', 'exit_code'])
})

test('ac20_identifier_parity_equal_spans_pass', () => {
  const fx = makeProviderFixture()
  try {
    fx.writePair(
      'library/demo',
      'Choose `deadline_seconds = 120` and keep `exit_code`.',
      '选择 `deadline_seconds = 120`，并保留 `exit_code`。',
    )
    const violations = scanIdentifierParity(['library/demo'], fx.providerAbs)
    assert.deepEqual(violations, [])
  } finally {
    fx.dispose()
  }
})

test('ac20_identifier_parity_mismatch_reports_semantic_and_diff', () => {
  const fx = makeProviderFixture()
  try {
    fx.writePair(
      'role/demo',
      'Wire field `exit_code` must stay.',
      'Wire field `退出码` must stay.',
    )
    const violations = scanIdentifierParity(['role/demo'], fx.providerAbs)
    assert.equal(violations.length, 1)
    assert.equal(violations[0].code, 'identifier-parity')
    assert.equal(violations[0].path, 'resources/provider/role/demo')
    assert.match(violations[0].detail ?? '', /only-en: \[exit_code\]/)
    assert.match(violations[0].detail ?? '', /only-zh-CN: \[退出码\]/)
  } finally {
    fx.dispose()
  }
})

test('ac20_tip_and_tool_catalog_hits_must_match', () => {
  const fx = makeProviderFixture()
  try {
    fx.writePair(
      'role/tip-demo',
      'Avoid blind-edit and call open-terminal.',
      '避免 blind-edit，并调用 open-terminal。',
    )
    const catalogs = {
      tipIdentities: ['blind-edit'],
      toolNames: ['open-terminal'],
    }
    assert.deepEqual(scanIdentifierParity(['role/tip-demo'], fx.providerAbs, catalogs), [])

    fx.writePair(
      'role/tip-demo',
      'Avoid blind-edit and call open-terminal.',
      '避免 盲目编辑，并调用 打开终端。',
    )
    const red = scanIdentifierParity(['role/tip-demo'], fx.providerAbs, catalogs)
    assert.equal(red.length, 1)
    assert.equal(red[0].path, 'resources/provider/role/tip-demo')
    assert.match(red[0].detail ?? '', /only-en: \[blind-edit, open-terminal\]/)
    assert.match(red[0].detail ?? '', /only-zh-CN: \[\]/)
  } finally {
    fx.dispose()
  }
})

test('ac20_extract_protocol_identifiers_unions_sources', () => {
  const ids = extractProtocolIdentifiers('See `exit_code` then blind-edit via open-terminal.', {
    tipIdentities: ['blind-edit'],
    toolNames: ['open-terminal'],
  })
  assert.deepEqual([...ids].sort(), ['blind-edit', 'exit_code', 'open-terminal'])
})

test('gate_c_placeholder_parity_equal_sets_pass', () => {
  const fx = makeProviderFixture()
  try {
    fx.writePair('tool/demo', '{{byname}} has returned.', '{{byname}} 已经回来了。')
    assert.deepEqual(scanPlaceholderParity(['tool/demo'], fx.providerAbs), [])
  } finally {
    fx.dispose()
  }
})

test('gate_c_placeholder_parity_mismatch_reports_diff', () => {
  const fx = makeProviderFixture()
  try {
    fx.writePair('tool/demo', '{{byname}} carries {{charge}}.', '{{byname}} 承担托付。')
    const violations = scanPlaceholderParity(['tool/demo'], fx.providerAbs)
    assert.equal(violations.length, 1)
    assert.equal(violations[0].code, 'placeholder-parity')
    assert.equal(violations[0].path, 'resources/provider/tool/demo')
    assert.match(violations[0].detail ?? '', /only-en: \[charge\]/)
  } finally {
    fx.dispose()
  }
})

test('gate_c_extract_placeholders', () => {
  assert.deepEqual([...extractPlaceholders('{{byname}} / {{charge}} / {{byname}}')].sort(), [
    'byname',
    'charge',
  ])
  assert.deepEqual([...extractPlaceholders('no holes')].sort(), [])
})

test('gate_c_semantic_anchor_parity_detects_missing_zh_id', () => {
  const fx = makeProviderFixture()
  try {
    fx.writePair('role/demo', 'Waiting is justified by dependency.', '等待是一种习惯。')
    const catalog = {
      demo: [{ id: 'waiting-by-dependency', en: /justified by dependency/i, zh: /等待由依赖证明/ }],
    }
    const violations = scanSemanticAnchorParity(fx.providerAbs, catalog)
    assert.equal(violations.length, 1)
    assert.equal(violations[0].code, 'semantic-anchor')
    assert.match(violations[0].path, /zh-CN\.md$/)
    assert.match(violations[0].detail ?? '', /waiting-by-dependency/)
  } finally {
    fx.dispose()
  }
})

test('gate_c_semantic_anchor_catalog_requires_every_role_law', () => {
  const violations = scanSemanticAnchorCatalog(['role/manager', 'role/unknown-office'])
  assert.ok(violations.some((v) => v.code === 'semantic-anchor-catalog' && /unknown-office/.test(v.path)))
  assert.equal(
    violations.filter((v) => /role\/manager$/.test(v.path)).length,
    0,
  )
})

test('gate_c_tool_description_anchor_parity_detects_missing_zh_id', () => {
  const fx = makeProviderFixture()
  try {
    fx.writePair(
      'tool/fork/description',
      'Entrust bounded work to another office within this mission. Coder / Engineer Changes repository source.',
      '把工作交给另一个职位。',
    )
    const catalog = {
      fork: [{ id: 'coder-mutation', en: /Changes repository source/i, zh: /改变 repository source/ }],
    }
    const violations = scanToolDescriptionAnchorParity(fx.providerAbs, catalog)
    assert.equal(violations.length, 1)
    assert.equal(violations[0].code, 'tool-description-anchor')
    assert.match(violations[0].path, /zh-CN\.md$/)
    assert.match(violations[0].detail ?? '', /coder-mutation/)
  } finally {
    fx.dispose()
  }
})

test('gate_c_tool_description_anchor_catalog_requires_high_risk_verbs', () => {
  assert.deepEqual(
    Object.keys(TOOL_DESCRIPTION_ANCHORS).sort(),
    [
      'commission',
      'establish-behavior',
      'fork',
      'inspect',
      'query-shell',
      'repair-behavior',
      'run',
    ].sort(),
  )
  assert.ok(TOOL_DESCRIPTION_ANCHORS.inspect.some((a) => a.id === 'no-implement-or-repair'))
  const violations = scanToolDescriptionAnchorCatalog(['tool/inspect/description'])
  const missing = violations
    .filter((v) => v.code === 'tool-description-anchor-catalog')
    .map((v) => v.path)
    .sort()
  assert.ok(missing.some((p) => /tool\/fork\/description$/.test(p)))
  assert.ok(missing.some((p) => /tool\/commission\/description$/.test(p)))
  assert.ok(missing.some((p) => /tool\/establish-behavior\/description$/.test(p)))
  assert.ok(missing.some((p) => /tool\/repair-behavior\/description$/.test(p)))
  assert.ok(missing.some((p) => /tool\/query-shell\/description$/.test(p)))
  assert.ok(missing.some((p) => /tool\/run\/description$/.test(p)))
  assert.equal(
    violations.filter((v) => /tool\/inspect\/description$/.test(v.path)).length,
    0,
  )
})

const OFFICE_PASS = {
  managerEn: [
    'Entrust mutation to a Coder.',
    'Entrust an Inspector.',
    'Entrust DevOps.',
    'Entrust a Browser.',
    'Entrust Inquiry.',
    'These offices are not interchangeable. A Coder is not an Operator.',
  ].join('\n'),
  managerZh: [
    '交给 Coder 改书写世界。',
    'Inspector 建立已存在事实。',
    'DevOps 行动。',
    'Browser 取外部事实。',
    'Inquiry 推理未决问题。',
    '这些 Office 不可互换；Coder 不是碰巧没有 shell 的 Operator。',
  ].join('\n'),
  forkEn: [
    'Coder / Engineer Changes repository source.',
    'Scout / Investigator already exist in the repository.',
    'Technician / Operator running world.',
    'Navigator / Researcher external world with provenance.',
    'Analyst / Inquirer not yet clear.',
  ].join('\n'),
  forkZh: [
    'Coder / Engineer 改变 repository source。',
    'Scout / Investigator 已经存在的事实。',
    'Technician / Operator 运行中的世界。',
    'Navigator / Researcher 外部世界的事实。',
    'Analyst / Inquirer 尚无明确答案。',
  ].join('\n'),
}

const writeOfficeFixture = (fx, overlay = {}) => {
  const texts = { ...OFFICE_PASS, ...overlay }
  fx.writePair('role/manager', texts.managerEn, texts.managerZh)
  fx.writePair('tool/fork/description', texts.forkEn, texts.forkZh)
}

test('gate_f_catalog_names_five_forkable_offices', () => {
  assert.deepEqual(Object.keys(OFFICE_CAPABILITY_ANCHORS).sort(), [
    'browser',
    'coder',
    'devops',
    'inquiry',
    'inspector',
  ])
  assert.equal(OFFICE_CAPABILITY_ANCHORS.coder.id, 'coder-mutation')
  assert.equal(OFFICE_CAPABILITY_ANCHORS.inspector.id, 'inspector-existing-facts')
  assert.equal(OFFICE_CAPABILITY_ANCHORS.devops.id, 'devops-execution')
  assert.equal(OFFICE_CAPABILITY_ANCHORS.browser.id, 'browser-external-provenance')
  assert.equal(OFFICE_CAPABILITY_ANCHORS.inquiry.id, 'inquiry-reasoning')
})

test('gate_f_office_capability_fixture_is_green', () => {
  const fx = makeProviderFixture()
  try {
    writeOfficeFixture(fx)
    assert.deepEqual(scanOfficeCapabilityIntegrity(fx.providerAbs), [])
  } finally {
    fx.dispose()
  }
})

test('gate_f_missing_locale_leaf_is_red', () => {
  const fx = makeProviderFixture()
  try {
    const violations = scanOfficeCapabilityIntegrity(fx.providerAbs)
    assert.ok(violations.some((v) => v.code === 'office-capability' && /role\/manager\/en\.md$/.test(v.path)))
    assert.ok(violations.some((v) => v.code === 'office-capability' && /tool\/fork\/description\/en\.md$/.test(v.path)))
  } finally {
    fx.dispose()
  }
})

test('gate_f_missing_manager_coder_projection_is_red', () => {
  const fx = makeProviderFixture()
  try {
    writeOfficeFixture(fx, {
      managerEn: OFFICE_PASS.managerEn.replace('Entrust mutation to a Coder.', 'Entrust mutation.'),
    })
    const violations = scanOfficeCapabilityIntegrity(fx.providerAbs)
    assert.ok(
      violations.some(
        (v) =>
          v.code === 'office-capability' &&
          /role\/manager\/en\.md$/.test(v.path) &&
          /coder-mutation/.test(v.detail ?? ''),
      ),
    )
  } finally {
    fx.dispose()
  }
})

test('gate_f_manager_must_forbid_interchangeable_offices', () => {
  const fx = makeProviderFixture()
  try {
    writeOfficeFixture(fx, {
      managerEn: OFFICE_PASS.managerEn.replace(
        'These offices are not interchangeable. A Coder is not an Operator.',
        '',
      ),
    })
    const violations = scanOfficeCapabilityIntegrity(fx.providerAbs)
    assert.ok(
      violations.some(
        (v) =>
          v.code === 'office-capability' &&
          /role\/manager\/en\.md$/.test(v.path) &&
          /not-interchangeable/.test(v.detail ?? ''),
      ),
    )
  } finally {
    fx.dispose()
  }
})

test('gate_f_fork_must_not_commission_another_witness', () => {
  const fx = makeProviderFixture()
  try {
    writeOfficeFixture(fx, {
      forkEn: `${OFFICE_PASS.forkEn}\nCommission another witness.`,
    })
    const violations = scanOfficeCapabilityIntegrity(fx.providerAbs)
    assert.ok(
      violations.some(
        (v) =>
          v.code === 'office-capability' &&
          /tool\/fork\/description\/en\.md$/.test(v.path) &&
          /Commission another witness/.test(v.detail ?? ''),
      ),
    )
  } finally {
    fx.dispose()
  }
})
