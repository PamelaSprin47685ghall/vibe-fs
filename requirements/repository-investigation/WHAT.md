# WHAT — repository-investigation 的唯一 normative 合同

> 命题 = 当前世界必须同时成立的事实。每条命题有测试落点（见 `PROOF.md`）。
> 边界（DOES NOT OWN）写在各条「边界」；更完整的弃权记录在 `HOW.md` §历史与弃权。

## REPOSITORY-INVESTIGATION-001 — repository claim 必须由真实观察建立

**规范陈述**：对「当前本地 repository 已存在什么」的 claim，其证据必须来自对本地已存在世界的真实 observation。reasoning、semantic search hint、旧 Case（未经重放）、外部 web 都不能自动升级为当前 repository evidence。搜索结果的**低信任定位**是合同：hints 不是 instructions、不是 proof、不是合成的工具历史。

**含义/动机**：把「以为的事实」当「观察到的事实」会让下游判断建立在虚假证据上。真实观察 = 可重放、可定位、可追溯的 typed observation（`FileRead`/`GlobResult`/`GrepResult`），而不是推理产物。

**边界**：旧 Case 的复用规则（重放 → freshness hint）→ `knowledge-reuse`；外部 web facts → `external-investigation`；「当前事实如何被真实观察」才是本命题。

**证据**：→ `PROOF.md` REPOSITORY-INVESTIGATION-001 行。

## REPOSITORY-INVESTIGATION-002 — observation 可定位、可追溯

**规范陈述**：建立的 repository observation 必须可定位（locatable）且带 provenance：路径、行区间、内容（或内容 hash）足以让同一事实再次被找到/再次被验证。provider 侧措辞与 Casebook index 都只暴露足以定位的事实，不泄漏机器内部。

**含义/动机**：locatability 让 claim 可被复核（「这个证据在哪一行」）；无 provenance 的「结论」无法与 repository 对照。`CasebookCapture` 的 `contentHash` 与 Semble Hit 的 `FilePath + StartLine + EndLine + Content` 都是同一原则的实例。

**边界**：index 的可见面（shelfmark + canonical Q）→ `knowledge-reuse`；「观察本身必须可定位」才是本命题。

**证据**：→ `PROOF.md` REPOSITORY-INVESTIGATION-002 行。

## REPOSITORY-INVESTIGATION-003 — evidence acquisition 与 semantic reasoning 分层

**规范陈述**：evidence acquisition（Inspector/观察工具）与 semantic reasoning（Inquiry/Sphinx 认识状态求解）是两层：reasoning 决定**问什么**（fact → cheapest adequate observation），但不能凭思考**增加 repository evidence**。一连串机械搜索不是方法——reasoning 必须命名要建立的事实，再买观察。

**含义/动机**：让思考层拥有取证权 = 让「我觉得存在」冒充「我观察到」；让搜索层拥有推理权 = 机械扫库代替判断。分层后每层只做自己那件事。

**边界**：Inquiry 的工具面（{inspect, sphinx MCP}）与 Inspector 的权限集由 `capability-enforcement`/`office-capability` 裁决；本命题是「思考不产生证据」的语义边界。

**证据**：→ `PROOF.md` REPOSITORY-INVESTIGATION-003 行。

## REPOSITORY-INVESTIGATION-004 — cheapest adequate observation，足够即停止

**规范陈述**：investigation 必须选择**最便宜的充分观察**（cheapest adequate observation）来建立或反驳已命名的事实，并在该观察足以回答当前事实问题时**停止**。不为「看起来彻底」购买所有可想象的观察；独立性由既有证据证明，不是靠一开始就想象完整搜索树。

**含义/动机**：调查成本是真实资源；grep → grep → dump 的机械轨迹不产生判断。停止规则让调查有界：第一个便宜观察能结束调查就结束。

**边界**：具体搜索/读取策略（怎么选工具）是 Inspector 的 craft（HOW）；「最便宜充分 + 足够即停」才是合同。

**证据**：→ `PROOF.md` REPOSITORY-INVESTIGATION-004 行。

## REPOSITORY-INVESTIGATION-005 — observation 因果只读

**规范陈述**：observation 在**因果意义上**只读：不得为了观察改变 repository（不写文件、不移动/删除），不得为了制造新行为而运行应用（不 build、不 test、不 lint、不 typecheck、不 benchmark、不 migrate、不启动应用、不安装包）。「是否只读」由**该 act 是否揭示已存在事实**决定，不由「是不是 shell 命令」或「是否写了文件」决定：Git 历史与文件系统 metadata 的窄读取（`git log`/`git show`/`git blame`/`git stat`）属于静态观察。

**含义/动机**：观察若制造新行为，产出的「证据」就不是对原世界的描述，而是对已改变世界的描述——replay 会失效，claim 不可追溯。`query-shell` 的负清单（build/test/lint/typecheck/application startup/migration/generation）是这条律的正面实现。

