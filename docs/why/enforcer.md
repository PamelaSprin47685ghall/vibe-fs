# Enforcer — 理由

规则曾由规范生成 F#：变更绑编译、多份清单漂移。规则是数据：打包 JSON + Domain 校验。拒绝 dist 双副本与代码内 fallback catalog——后者掩盖打包错误。

tip 取代 score-vector：把「评分并集」从控制流里挖掉，只保留 catalog 字段枚举与 cycle 原子提交。blog 立即返回，是避免工具路径变成隐藏的第二会话循环。

## 备选与被拒

**规则载体：规范生成 F# vs 数据 JSON + Domain 校验。** 拒生成代码：变更绑编译、多份清单漂（ENFORCER-071 前身教训）。规则是数据、打包 `catalog.json`，运行期校验。

**分发：单一打包 vs dist 双副本/代码 fallback。** 拒双副本：掩盖打包错误；拒代码内 fallback catalog：让坏的打包静默成功。resource 随 npm pack 单份发布。

**激励：score-vector 评分 vs tip 单一字段。** 拒评分：把「评分并集」烙进控制流、不可定序。tip 只有 catalog 字段枚举 + cycle 原子提交，可测且无解释器负担。

**blog 时机：立即返回 vs 长流程。** 拒长流程：工具路径变成隐藏的第二会话循环。blog 立即返回，只记账不编排。
