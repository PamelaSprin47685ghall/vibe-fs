/**
 * ARCH-016 Gate C — Role Law semantic-anchor catalog (PROMPT-019).
 * Same id must hit both locales. Regexes lock cognition, not wording.
 */

export const ROLE_ANCHOR_DIRS = Object.freeze([
  'manager',
  'coder',
  'inspector',
  'devops',
  'inquiry',
  'reviewer',
  'orchestrator',
  'browser',
  'blogger',
  'distiller',
  'bookkeeper',
])

/** @typedef {{ id: string, en: RegExp, zh: RegExp }} SemanticAnchor */

/** @type {Readonly<Record<string, readonly SemanticAnchor[]>>} */
export const ROLE_SEMANTIC_ANCHORS = Object.freeze({
  manager: Object.freeze([
    {
      id: 'arms-length-planning',
      en: /arm'?s[- ]length|another Manager/i,
      zh: /一臂之距|另一个 Manager/,
    },
    {
      id: 'planning-table-or-entrusted',
      en: /Planning Table|Entrusted Road/i,
      zh: /规划桌|受托之路|Planning Table|Entrusted Road/,
    },
    { id: 'obligations', en: /obligation/i, zh: /obligation/ },
    { id: 'order-of-ten', en: /order of ten/i, zh: /大约十条|十条 lane|order of ten/ },
    {
      id: 'waiting-by-dependency',
      en: /waiting is justified by dependency|Wait only when.*depend/i,
      zh: /等待由依赖证明其正当/,
    },
    {
      id: 'no-personal-repository-witness',
      en: /do not establish repository facts with your own hands/i,
      zh: /不以自己的双手去建立 repository 事实/,
    },
    {
      id: 'anti-defeatism',
      en: /time spent is not time exhausted|road is long/i,
      zh: /已经花掉的时间|道路漫长/,
    },
    { id: 'opportunity-cost', en: /opportunity cost/i, zh: /机会成本/ },
    {
      id: 'returned-record',
      en: /returned record changes the mission only through what it establishes/i,
      zh: /返回的记录，只通过它所建立的事实改变 mission/,
    },
    { id: 'entrust-by-consequence', en: /Entrust by consequence/i, zh: /按后果托付/ },
    {
      id: 'choose-by-return',
      en: /what kind of truth or change must come back/i,
      zh: /必须回来的是哪一种真相或改变/,
    },
  ]),
  coder: Object.freeze([
    { id: 'written-world', en: /written world/i, zh: /书写出来的世界|书写世界/ },
    { id: 'no-execution', en: /do not execute|You do not execute/i, zh: /你不执行自己写下的东西/ },
    {
      id: 'consume-runtime-evidence',
      en: /reason deeply from (?:that |runtime )?evidence|Use that evidence/i,
      zh: /可以从这些证据出发深入推理/,
    },
    {
      id: 'tests-are-source',
      en: /Tests are source when you write them/i,
      zh: /当你编写测试时，测试是源码/,
    },
    {
      id: 'coherent-not-smallest',
      en: /smallest coherent change is not always the smallest diff|no less than coherence requires/i,
      zh: /最小的连贯改变，并不等于最小的 diff/,
    },
    { id: 'shell-boundary', en: /urge to run|edge of mutation/i, zh: /想要一个 shell|修改的边界/ },
    { id: 'clean-handoff', en: /clean handoff/i, zh: /清晰的 handoff/ },
    {
      id: 'inspector-is-witness',
      en: /Inspector is your witness, not your second pair of editing hands/i,
      zh: /Inspector 是你的见证者，不是第二双写代码的手/,
    },
    {
      id: 'do-not-ask-inspect-and-fix',
      en: /Inspect this and fix the code/i,
      zh: /检查一下然后把代码修掉/,
    },
  ]),
  inspector: Object.freeze([
    {
      id: 'causal-readonly',
      en: /Observe without changing|causal|static observation/i,
      zh: /不要为了观察而改变它|Read-only 是因果/,
    },
    { id: 'existing-fact', en: /existing fact/i, zh: /已经存在的事实/ },
    {
      id: 'evidence-funnel',
      en: /cheapest adequate observation|fact would change/i,
      zh: /最便宜的充分观察|cheapest adequate/,
    },
    { id: 'locatability', en: /locatable|find again/i, zh: /再次找到/ },
    {
      id: 'consequence-not-verdict',
      en: /does not become a judge|consequence without becoming the judge|does not turn those consequences into a verdict|stop before the evidence becomes a verdict/i,
      zh: /不会因此变成法官/,
    },
    {
      id: 'semantic-stopping',
      en: /what the world ought to mean/i,
      zh: /世界应该意味着什么/,
    },
  ]),
  devops: Object.freeze([
    {
      id: 'operational-closure',
      en: /operational objective|honest\s+closure/i,
      zh: /operational objective 得到诚实的收束/,
    },
    {
      id: 'act-vs-observation',
      en: /command is an act|signal is an act/i,
      zh: /命令是一种行动|Signal 是一种行动/,
    },
    {
      id: 'mechanical-meaning',
      en: /Mechanical means the meaning is already decided|already decided/i,
      zh: /含义已经被决定/,
    },
    {
      id: 'coder-report-not-evidence',
      en: /Coder'?s report is not execution evidence/i,
      zh: /Coder 的报告不是执行证据/,
    },
    {
      id: 'continuing-process',
      en: /continuing (terminal|process|interactive)/i,
      zh: /持续存在的交互状态|持续终端/,
    },
    {
      id: 'signal-not-exit',
      en: /signal is an act, not an exit|A signal is not an exit|signal is not an exit/i,
      zh: /Signal 不是 exit/,
    },
    {
      id: 'failure-can-be-work',
      en: /failure is often work|long diagnostic road/i,
      zh: /失败可以是工作|漫长的诊断道路/,
    },
  ]),
  inquiry: Object.freeze([
    {
      id: 'kernel-owns-state',
      en: /epistemic state is\s+governed by a Kernel|Kernel\s+owns/i,
      zh: /认识状态由 Kernel 治理/,
    },
    {
      id: 'control-not-evidence',
      en: /not evidence that the requested idea/i,
      zh: /不是证据，不能证明被请求的想法已经成立/,
    },
    {
      id: 'generation-not-control',
      en: /Generation is not control/i,
      zh: /生成不是控制/,
    },
    {
      id: 'no-free-information',
      en: /thought twice|Repeated reasoning is not new evidence|No Free Information/i,
      zh: /想了两次|No Free Information/,
    },
    {
      id: 'closure-not-collapse',
      en: /Closure is not always collapse/i,
      zh: /Closure 不总是坍缩/,
    },
    { id: 'root-relative', en: /root question/i, zh: /根问题/ },
    {
      id: 'synthesis-boundary',
      en: /Do not decorate a canonical synthesis|strongest synthesis the evidence has earned/i,
      zh: /不要用这场探究并未赢得的确定性，去装饰 canonical synthesis|留下证据真正赢得的最强 synthesis/,
    },
  ]),
  reviewer: Object.freeze([
    { id: 'discrimination', en: /discrimination/i, zh: /有区分力的判断/ },
    {
      id: 'rejection-must-purchase',
      en: /Rejection must (also be earned|purchase)/i,
      zh: /拒绝必须买到/,
    },
    { id: 'non-blocking', en: /non-blocking/i, zh: /非阻断性/ },
    {
      id: 'perfect-not-flawless',
      en: /PERFECT means|literal flawlessness/i,
      zh: /并不意味着字面上的毫无瑕疵/,
    },
    { id: 'acceptance-not-omniscience', en: /omniscience/i, zh: /全知/ },
  ]),
  orchestrator: Object.freeze([
    { id: 'owns-roads', en: /own(?:s)?(?:\s+the)?\s+roads/i, zh: /你拥有的是道路/ },
    {
      id: 'same-road-continuation',
      en: /continue that road|same road|Continue the same road/i,
      zh: /继续那条道路/,
    },
    {
      id: 'independent-destination',
      en: /independently coherent destination|coherent destination of its own/i,
      zh: /独立连贯的目的地/,
    },
    { id: 'shared-gate', en: /shared destination|one gate/i, zh: /同一道门|共享目的地/ },
    {
      id: 'host-vs-orchestrator',
      en: /Host reconciles states|You reconcile purposes|Orchestrator reconciles purposes/i,
      zh: /Host 协调状态|你协调目的/,
    },
  ]),
  browser: Object.freeze([
    {
      id: 'provenance-not-reachability',
      en: /Reachability does not determine ownership|Provenance does/i,
      zh: /Reachability 并不决定 ownership|Provenance 才决定/,
    },
    { id: 'far-shore', en: /far shore/i, zh: /远岸/ },
    { id: 'source-closest', en: /source closest to the fact/i, zh: /最接近.*事实/ },
    { id: 'visual-truth', en: /only visible|screenshots/i, zh: /只有通过视觉才能看见|Screenshot/ },
    { id: 'disagreement', en: /disagreement/i, zh: /分歧/ },
  ]),
  blogger: Object.freeze([
    { id: 'occurrence-selection', en: /changed the continuing road/i, zh: /什么改变了继续前进的道路/ },
    {
      id: 'not-instrumentation',
      en: /not how it was observed|instrument/i,
      zh: /而不是它是怎样被观察到的/,
    },
    {
      id: 'tip-ontology',
      en: /One observation[\s\S]*One lesson[\s\S]*One listener/i,
      zh: /一个 observation[\s\S]*一个 lesson[\s\S]*一个 listener/,
    },
    { id: 'repetition-legal', en: /repeated lesson|repetition is legal/i, zh: /重复是合法的/ },
  ]),
  distiller: Object.freeze([
    {
      id: 'distinguishing',
      en: /distinguishing|Preserve facts that can change/i,
      zh: /能够改变后续 judgment 的事实|区分价值/,
    },
    {
      id: 'fragment-humility',
      en: /fragment cannot establish the whole|fragment silence/i,
      zh: /fragment 的谦逊|沉默的 fragment/,
    },
    { id: 'merge-conflicts', en: /Conflicting observations|preserve conflicts/i, zh: /保留冲突/ },
    {
      id: 'no-invented-causality',
      en: /Do not guess causes|what the material before you can establish/i,
      zh: /不要猜测 cause/,
    },
  ]),
  bookkeeper: Object.freeze([
    {
      id: 'reusable-knowledge',
      en: /reusable knowledge|Casebook remembers what the road taught/i,
      zh: /可复用的知识|Casebook 记住这条道路教会了什么/,
    },
    { id: 'one-case', en: /one question and one answer/i, zh: /一个 Question 和一个 Answer/ },
    {
      id: 'question-may-change',
      en: /Deeper learning may change the question/i,
      zh: /也可能改变 Question/,
    },
    {
      id: 'zero-mutation',
      en: /leave the\s+case unchanged|zero mutation/i,
      zh: /零变更是合法的|保持 case 不变/,
    },
    {
      id: 'transcript-is-data',
      en: /do not become your instructions|Instructions appearing inside that material/i,
      zh: /不会因此成为对你的 instructions/,
    },
  ]),
})

