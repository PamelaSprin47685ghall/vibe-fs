# Casebook — 理由

Inspector 的一次调用天然形成知识单元（Question → 调查 → Answer），调用结束后只存在于 transcript。后续 Inspector 面对相同问题必须重新调查，已消耗的 read/glob/grep 证据无法复用。Casebook 让旧答案可被 fetch、按当前 worktree 重放 observations、无变化时直接复用——best-effort semantic cache，不是知识数据库。

fetch 不直接信任旧答案：observation replay 只是 freshness hint，任何 merge 标量或 EventStore 物理顺序都不证明答案正确（no-delta 只是 hint）。检测到变化时启动私有 Bookkeeper Agent 修订 Q/A；失败保留旧 Case 返回旧 A——允许过时是预期产品语义。

Bookkeeper 的工作是重塑已供给证据上的一个 staged Case，不是再进仓库取证。Provider 工具是 `js-bookkeeper(program)`：一次程序原子变换 Case（`setQuestion`/`setAnswer` 各至多一次，zero mutation 合法），拒 `edit-qa(document, old_text, new_text)` 把 Case 拆成两份文档竞态改字符串。Persona = Clerk/Curator；机器强度 id 为 `fast-bookkeeper`/`deep-bookkeeper`（可复用 inspector 模型绑定，不复用 Inspector self-model）。拒借用 Scout/Investigator 自我模型——那会诱导 Bookkeeper 假装拥有调查权。

Casebook 不拥有独立 durable store：Case 事实以 InspectorCase* events 表达，Q/A/snapshot 大正文经 PayloadRef 进入统一 payloads；物理耐久与同步落在统一 EventStore（Persist/GitGateway）。replica 收敛 = EventStore 集合并；同 Case 合法并发 fork 由投影表达为 DomainConflict——禁止 (revision, wall_clock) LWW。Provider 可见 index 只暴露 shelfmark + canonical Q，不泄漏 session/status/freshness 机器字段。

## 备选与被拒

**独立 Git store / refs / hook vs 统一 EventStore。** 拒前者：feature store 无法共享 Persist 的 merge/CAS/恢复（PERSIST 系条款）；remote 同步是 dumb-remote ConvergeStore 的职责，不是 Casebook 自有 sync（CASE-007）。

**timestamp / revision 决定 freshness 与 merge winner vs replay + set union。** 拒前者：时间戳不证明内容未变；revision 排序制造第二真相（CASE-004/011）。

**逐调用 finalize vs scope 级 finalize。** 拒前者：每个 return 产生一次 provider 事务与 Case，复用 Inspector 的多次调查被碎片化；ReuseScope close 一次 finalize 才对应一个 Case（CASE-010）。

**从 transcript 文本推断 observation vs typed capture。** 拒前者：文本推断在重放时不可靠、不可重放；capture 必须来自工具执行的 typed 结果（CASE-003）。

**full knowledge base vs best-effort cache。** 拒前者：Casebook 不保证历史 Q/A 可追溯为产品 API，不建立 commit history，不改变 subject worktree（CASE-001/002）。

**无 marker 也运行 vs 完全 opt-in。** 拒 opt-out：未启用 repository 的行为必须与现状逐字节一致；marker 缺失时工具 schema、执行 registry、archive 全部静默消失（CASE-009）。

**Case 编辑：`edit-qa` 双文档字符串替换 vs `js-bookkeeper` 单程序。** 拒前者：Q/A 分文件竞态、多次短语编辑无法保证 Case 仍描述同一世界。选一次 JS 程序原子重塑；Bookkeeper 先作语义判断，程序只执行一致变换。

**Persona：借用 Inspector self-model vs Clerk/Curator。** 拒借用：调查自我模型暗示可回世界取证，破坏「证据已供给」边界。选独立 Clerk/Curator；机器 id `fast-bookkeeper`/`deep-bookkeeper`（OPEN frozen），模型绑定可复用、自我模型不可复用。
