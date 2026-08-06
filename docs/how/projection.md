# Projection — 目标实现

## PROJ-008：迁移顺序

旧投影必须按以下顺序迁移到 DSL：

1. 普通 X + ActivePrefixEpoch projection
2. attempt-local PrefixProbe projection
3. Companion BloggerMain / BloggerSquash / BloggerDelta projection
4. InteractionRepair projection
5. ReviewConfirmation + skeptical challenge Seal projection
6. Host compaction reanchor 后 projection
7. Strength Primary/Replica projection（含 transport-only suppression）

迁移期间测试环境可同时运行 LegacyProjection 和 DslProjection 并比较 canonical digest；生产环境不得按请求随机混用两套实现。

切换条件：所有历史 canary 轨迹 `LegacyDigest = DslDigest`；允许有意变化的差异须有明确的新 SSOT 条款。

切换后删除 `LegacyProjection`，不长期维护双实现。

---

## 历史说明

`docs/proposal/strength.md` 第 19 节把 Projection DSL 称为「spec/13 — Projection Algebra（条款前缀 `PROJ-`）」。本文件生效后，该引用 superseded 为 `docs/what/projection.md`（条款前缀 `PROJ-`）。
