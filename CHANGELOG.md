# Changelog — 版本历史

## 0.5.0-rc.1 — docs freeze / RC development

Breaking changes:
- All agents now require explicit `fast-*` or `deep-*` names
- Unprefixed agent names, `build`, `plan` aliases removed
- Agent-to-model bindings read exclusively from `opencode.json`
- All Wanxiangshu model environment variables removed
- No longer persists or overrides model IDs
- Provider fallback cycles A/A/B/B within budget（Cursor 无限定义；自动恢复上限默认 12 连续失败）
- Provider retry count no longer kills a Logical Run
- Blogger and Executor Agent are now internal fast/deep pairs
- Pre-0.5.0 runtime journals not supported

## 0.4.0 — 最终版

- Structured Agent Program (Flow CE, no Stage/Phase/Lease platform)
- Prompt Authority / Logical Run rules
- Companion + ActivePrefixEpoch / FrozenB cache protection
