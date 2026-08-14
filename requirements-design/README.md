# Requirement Package ontology — design workspace

本目录是未来 Requirement Package 架构的设计稿，**不是当前 normative authority**。当前仓库的正式语义仍由 `docs/` 定义；本目录用于从这些证据重新因式分解未来 ontology，避免在迁移完成前形成两个同时自称权威的世界。

设计目标不是维持某个 package 数量。旧 36 包仅作考古工作集；本目录允许拆分、合并、新增被证据证明存在的独立 WHY，也允许把兼容/迁移事故判为 garbage。

## 接受标准

一个候选 package 必须同时满足：

1. 有一句不可替代的 WHY。
2. WHAT 能被解释成一组当前世界必须同时成立的命题，而不是当前文件/模块清单。
3. `OWNS` 与 `DOES NOT OWN` 能划出唯一 semantic owner。
4. RED verdict 能一句话解释世界哪里不成立。
5. 至少存在一次重大 redesign，可只修改本包而保持相邻包 WHAT 不动。
6. proof 可以恰好归一个 package；组合产生的新语义需要新的 owner，而不是共享 test ownership。

## 目录

- `HANDOFF.md`：新对话保姆级交接；任务、边界铁律、当前裁决、下一步与启动 Prompt。
- `INDEX.md`：当前候选集合、当前数量与依赖骨架。
- `01-*.md` … `21-*.md`：按语义邻域组织的 boundary cards；物理文件名只是设计期分组，不是 package identity。
- `AUDIT.md`：ORPHAN / OVERLAP / GARBAGE / unresolved boundary questions 的统一账本。
- `PROOF-MAP.md`：现有 tests/gates 向未来 unique proof ownership 的第一版迁移投影。

## 非目标

本目录暂不：

- 创建真正的 `requirements/<package>/WHAT.md` normative tree；
- 迁移或删除 `docs/`、`changes/`、`tests/`、`scripts/checks/`；
- 为每个旧 Clause 找新家；
- 保证每个旧 test 必须迁移；
- 固化 OpenCode hook、F# module、JS/TOML、当前 agent 名、当前 event shape 为产品 ontology。

真正迁移应在 ontology + dependency graph + proof ownership 经过全仓反向覆盖后一次完成。
