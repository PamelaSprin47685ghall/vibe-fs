# CUT-33：两仓构建、打包与发布闭环

返回 [看板](../BOARD.md) / [职责卡](../CARDS.md#cut-33)。

## 1. 认领与重判

- Owner：`eng-cut06-goose`；认领 `0a3e45869`；base 主仓 `7f28c3562`、wanxiangli `2564387`。
- 建板时的链是 `vendor/wanxiangli-0.5.2.tgz` 被 Git 跟踪、`file:vendor/...` 消费。仓主此后改定（主仓 AGENTS.md「源码与运行数据分离」）：`wanxiangli/` 为正式 submodule（跟踪 main，`.gitmodules` branch=main），npm 以 `file:wanxiangli` 消费并 bundle，任何 `.tgz` 不得进 Git，`scripts/wanxiangli-source.mjs check` 强制。本卡按现行规则交付，不恢复 vendor。

## 2. 现行链（实测）

1. 库：`scripts/build.mjs` 每个带 fsproj 的模块独立 Fable 编译 + esbuild 打包到 `dist/<module>/`；`package.json` 的 exports/files 手写。
2. 主仓：gitlink 固定库提交 → `npm ci` 使 `node_modules/wanxiangli` 为指向 `../wanxiangli` 的链接 → `node scripts/build.mjs` 先 `buildSourceDependency`（submodule 内 `npm ci` + 库全量构建）再编译宿主 → `scripts/verify-package.mjs` 真实 `npm pack` 宿主，`node_modules/wanxiangli/**` 作为 bundleDependency 进包，隔离目录解包消费。

## 3. 本轮交付

| 项 | 内容 | 位置 |
|---|---|---|
| 库侧门禁 | `SPEC[build-008]`：每个已实现模块恰好一个 exports 项、指向 build 产出的入口文件；类型声明存在且在 files；无指向已删模块的入口；files 中无陈旧声明 | wanxiangli `5edd3c7`，`scripts/tests/exports.test.mjs`，`scripts/AGENTS.md` LEMMA 3 / SPEC 8；SPEC 7 由"未执行"更新为已执行 |
| 指南校正 | README §1/§4/§5/§6E/§8 改为 submodule + gitlink 流程；COVERAGE、CARDS 中 vendor/"不提交 gitlink" 的过时句校正 | 本提交 |

## 4. 证据

| 命令 | 结果 |
|---|---|
| `node --test scripts/tests/exports.test.mjs scripts/tests/repository.test.mjs`（库 0.9.0，rebase 后于 `5edd3c7`） | 4 pass / 0 fail |
| 变异：删 `./delivery` 入口与其 files 项 | build-008 第 1 项红 |
| 变异：删 `src/wait/index.d.ts` 的 files 项 | 第 2、3 项红 |
| 变异：加指向已删 `workrecord` 的入口 | 3 项全红；还原后复绿 |
| `node scripts/wanxiangli-source.mjs check`（主仓 `7f28c3562`） | exit 0 |
| `node scripts/verify-package.mjs`（主仓 `7f28c3562`，build generation 4） | OK：1356 members、11087350 bytes、tarball digest `73bcbf4f…bc34b`；包内 `node_modules/wanxiangli/package.json` 为 0.9.0，含 `dist/delivery/Delivery.js` 与 `src/delivery/index.d.ts` |

环境：Node v20.19.2（低于主仓 engines `>=22.13.0`，如实记录）、npm 11.21.0、Bun 1.4.2，Linux x64。

## 5. 未闭合（不计入本轮）

- gitlink 必须指向已在库 main 上的提交：目前只靠流程（指南 §8），`wanxiangli-source.mjs check` 尚未校验祖先关系；加入它会让库未推送前的本地 check 失败，需要与在制 worker 协调后再加。
- 库 package version 与字节的对应不再是宿主的身份（gitlink 才是）；库内同版本号不同字节未被机械禁止。
- 全量 `scripts/verify.mjs`、release 层 e2e 与 macOS/Windows 未验证。