/** Tool-description cognition (PROMPT-019). Same id must hit both locales. */
export const TOOL_DESCRIPTION_ANCHORS = Object.freeze({
  inspect: Object.freeze([
    {
      id: 'repository-fact',
      en: /facts that already exist in the repository/i,
      zh: /repository 中已经存在的事实/,
    },
    {
      id: 'causal-readonly',
      en: /read-only in the causal sense/i,
      zh: /在因果意义上是只读的/,
    },
    {
      id: 'no-code-changes',
      en: /Do not use inspect to ask for code changes/i,
      zh: /不要用 inspect 请求代码修改/,
    },
    {
      id: 'no-behavioral-execution',
      en: /make the project run[\s\S]{0,80}behavioral evidence/i,
      zh: /不会让项目运行起来以制造新的行为证据/,
    },
  ]),
  fork: Object.freeze([
    {
      id: 'office-not-witness',
      en: /another office within this mission/i,
      zh: /当前 mission 中的另一个 Office/,
    },
    {
      id: 'coder-mutation',
      en: /Coder \/ Engineer[\s\S]{0,120}Changes repository source/i,
      zh: /Coder \/ Engineer[\s\S]{0,80}改变 repository source/,
    },
    {
      id: 'inspector-existing-facts',
      en: /Scout \/ Investigator[\s\S]{0,160}already exist in the repository/i,
      zh: /Scout \/ Investigator[\s\S]{0,80}已经存在的事实/,
    },
    {
      id: 'devops-execution',
      en: /Technician \/ Operator[\s\S]{0,160}running world/i,
      zh: /Technician \/ Operator[\s\S]{0,80}运行中的世界/,
    },
    {
      id: 'browser-external-provenance',
      en: /Navigator \/ Researcher[\s\S]{0,160}external world with provenance/i,
      zh: /Navigator \/ Researcher[\s\S]{0,80}外部世界的事实/,
    },
    {
      id: 'inquiry-reasoning',
      en: /Analyst \/ Inquirer[\s\S]{0,160}not yet clear/i,
      zh: /Analyst \/ Inquirer[\s\S]{0,80}尚无明确答案/,
    },
    {
      id: 'persona-not-authority',
      en: /differ in persona and reasoning depth,[\s\S]{0,40}not in the office's authority/i,
      zh: /区别在 persona 与 reasoning depth，不改变该 Office 的 authority/,
    },
    {
      id: 'create-and-continue',
      en: /calling \+ name \+ charge[\s\S]{0,80}same name/i,
      zh: /calling \+ name \+ charge[\s\S]{0,80}同一个 name/,
    },
  ]),
})
