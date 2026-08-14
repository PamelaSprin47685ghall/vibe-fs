# WHY — external-investigation

## 一句话理由

external / public-web facts 来自会变化、会冲突、需要 provenance 的远方世界；网络可达性
不等于 source ownership，外部可能性也不能自动变成 repository obligation。

## 不可替代性：为什么不能并进别的包

- **不是 `repository-investigation`**：本地 repository fact 与 public-web provenance 的
  source law 不同（HANDOFF §7.8）。Inspector 观察的是本地文件树（可定位、可追溯的真实
  observation）；Browser 跨越到会变化、会冲突、无法单点验证的远方世界。不能因为两者都
  「查资料」就并入一个 investigation 包（HANDOFF §6.9）。
- **不是 `office-capability`**：office 有资格产生什么后果归 office-capability；「Browser
  这个 office 如何建立带 provenance 的外部事实」归本包。
- **不是 `capability-enforcement`**：schema/runtime gate 同源归 capability-enforcement；
  「外部事实的 evidence contract」归本包。
- **不是 `host-boundary`**：stealth-browser MCP 的 uvx/ref/env 启动判定是 Host adapter
  机制（host-boundary HOW）；本包拥有的是它建立的事实如何带 provenance。
- **不是 `epistemic-reasoning`**：认识状态求解器（proposal/evidence/不确定性）归
  epistemic-reasoning；本包只拥有「外部事实采集的 source law」。

## 失败模式（RED 长什么样）

1. **网络可达内容被无 provenance 当作事实**：一个网页「能打开」不等于「可据此断言」。
   `Reachability does not determine ownership. Provenance does.`
2. **来源冲突被抹平**：可靠来源互相冲突时静默平均成合成中间值——`Disagreement is not a
   confidence average`。分歧本身是远岸显示的一部分。
3. **外部可能性被直接升级成 repository obligation**：web 上「似乎应该改 X」不自动成为
   仓库义务；外部事实只建立外部世界事实。
4. **条件丢失**：version / date / jurisdiction / feature flag 等条件改变即改变事实；
   把有条件的主张洗成无时间通则 = 事实丢失。

历史病灶（`resources/provider/role/browser/en.md` 是唯一散文合同，无 F# runtime
provenance 类型）：真实 browsing 在外部 `stealth-browser-mcp`，Wanxiangshu 只注入服务器
+ 按角色锁 + Browser Role Law 固化 provenance contract。HANDOFF §29（Oracle 1）发现现有
proof 只有 5 条**松**锚点（`/disagreement/i`、`/far shore/i` 这类单词级），合同退化成
反面也能通过——因此强化成 8 条锁定实质区分的锚点，并补 browser-provenance canary。

## 独立变化测试

从当前 browser backend 换成另一 browser/search backend，只要 provenance / evidence
boundary 不变 → 本包可独立重大变化（`20-capability-external.md` INDEPENDENT CHANGE）。

## DEPENDS ON（`archive/requirements-design/INDEX.md` 依赖骨架唯一来源）

- `office-capability`：Browser office 的 entitled consequence 是前提（external fact
  acquisition 是 office 的职责）。
- `participant-horizon`：外部事实进入 participant experience 的准入过滤是前提。
- `host-boundary`：外部浏览的物理能力（MCP/网络）由 Host 提供，业务只消费稳定观察。
