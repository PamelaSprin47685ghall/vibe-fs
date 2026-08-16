// delegation structure contract tests (cutover Wave 2a additions).
//
// 为 5 条 PROOF 落点是「REUSE 脚本/结构本身」的命题补最小 executable contract
// test（requirement-trace 机器契约要求每条未 deleted 命题至少一个 active test）：
//
// - DELEG-001: semantic anchor `entrust-by-consequence` / `choose-by-return` /
//   `no-omnipotent-charge`（manager Role Law 双语文档命中）。
// - DELEG-002: anchor `persona-not-authority`（fork description 双语文档命中）。
// - DELEG-004: anchor `office-not-witness`（fork description 双语文档命中）+
//   fork 与 commission 是不同角色门 ⇒ 不同 contract。
// - DELEG-007: 允许的同步委托边（Inquiry/Coder/DevOps → Inspector via Inspect；
//   DevOps → Coder via Behavior）存在且反向边禁止，边集构成 DAG（无环）。
// - DELEG-020: 工具名是当前 HOW 选择，语义合同不绑定名字（HOW.md「边界与弃权」）。

import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'

import {
  ROLE_SEMANTIC_ANCHORS,
  TOOL_DESCRIPTION_ANCHORS,
} from '../../../scripts/checks/semantic-anchors.mjs'
import { roles } from '../../verification-system/tests/support/domain.mjs'

const PROVIDER_ROOT = join(fileURLToPath(new URL('../../..', import.meta.url)), 'resources', 'provider')

const readProviderPair = (relative) => {
  const base = join(PROVIDER_ROOT, relative)
  return {
    en: readFileSync(join(base, 'en.md'), 'utf8'),
    zh: readFileSync(join(base, 'zh-CN.md'), 'utf8'),
  }
}

const assertAnchorHit = (pair, anchor, id) => {
  assert.match(pair.en, anchor.en, `${id}: en must hit the semantic anchor`)
  assert.match(pair.zh, anchor.zh, `${id}: zh must hit the semantic anchor`)
}

const anchorById = (catalog, id) => {
  const found = catalog.find((entry) => entry.id === id)
  assert.ok(found, `anchor ${id} must exist in the semantic-anchor catalog`)
  return found
}

test('WHAT[DELEG-001] manager_role_law_entrusts_by_consequence_not_persona', () => {
  // 001 规范：委托必须同时明确 charge / office / logical owner / bounded 返回后果；
  // 识别依据是后果（entrust by consequence），不是 persona 名。
  const manager = ROLE_SEMANTIC_ANCHORS.manager
  const pair = readProviderPair('role/manager')
  assertAnchorHit(pair, anchorById(manager, 'entrust-by-consequence'), 'entrust-by-consequence')
  assertAnchorHit(pair, anchorById(manager, 'choose-by-return'), 'choose-by-return')
  assertAnchorHit(pair, anchorById(manager, 'no-omnipotent-charge'), 'no-omnipotent-charge')
})

test('WHAT[DELEG-002] calling_names_differ_in_persona_depth_not_authority', () => {
  // 002 规范：同一 Office 的 calling 名只差 persona 与 reasoning depth，
  // 不改变该 Office 的 authority。
  const fork = TOOL_DESCRIPTION_ANCHORS.fork
  const pair = readProviderPair('tool/fork/description')
  assertAnchorHit(pair, anchorById(fork, 'persona-not-authority'), 'persona-not-authority')
})

test('WHAT[DELEG-004] commission_and_fork_are_distinct_contracts_not_witness', () => {
  // 004 规范：commission（独立集成之路）≠ fork（mission 内 witness），不同 contract 不同名。
  const fork = TOOL_DESCRIPTION_ANCHORS.fork
  const pair = readProviderPair('tool/fork/description')
  assertAnchorHit(pair, anchorById(fork, 'office-not-witness'), 'office-not-witness')

  // 同一工具名在任何地方命名同一个 contract：fork 与 commission 的角色门不同
  // ⇒ 二者是不同 contract，不允许同名混用。
  assert.notEqual(roles.toolAllows('fork', 'Manager', 'ses_x'), roles.toolAllows('commission', 'Manager', 'ses_x'))
  assert.equal(roles.toolAllows('fork', 'Manager', 'ses_x'), true, 'fork belongs to Manager')
  assert.equal(roles.toolAllows('commission', 'Orchestrator', 'ses_x'), true, 'commission belongs to Orchestrator')
})

test('WHAT[DELEG-007] sync_delegate_edges_are_the_allowed_dag_only', () => {
  // 007 规范：允许的同步委托边 Inquiry/Coder/DevOps → Inspector、
  // DevOps → Coder；禁止反向/成环边；图必须是 DAG。
  const canInspect = (role) => roles.allows(role, 'Inspect')
  const canDelegateBehavior = (role) => roles.allows(role, 'Behavior')

  // 正向边全部存在。
  assert.equal(canInspect('Inquiry'), true, 'Inquiry → Inspector allowed')
  assert.equal(canInspect('Coder'), true, 'Coder → Inspector allowed')
  assert.equal(canInspect('DevOps'), true, 'DevOps → Inspector allowed')
  assert.equal(canDelegateBehavior('DevOps'), true, 'DevOps → Coder allowed')

  // 反向/成环边全部被拒：Inspector/Coder 无 Behavior（→ Coder 委托禁止），
  // Inspector 无 Inspect（→ Inspector 自环禁止），无向 Inquiry 的 sync 委托面。
  for (const role of ['Inspector', 'Coder', 'Manager', 'Orchestrator', 'Browser', 'Reviewer', 'Distiller', 'Blogger']) {
    assert.equal(canDelegateBehavior(role), false, `no role other than DevOps may delegate to Coder`)
  }
  for (const role of ['Inspector', 'Manager', 'Orchestrator', 'Browser', 'Reviewer', 'Distiller', 'Blogger']) {
    assert.equal(canInspect(role), false, `only Inquiry/Coder/DevOps may delegate to Inspector`)
  }

  // 边集 {Inquiry,Coder,DevOps} → Inspector、{DevOps} → Coder 必须是无环 DAG
  // （标准 DFS 环检测：任何节点不得沿边回到自身）。
  const adjacency = new Map([
    ['Inquiry', ['Inspector']],
    ['Coder', ['Inspector']],
    ['DevOps', ['Inspector', 'Coder']],
    ['Inspector', []],
  ])
  const visiting = new Set()
  const visited = new Set()
  const visit = (node) => {
    if (visiting.has(node)) throw new Error(`cycle detected through ${node}`)
    if (visited.has(node)) return
    visiting.add(node)
    for (const next of adjacency.get(node) ?? []) visit(next)
    visiting.delete(node)
    visited.add(node)
  }
  for (const node of adjacency.keys()) visit(node)
})

test('WHAT[DELEG-020] delegation_semantics_do_not_depend_on_current_tool_names', () => {
  // 020 规范：fork/commission/inspect/establish-behavior/repair-behavior 是当前
  // HOW 选择的动词名；WHAT 绑定的是语义合同（DELEG-001..019），改名不改变命题。
  const how = readFileSync(new URL('../HOW.md', import.meta.url), 'utf8')
  assert.match(how, /工具名/)
  assert.match(how, /DELEG-020/)
  assert.match(how, /改名不动 WHAT/)
  // 当前选择的名字必须真实存在（引用完整性由 tool-referential-integrity 交叉）。
  for (const name of ['fork', 'commission', 'inspect', 'establish-behavior', 'repair-behavior']) {
    assert.ok(how.includes(name), `HOW must record the current tool name ${name}`)
  }
})
