# Enforcer — 理由

规则曾由规范生成 F#：变更绑编译、多份清单漂移。规则是数据：打包 JSON + Domain 校验。拒绝 dist 双副本与代码内 fallback catalog——后者掩盖打包错误。

tip 取代 score-vector：把「评分并集」从控制流里挖掉，只保留 catalog 字段枚举与 cycle 原子提交。blog 立即返回，是避免工具路径变成隐藏的第二会话循环。

## 备选与被拒

**规则载体：规范生成 F# vs 数据 JSON + Domain 校验。** 拒生成代码：变更绑编译、多份清单漂（ENFORCER-071 前身教训）。规则是数据、打包 `catalog.json`，运行期校验。

**分发：单一打包 vs dist 双副本/代码 fallback。** 拒双副本：掩盖打包错误；拒代码内 fallback catalog：让坏的打包静默成功。resource 随 npm pack 单份发布。

**激励：score-vector 评分 vs tip 单一字段。** 拒评分：把「评分并集」烙进控制流、不可定序。tip 只有 catalog 字段枚举 + cycle 原子提交，可测且无解释器负担。

**blog 时机：立即返回 vs 长流程。** 拒长流程：工具路径变成隐藏的第二会话循环。blog 立即返回，只记账不编排。

**所有权：物理轴 vs 镜像 State cell。** 拒把 busy/parked/pending/drain 压进互斥 `BloggerRuntimeState`：程序位置伪装成事实，重启后只能从 transcript 猜业务。选物理所有权轴（`HasFlight` / `HasParked` / `PendingOffer` / `DrainWindow`），busy 唯一含义是 Host 持有 flight，恢复只从 durable facts 与可证明 Host snapshot 重建。

**tip 交付：Blogger 低信任历史 vs Main overlay。** 拒向 Main 注入工程 fake-user message：等于给 Main 建立第二个 Authority 解释器，污染投影、seal 与恢复。tip 只回投 Blogger 自己的 projection 作为低信任历史，Main 语义仍由正式域独有，不改写 Main Authority。
