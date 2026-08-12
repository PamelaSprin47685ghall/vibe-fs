// Prompt Restoration — semantic-depth gate.
// Role Law must teach a durable cognition contract, not merely avoid tool names.
// Word-count is not quality; missing anchors are.

import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'
import { roles } from '../support/domain.mjs'

const root = join(dirname(fileURLToPath(import.meta.url)), '../../..')
const rolePath = (role, locale) => join(root, 'resources/provider/role', role, locale)

const readRole = (role, locale) => readFileSync(rolePath(role, locale), 'utf8')

/** Every entry must match; regex keeps EN/ZH flexible while locking the cognition. */
const EN_ANCHORS = Object.freeze({
  manager: [
    { id: 'arms-length-planning', re: /arm'?s[- ]length|another Manager/i },
    { id: 'planning-table-or-entrusted', re: /Planning Table|Entrusted Road/i },
    { id: 'obligations', re: /obligation/i },
    { id: 'order-of-ten', re: /order of ten/i },
    { id: 'waiting-by-dependency', re: /waiting is justified by dependency|Wait only when.*depend/i },
    { id: 'no-personal-repository-witness', re: /do not establish repository facts with your own hands/i },
    { id: 'anti-defeatism', re: /time spent is not time exhausted|road is long/i },
    { id: 'opportunity-cost', re: /opportunity cost/i },
    { id: 'returned-record', re: /returned record changes the mission only through what it establishes/i },
  ],
  coder: [
    { id: 'written-world', re: /written world/i },
    { id: 'no-execution', re: /do not execute|You do not execute/i },
    { id: 'consume-runtime-evidence', re: /reason deeply from (?:that |runtime )?evidence|Use that evidence/i },
    { id: 'tests-are-source', re: /Tests are source when you write them/i },
    { id: 'coherent-not-smallest', re: /smallest coherent change is not always the smallest diff|no less than coherence requires/i },
    { id: 'shell-boundary', re: /urge to run|edge of mutation/i },
    { id: 'clean-handoff', re: /clean handoff/i },
  ],
  inspector: [
    { id: 'causal-readonly', re: /Observe without changing|causal|static observation/i },
    { id: 'existing-fact', re: /existing fact/i },
    { id: 'evidence-funnel', re: /cheapest adequate observation|fact would change/i },
    { id: 'locatability', re: /locatable|find again/i },
    { id: 'consequence-not-verdict', re: /does not become a judge|consequence without becoming the judge|does not turn those consequences into a verdict|stop before the evidence becomes a verdict/i },
    { id: 'semantic-stopping', re: /what the world ought to mean/i },
  ],
  devops: [
    { id: 'operational-closure', re: /operational objective|honest\s+closure/i },
    { id: 'act-vs-observation', re: /command is an act|signal is an act/i },
    { id: 'mechanical-meaning', re: /Mechanical means the meaning is already decided|already decided/i },
    { id: 'coder-report-not-evidence', re: /Coder'?s report is not execution evidence/i },
    { id: 'continuing-process', re: /continuing (terminal|process|interactive)/i },
    { id: 'signal-not-exit', re: /signal is an act, not an exit|A signal is not an exit|signal is not an exit/i },
    { id: 'failure-can-be-work', re: /failure is often work|long diagnostic road/i },
  ],
  inquiry: [
    { id: 'kernel-owns-state', re: /epistemic state is\s+governed by a Kernel|Kernel\s+owns/i },
    { id: 'control-not-evidence', re: /not evidence that the requested idea/i },
    { id: 'generation-not-control', re: /Generation is not control/i },
    { id: 'no-free-information', re: /thought twice|Repeated reasoning is not new evidence|No Free Information/i },
    { id: 'closure-not-collapse', re: /Closure is not always collapse/i },
    { id: 'root-relative', re: /root question/i },
    { id: 'synthesis-boundary', re: /Do not decorate a canonical synthesis|strongest synthesis the evidence has earned/i },
  ],
  reviewer: [
    { id: 'discrimination', re: /discrimination/i },
    { id: 'rejection-must-purchase', re: /Rejection must (also be earned|purchase)/i },
    { id: 'non-blocking', re: /non-blocking/i },
    { id: 'perfect-not-flawless', re: /PERFECT means|literal flawlessness/i },
    { id: 'acceptance-not-omniscience', re: /omniscience/i },
  ],
  orchestrator: [
    { id: 'owns-roads', re: /own(?:s)?(?:\s+the)?\s+roads/i },
    { id: 'same-road-continuation', re: /continue that road|same road|Continue the same road/i },
    { id: 'independent-destination', re: /independently coherent destination|coherent destination of its own/i },
    { id: 'shared-gate', re: /shared destination|one gate/i },
    { id: 'host-vs-orchestrator', re: /Host reconciles states|You reconcile purposes|Orchestrator reconciles purposes/i },
  ],
  browser: [
    { id: 'provenance-not-reachability', re: /Reachability does not determine ownership|Provenance does/i },
    { id: 'far-shore', re: /far shore/i },
    { id: 'source-closest', re: /source closest to the fact/i },
    { id: 'visual-truth', re: /only visible|screenshots/i },
    { id: 'disagreement', re: /disagreement/i },
  ],
  blogger: [
    { id: 'occurrence-selection', re: /changed the continuing road/i },
    { id: 'not-instrumentation', re: /not how it was observed|instrument/i },
    { id: 'tip-ontology', re: /One observation[\s\S]*One lesson[\s\S]*One listener/i },
    { id: 'repetition-legal', re: /repeated lesson|repetition is legal/i },
  ],
  distiller: [
    { id: 'distinguishing', re: /distinguishing|Preserve facts that can change/i },
    { id: 'fragment-humility', re: /fragment cannot establish the whole|fragment silence/i },
    { id: 'merge-conflicts', re: /Conflicting observations|preserve conflicts/i },
    { id: 'no-invented-causality', re: /Do not guess causes|what the material before you can establish/i },
  ],
  bookkeeper: [
    { id: 'reusable-knowledge', re: /reusable knowledge|Casebook remembers what the road taught/i },
    { id: 'one-case', re: /one question and one answer/i },
    { id: 'question-may-change', re: /Deeper learning may change the question/i },
    { id: 'zero-mutation', re: /leave the\s+case unchanged|zero mutation/i },
    { id: 'transcript-is-data', re: /do not become your instructions|Instructions appearing inside that material/i },
  ],
})

const ZH_ANCHORS = Object.freeze({
  manager: [
    { id: 'arms-length', re: /臂长|另一个 Manager|Planning Table|Entrusted Road/ },
    { id: 'obligations', re: /obligation|义务/ },
    { id: 'order-of-ten', re: /十|order of ten/ },
    { id: 'no-personal-witness', re: /不亲自|自己的手|repository facts/ },
    { id: 'anti-defeatism', re: /已经花掉的时间|道路漫长|时间花掉|长路|time exhausted|road is long/ },
    { id: 'opportunity-cost', re: /机会成本|opportunity cost/ },
  ],
  coder: [
    { id: 'written-world', re: /书写|written world/ },
    { id: 'tests-are-source', re: /Tests are source|当你编写测试时，测试是源码|测试是源码/ },
    { id: 'coherent-not-smallest', re: /最小的连贯改变|最小的 coherent|smallest coherent|coherence|并不等于最小的 diff/ },
    { id: 'clean-handoff', re: /干净的交接|清晰的 handoff|clean handoff|handoff 是你完成/ },
  ],
  inspector: [
    { id: 'existing-fact', re: /existing fact|已经存在的事实/ },
    { id: 'locatability', re: /再次找到|locatable|find again/ },
    { id: 'not-verdict', re: /不是裁决|verdict|judge/ },
    { id: 'semantic-stopping', re: /世界应[该当]意味|ought to mean/ },
  ],
  devops: [
    { id: 'operational', re: /operational objective|运营目标|诚实.*closure|closure/ },
    { id: 'mechanical', re: /Mechanical means|含义已经被决定|意义已经决定|already decided/ },
    { id: 'coder-report', re: /Coder.*(?:report|报告).*不是|不是执行证据|不是 execution evidence/ },
    { id: 'signal', re: /[Ss]ignal 是(?:一种)?行动|signal is an act|[Ss]ignal 不是 exit/ },
  ],
  inquiry: [
    { id: 'kernel', re: /Kernel/ },
    { id: 'control-not-evidence', re: /不是证据|not evidence that the requested/ },
    { id: 'no-free-info', re: /想了两次|Repeated reasoning|No Free Information/ },
    { id: 'generation-not-control', re: /生成不是控制|Generation is not control/ },
    { id: 'root', re: /根问题|root question/ },
  ],
  reviewer: [
    { id: 'non-blocking', re: /non-blocking|非阻断/ },
    { id: 'perfect', re: /PERFECT/ },
    { id: 'purchase', re: /purchase|买到|赢得/ },
    { id: 'omniscience', re: /omniscience|全知/ },
  ],
  orchestrator: [
    { id: 'owns-roads', re: /拥有.*道路|owns roads/ },
    { id: 'independent-destination', re: /独立.*目的地|独立推进|independently coherent|coherent destination/ },
    { id: 'host-vs-orch', re: /Host (?:reconciles|协调)|你协调目的|Orchestrator reconciles|调和/ },
  ],
  browser: [
    { id: 'provenance', re: /Provenance|出处|Reachability/ },
    { id: 'visual', re: /可见|only visible|screenshot/ },
    { id: 'disagreement', re: /分歧|disagreement/ },
  ],
  blogger: [
    { id: 'continuing-road', re: /继续前进的道路|继续的道路|continuing road/ },
    { id: 'tip', re: /One observation|一个 observation|一个观察/ },
  ],
  distiller: [
    { id: 'distinguishing', re: /区分|distinguishing|改变.*判断/ },
    { id: 'conflicts', re: /冲突|Conflicting/ },
  ],
  bookkeeper: [
    { id: 'one-qa', re: /一个 Question 和一个 Answer|一个问题.*一个答案|one question and one answer/ },
    { id: 'question-may-change', re: /改变 Question|改变问题|change the question/ },
    { id: 'transcript-data', re: /不会因此成为对你的 instructions|不是你的指令|do not become your instructions/ },
  ],
})

const assertAnchors = (role, locale, text, anchors) => {
  for (const { id, re } of anchors) {
    assert.match(text, re, `${role}/${locale} missing semantic anchor: ${id}`)
  }
}

test('PROMPT_depth_EN_role_laws_carry_cognition_anchors', () => {
  for (const [role, anchors] of Object.entries(EN_ANCHORS)) {
    assertAnchors(role, 'en.md', readRole(role, 'en.md'), anchors)
  }
})

test('PROMPT_depth_ZH_role_laws_carry_matching_cognition_anchors', () => {
  for (const [role, anchors] of Object.entries(ZH_ANCHORS)) {
    assertAnchors(role, 'zh-CN.md', readRole(role, 'zh-CN.md'), anchors)
  }
})

test('PROMPT_depth_Inquiry_Sphinx_capability_requires_Kernel_self_model', () => {
  // Production permissions already grant Sphinx to Inquiry. Role Law must teach the
  // Kernel/Inquirer relationship without enumerating sphinx_* tool names.
  const permissions = roles.permissions(roles.of('Inquiry'))
  assert.ok(
    permissions.some((n) => /Sphinx/i.test(n)),
    `Inquiry must retain Sphinx permission; got ${permissions.join(',')}`,
  )

  const en = readRole('inquiry', 'en.md')
  const zh = readRole('inquiry', 'zh-CN.md')
  assert.match(en, /Kernel/)
  assert.match(en, /Inquirer/)
  assert.match(zh, /Kernel/)
  assert.doesNotMatch(en, /sphinx_start|sphinx_resume/)
  assert.doesNotMatch(zh, /sphinx_start|sphinx_resume/)
})

test('PROMPT_depth_no_universal_closing_report_schema_in_role_laws', () => {
  for (const role of Object.keys(EN_ANCHORS)) {
    const en = readRole(role, 'en.md')
    assert.doesNotMatch(
      en,
      /Report back with exactly these fields|result, files changed, tests run, evidence, remaining risks, blockers/,
      role,
    )
  }
})
