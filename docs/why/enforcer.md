# Enforcer — 理由

规则曾由规范生成 F#：变更绑编译、多份清单漂移。规则是数据：每个 tip 一个目录（`enforcer.md` + `main.md`）+ Domain 校验。拒绝 dist 双副本与代码内 fallback catalog——后者掩盖打包错误。也拒绝并行维护 `catalog.json` 元数据第二真相：目录扫描已经给出 TipName 与正文，JSON 只会变成第二个会漂的 ordinal/field 表。

tip 取代 score-vector：把「评分并集」从控制流里挖掉，只保留目录 TipName 枚举与 cycle 原子提交。blog 立即返回，是避免工具路径变成隐藏的第二会话循环。

双文件不是重复文档：`enforcer.md` 约束 Blogger 的检测边界与 tip 选择；`main.md` 服务 Main 的可执行指导。同一 TipName 绑定两边，避免「评分标签」与「给人看的建议」脱节，又禁止把 Main 指导泄漏进 Y system 或把检测散文塞进 Main Authority。

Full/Identity 经 `TipGuidanceDelivered` fold：首次给全文、重复给身份，是为了在重放与 compaction 后仍可证明「Main 是否已见过该 tip 正文」，而不靠进程内存。重锚清空 Full 集合，是避免 IdentityOnly 在截断 transcript 后永远搁浅。

Observation 配对（tip↔frame zip）而不是两路平行流：Companion 重建时需要「上一条 tip 对应哪一段 work log」的稳定故事；squash 时 tip co-truncate，避免 tip 历史比 frame 覆盖范围活得更久而变成幽灵上下文。Observation 先作为 domain 视图命名，而不是立刻爆炸 EventStore 事件类型——物理事实仍由 BlogEntry/BlogSquash 承担，避免 Journal 双写与命名漂移。

## 备选与被拒

**规则载体：规范生成 F# vs 目录 Markdown + Domain 校验。** 拒生成代码：变更绑编译、多份清单漂。规则是数据、按 tip 目录打包，运行期扫描校验。

**分发：单一打包 vs dist 双副本/代码 fallback。** 拒双副本：掩盖打包错误；拒代码内 fallback catalog：让坏的打包静默成功。resource 随 npm pack 单份发布。

**元数据：`catalog.json` vs 目录即清单。** 拒 JSON 第二真相：field/id/ordinal 与文件夹名双写必漂；lexical order 由扫描排序派生即可。

**激励：score-vector 评分 vs tip 单一字段。** 拒评分：把「评分并集」烙进控制流、不可定序。tip 只有目录 TipName 枚举 + cycle 原子提交，可测且无解释器负担。

**blog 时机：立即返回 vs 长流程。** 拒长流程：工具路径变成隐藏的第二会话循环。blog 立即返回，只记账不编排。

**所有权：物理轴 vs 镜像 State cell。** 拒把 busy/parked/pending/drain 压进互斥 `BloggerRuntimeState`：程序位置伪装成事实，重启后只能从 transcript 猜业务。选物理所有权轴（`HasFlight` / `HasParked` / `PendingOffer` / `DrainWindow`），busy 唯一含义是 Host 持有 flight，恢复只从 durable facts 与可证明 Host snapshot 重建。

**tip 交付：Blogger 低信任历史 vs Main overlay；Full 每次 vs Full/Identity。** 拒向 Main 注入工程 fake-user message：等于给 Main 建立第二个 Authority 解释器，污染投影、seal 与恢复。tip 对 Y 只回投 projection 低信任历史；对 Main 用正式 `TipGuidanceDelivered` + main.md。拒每次 Full：重复烧上下文且无法区分「已交付」；拒仅 Identity：首次无正文可执行。选 Full 一次 + Identity 重复 + reanchor 重置。

**Observation：平行 tip/frame 流 vs 配对 unit；独立 Observation 事件 vs fold 视图。** 拒平行流：重建顺序不确定、squash 后难对齐。拒立刻新增整族 Observation EventStore 事件：与 BlogEntry 双写、命名易与 Casebook Observation 撞车。选 domain `ObservationUnit` + 既有 Blog/Enforcement fold，名称在文档与配对 API 中明确为 Observation。
