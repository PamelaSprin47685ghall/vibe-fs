// Split from tests/unit/verify/language-parity-gate.test.mjs (cutover Wave 2a); owner: office-capability
//
// ARCH-016 Gate F — office capability integrity（OFF-005..007）：五个可 fork office 的
// canonical catalog、manager law / fork description 双语文档命中、不可互换约束。
// 结构 parity 机制面（gate_c_* / ac20_*）在 provider-language 包测试内。
import assert from 'node:assert/strict'
import { mkdirSync, mkdtempSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'
import {
  PROVIDER_ROOT,
  scanOfficeCapabilityIntegrity,
} from '../../../scripts/checks/language-parity-gate.mjs'
import { OFFICE_CAPABILITY_ANCHORS } from '../../../scripts/checks/semantic-anchors.mjs'

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
