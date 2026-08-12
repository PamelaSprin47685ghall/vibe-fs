# Enforcer — 理由

规则曾由规范生成 F#：变更绑编译、多份清单漂移。规则是数据：每个 tip 一个目录（`enforcer.md` + `main.md`）+ Domain 校验。拒绝 dist 双副本与代码内 fallback catalog——后者掩盖打包错误。也拒绝并行维护 `catalog.json` 元数据第二真相：目录扫描已经给出 TipName 与正文，JSON 只会变成第二个会漂的 ordinal/field 表。

tip 取代 score-vector：把「评分并集」从控制流里挖掉，只保留目录 TipName 枚举与 cycle 原子提交。`chronicle` 立即返回，是避免工具路径变成隐藏的第二会话循环；工具名是动词（记录一次 occurrence），不是名词博客场。

双文件不是重复文档：`enforcer.md` 约束 Chronicler 的检测边界与 tip 选择；`main.md` 服务 Main 的可执行补救。同一 TipName 绑定两边，避免「分类标签」与「给人看的建议」脱节，又禁止把 Main 指导泄漏进 Y system 或把检测散文塞进 Main Authority。Rulebook = 这两面合称：Detection Wing 全量继承给 Blogger office；Remediation Wing 以 Triggered Folio 按 occurrence 交付给 Main。

Full/Identity 经 tip 交付事实 fold：首次给全文、重复给身份，是为了在重放与 compaction 后仍可证明「Main 是否已见过该 tip 正文」，而不靠进程内存。但「已交付」≠「全文此刻仍可从 horizon 恢复」：TipDeliveryFrontier 按 occurrence 单调耐久；TipSemanticCoverage 按 TipName 相对当前 provider horizon，reanchor 可丢失。覆盖丢失后再次给出 full main.md 是语义恢复，不是新 occurrence——拒把二者压成一个 durable bool。

Observation 配对（tip↔frame zip）而不是两路平行流：Companion 重建时需要「上一条 tip 对应哪一段 work log」的稳定故事；squash 时 tip co-truncate，且 squash 只是历史表示变换（K→1、保留代表 TipIdentity），不创造新 TipOccurrence、不触发新 Main 交付——压缩可改写记忆形状，不得再造事件。Observation 先作为 domain 视图命名，而不是立刻爆炸 EventStore 事件类型——物理事实仍由 Chronicle 记账事件承担，避免 Journal 双写与命名漂移。

## 备选与被拒

**规则载体：规范生成 F# vs 目录 Markdown + Domain 校验。** 拒生成代码：变更绑编译、多份清单漂。规则是数据、按 tip 目录打包，运行期扫描校验。

**分发：单一打包 vs dist 双副本/代码 fallback。** 拒双副本：掩盖打包错误；拒代码内 fallback catalog：让坏的打包静默成功。resource 随 npm pack 单份发布。

**元数据：`catalog.json` vs 目录即清单。** 拒 JSON 第二真相：field/id/ordinal 与文件夹名双写必漂；lexical order 由扫描排序派生即可。

**激励：score-vector 评分 vs tip 单一字段。** 拒评分：把「评分并集」烙进控制流、不可定序。tip 只有目录 TipName 枚举 + cycle 原子提交，可测且无解释器负担。

**记账工具：`blog` 名词场 vs `chronicle` 动词。** 拒 `blog`：把工具伪装成博客会话/媒体对象。选 `chronicle(entry, tip)`：一次 occurrence、立即返回、只记账不编排；删 `evidence` 字段——若证据改变 occurrence，它应进入 entry。

**记账时机：立即返回 vs 长流程。** 拒长流程：工具路径变成隐藏的第二会话循环。

**所有权：物理轴 vs 镜像 State cell。** 拒把 busy/parked/pending/drain 压进互斥 runtime cell：程序位置伪装成事实，重启后只能从 transcript 猜业务。选物理所有权轴（`HasFlight` / `HasParked` / `PendingOffer` / `DrainWindow`），busy 唯一含义是 Host 持有 flight，恢复只从 durable facts 与可证明 Host snapshot 重建。

**tip 交付：Blogger 低信任历史 vs Main overlay；Full 每次 vs Full/Identity。** 拒向 Main 注入工程 fake-user message：等于给 Main 建立第二个 Authority 解释器，污染投影、seal 与恢复。tip 对 Y 只回投 projection 低信任历史；对 Main 用正式交付事实 + main.md。拒每次 Full：重复烧上下文且无法区分「已交付」；拒仅 Identity：首次无正文可执行。选 Full 一次 + Identity 重复，且 Identity 仅当当前 TipSemanticCoverage 仍可恢复全文时合法。

**交付前沿 vs 语义覆盖：单一 durable bool vs 两轴分离。** 拒单一 bool：reanchor 后要么误删已交付事实、要么假装全文仍在 horizon。选 TipDeliveryFrontier（occurrence、单调）⊥ TipSemanticCoverage（TipName、horizon-relative）；恢复全文 ≠ 新 occurrence。

**squash：压缩即新 tip 事件 vs 表示变换。** 拒压缩创造 TipOccurrence / 触发 Main 交付：把记忆重写伪装成又一次世界教训。选 K→1 表示变换 + 代表 Tip 保留 + tip co-truncate。

**Observation：平行 tip/frame 流 vs 配对 unit；独立 Observation 事件 vs fold 视图。** 拒平行流：重建顺序不确定、squash 后难对齐。拒立刻新增整族 Observation EventStore 事件：与 Chronicle 记账双写、命名易与 Casebook Observation 撞车。选 domain `ObservationUnit` + 既有 Chronicle/Enforcement fold，名称在文档与配对 API 中明确为 Observation。
