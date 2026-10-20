# CUT-20 证据

- `base-red-git-grep-spooled.mjs` / `base-red-output.json`：base master dist 上真实 `git grep` 6000 行冲突标记 → Spooled，GitAdapter 映射为空 stdout，spool 文件泄漏 1 个。
- `wxl-full-test.summary`：wanxiangli `20e0372`（rebase 到 0.12.0 后）全量 `node --test --test-force-exit`：531/531，Node 22.21.1，Bun 1.4.2 存在。
- `host-process-execution.summary`、`host-change-integration.summary`：主仓候选（gitlink 6bf65d3）重建后的结果。
- 环境：Linux x64，Node 22.21.1（`/workspace/vfs-cut/tools/node22`），npm 10.x，Fable 5.13.0（dotnet tool），共享 8 核机。