**边界**：repository mutation 的合法面 → `repository-programming`；进程执行 → `process-execution`；Casebook 的 replay 重放也遵循只读（fetch 不写 subject → `knowledge-reuse` 交叉）。

**证据**：→ `PROOF.md` REPOSITORY-INVESTIGATION-005 行。

## REPOSITORY-INVESTIGATION-006 — warm-start/semantic search 命中是低信任 orientation，须真实观察确认

**规范陈述**：warm-start（RepositoryWarmStart）与内部 Semble 搜索的命中只是**低信任 orientation data**，必须由真实观察确认后才成为 fact。命中不得伪装成 `read`/`grep`/工具历史；不得直接写入 Casebook；provider 措辞永远不得说「Semble 确认 X 不存在」（零命中可能是 disabled/timeout/截断/index 行为，不是 absence）。Semble 不是 Host MCP、不是 provider tool、不是 permission、不是 Strength 能力。

**含义/动机**：semantic search 是概率性 orientation，不是证据采集。把 hit 当 fact = 把「相关」当「真实」；把零命中当 absence = 把「没找到」当「没有」。

**边界**：Casebook 复用（hint 能否被 cache）→ `knowledge-reuse`；Semble 的进程/启动机制 → HOW（`host-boundary` 交叉）。

**证据**：→ `PROOF.md` REPOSITORY-INVESTIGATION-006 行。

## REPOSITORY-INVESTIGATION-007 — explicit keywords 每次 fresh search；无 keywords 零工作

**规范陈述**：warm-start 只由显式 keywords 驱动：每个非空行是一个完整 Semble query（不按空格切词）；无 keywords / 全空白时必须**零 Semble 工作**且 provider prompt 与原 charge **字节完全相同**。显式 keywords 每次 fresh search：不得自动从 charge 抽词（无 tokenizer/noun picker/LLM generator）、无 cross-call warm-start cache。

**含义/动机**：自动抽词把优化变成第二 assignment——系统替模型决定「该找什么」；cross-call cache 让旧搜索结果冒充新搜索。显式 keywords 保持 charge 的 authority 完整。

**边界**：normalize 的具体规则（LF 分行/trim/稳定 dedupe/`MaxKeywords`）是 HOW 细节（bounds 见 REPOSITORY-INVESTIGATION-009）；「显式 + fresh + 零 keywords 零工作」才是合同。

**证据**：→ `PROOF.md` REPOSITORY-INVESTIGATION-007 行。

## REPOSITORY-INVESTIGATION-008 — keywords 只对直接消费者；repoPath 只用真实 WorkspaceDirectory

**规范陈述**：V1 直接消费者恰为 `Coder | Inspector | DevOps`——只有本来就允许直接生活在 repository evidence 中的角色可接收 repository snippets。其它角色只能在既有 invocation DAG 上把 keywords **携带**给这些角色，不能因此获得 snippets（Reviewer 拒绝任意 caller keywords；Orchestrator `commission` 不增加 keywords）。repoPath 只用真实 `WorkspaceDirectory`；缺失/不可信时跳过 warm-start，禁止猜 `"."`。

**含义/动机**：把 snippets 发给无取证权角色 = 借 side channel 泄露 repository 内容。猜 repoPath 会搜索错误世界——错误的 repository hint 比没有 hint 更糟。

**边界**：谁能通过 invocation DAG 携带 keywords 由既有 delegation/tool surface 裁决（→ `delegation`/`office-capability`）；「直接消费者门 + 真实路径」才是本命题。

**证据**：→ `PROOF.md` REPOSITORY-INVESTIGATION-008 行。

## REPOSITORY-INVESTIGATION-009 — warm-start 有界且确定

**规范陈述**：所有独立 query 在一个 **bounded parallel wave** 中执行（禁止串行 `await K1; await K2; ...`）；merge 恢复 `keyword ordinal → local rank`，按 `FilePath + StartLine + EndLine + Content` 稳定去重（并行完成顺序不得影响 prompt 字节）；最终 hint 数有界。超限只删除**完整 hint entry**，绝不截断 TOML 字符串。单 query failure、Semble disabled/timeout/launch failure 均 **fail-open**（不得失败或串行化工作 invocation）。

**含义/动机**：并行完成顺序影响结果 = 非确定性；截断 TOML 字符串 = 破坏表示。warm-start 是优化，不是正确性依赖——任何搜索故障都不能让工作 invocation 失败。

**边界**：具体 bound 数值（`MaxKeywords=8`/`TopKPerKeyword=4`/`MaxHintsTotal=24`/`MaxWarmStartBytes=64 KiB`）是 HOW 常数（HANDOFF §12）；「有界 + 确定 + fail-open」才是合同。

**证据**：→ `PROOF.md` REPOSITORY-INVESTIGATION-009 行。
