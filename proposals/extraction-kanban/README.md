# 多人抽吸施工指南

基线：2026-10-15，主仓 `master@8f7f9be77`；独立库 `wanxiangli main@46d9c07`，npm 版本 `0.5.2`。

**先选卡、确认边界和反例，再各自在工作树里施工。一个职责只认一个 owner，一次集成只认一份候选，一张卡最后必须说清旧系统少负责了什么。**

这是后续抽吸工作的共同蓝图，不是全系统已经审计完毕的声明。目录与需求包已经纳入分工；每卡开工仍需追踪实际调用者、在途状态和持久数据。包名、接口与删除清单中的待定项必须在该卡准备阶段落实，不用想象填空。

**最新复核：** [四模块源码复核、问题报告与修复计划（2026-10-15）](SOURCE-AUDIT-2026-10-15.md)。主仓 `7d8feff03` 的 CUT-02/03/04 有已确认的接线缺陷；历史合入记录保留，未闭合范围见 [关联修复卡](BOARD.md#source-audit-repairs)。

## 入口：五份文件，各管一件事

| 文件 | 用途 | 谁改 |
|---|---|---|
| [BOARD.md](BOARD.md) | 唯一看板：状态、认领、前置、公共编辑面预约、集成次序 | 当轮协调者串行登记 |
| [CARDS.md](CARDS.md) | CUT-00—CUT-38 的完整职责卡：切什么、不切什么、怎么证伪、何时删旧 | 该卡 owner；改边界须让相邻卡评审 |
| [COVERAGE.md](COVERAGE.md) | 主仓需求、源码区域、新库草稿和原施工计划的归属与冲突 | owner 提交，协调者合并 |
| [TEMPLATES.md](TEMPLATES.md) | 认领、契约、证据、PR、交接和异常处置模板 | 通用模板由协调者维护 |
| 本文 | 日常操作、分支、交付、验收和回退方法 | 共同工作约定 |

后续单卡的详细材料放在 `cards/CUT-NN.md`，证据索引放在 `evidence/CUT-NN/README.md`；按需创建，不先制造几十个空文档。子卡确有独立 owner 时使用 `CUT-NN-a`，先给看板加行与前置再开工。卡号永久保留，不重排、不复用。

## 1. 从哪里开始

### 已有成果，不重复重写

Lifecycle 已通过 `wanxiangli/work`、`eventstore`、`durable-work` 接回主仓，对应旧 Facts/Fold/Kernel/Codec 等已退出。这是 CUT-01 的完成范围，不代表整个应用或全部 GAP 已完成。[合并比较与收尾证据](../lifecycle-cutover-closeout.md)

建板基线中，`wanxiangli/process`、`trace`、`ownership` 已有独立实现与测试，主仓尚只接入前三个生命周期入口。随后 CUT-02/03/04 已合入接线；本次完整源码复核发现部分产品义务尚未承接，状态与反例以最新看板和上述报告为准。导入包、基础测试通过或历史 DONE 均不能单独证明职责已迁完。

主仓锁定并跟踪 `vendor/wanxiangli-0.5.2.tgz`。旁边的 `wanxiangli/` 是另一 Git 仓，不是主仓源码依赖；新成员只克隆主仓也必须能构建。位置变化不应影响 npm 消费。[分发要求](../../requirements/distribution/WHAT.md)

### 第一批怎么分人

先由一人完成 CUT-00 的基线、公共文件预约与两仓集成安排，其余人可以同时做只读盘点。准备完成后，建议按专长分派：

| 工作位，不是已分配人员 | 第一张卡 | 当轮交付 |
|---|---|---|
| 集成与验证 | CUT-00；随后维护 CUT-33 的交付路径 | 认领记录、基线、唯一打包/接线队列 |
| 物理执行 | CUT-02 | 一个真实非 PTY 调用方改用 ProcessSession；硬期限和完整输出义务不丢 |
| 历史与上下文 | CUT-03 | 确认捕获差异表、区间契约，以及一条真实捕获/恢复链 |
| 资源收尾 | CUT-04 | 一类资源由唯一 Ownership 跟踪；取消不冒充结束 |
| 相邻独立工作 | 从 CUT-05、07、18、21、24、26 中选一张 | 先完成卡内契约与反例，不碰别人的公共接线 |
| 审查/基础设施 | 审前三条边界；空档做 CUT-32、37 的调查 | 能推翻方案的用例、可复跑证据或实测成本 |

只有三个人时，不需要虚设六个岗位：两人各做一张产品卡，一人负责评审和串行集成；角色可以轮换。人数增加先增加独立卡与评审能力，不增加同一公共文件的写入者。

**默认在制上限：一人一张正在修改的卡，全队同时三条接线链；打包/锁文件更新一条，主仓集成一条。** 这是初始工作约定，不是性能指标。排队时先帮助复核和清理，不额外开启一堆半成品；确有资源与评审能力再在看板记录调整。

## 2. 一张蓝图，不等于预写全部实现

终点是：可移植职责在独立库里成立，宿主代码只承接真实宿主能力，组合者负责合作，过期实现退出。主仓可以留下必要的 OpenCode 适配、组装、资源及明确保留的产品组件，不要求物理目录变空。

看板列出的全部现有职责都必须有去向：抽取、经批准删除、或明确保留。**明确保留不是抽吸完成**；它必须标出理由、边界、继续维护者，并在最终盘点中列作有意保留。没有处置的遗留代码不能藏在“公共基础设施”或“稍后处理”里。

后续发现边界不成立，可以在同一蓝图修订该卡或增补子卡，不另建第二张竞争看板。变更须保留旧卡号、原前提、反例、受影响卡与新的删除条件。远期卡给出职责、证据和约束，不假定其 API 已冻结。

正式用户行为以 [requirements](../../requirements/README.md) 和各包 WHAT 为准；旧计划中的 T/WP/GAP 是证据索引，不自动成为新的库切分。新库草稿与现行要求不一致时，先记到 [差异表](COVERAGE.md#draft-conflicts)，未经明确裁决不得借抽取改变用户行为。

## 3. 共同设计准绳

**各就其位。** 准备下一条可运行闭环所需的事实、输入、输出、状态和反例，不先造全套框架。小步按是否完整、能否停下来衡量，不按行数衡量。

**状态有主人。** F# 对象可以直接持有私有可变集合和跨调用状态。对象自行维护不变量；输入、输出不能以可变别名暴露内部。同步也要考虑重入，异步要考虑旧回执。

**合作另有主人。** 声明独立的 A、B 不互相调用，由 C 分别发消息并处理结果。C 可以有协作状态，但不复制 A、B 的领域判断。给 A 注入 B 的接口、万能发送器，或让 A 返回调用 B 的脚本，都不算把协作移出。

**依赖尽量平。** 数据与必要基础协议可以共享，必须如实声明；不把整个旧 Context、ServiceProvider、Journal 或角色全集装进参数。不为每个函数造一个 C，不为了目录平整制造巨型总控。

**先完整，再加强。** 最简版本必须兑现自己的全部承诺。增强能拆则拆，基础对象不知道增强者；拆除后只保留基础承诺，不能继续宣称持久化、完整输出等增强保证。直接简化内部更清楚时，就直接改。

**验证真实后果。** 发送不等于承接，承接不等于完成，内存 Applied 不等于持久 Committed，信号和超时不等于物理退出。未知不是重新执行的许可。只把纯决策做新旧双算，禁止把真实命令、模型请求、会话创建或 Git 写入双执行来比较。

详细元规则在独立库根 `AGENTS.md`；本蓝图不复制第二套 AXIOM/LEMMA/SPEC，也不以类、接口或可变字段数量评分。

## 4. 认领与公共编辑面

认领写明：卡号、owner、非作者 reviewer、两个 base SHA、两个工作树、分支、准备修改的精确路径、正在等什么。owner 是人或可交接的工作身份，不填“大家”；本文件发布时尚未替任何合作者认领。

协调者先确认没人持有同一编辑面，再把认领合入看板。**未得到已登记的认领，只做只读调查，不同时修改同一个文件。** Git 推送冲突不是抢占锁的机制，后来者不能覆盖先前认领。离线或长时间无回应时，先确认其分支、未提交内容与运行进程，再明确交接；不靠超时自动回收。

细分到文件才能并行：例如 CUT-02 持有进程启动链，CUT-20 持有 PTY 链，但它们不能同时修改共同的 `Process/Surface.fs`。同理，CUT-03、15、16、17 都涉及请求投影，业务代码可并行，最终 hook 接线必须排队。

公共编辑面包括两仓 package/lock/export、主仓 `vendor/`、`compile-order.txt` 与重叠 fsproj、`ToolRegistry`、`PluginTransforms`、`PluginRecoveryWiring`、`Composition/Durable`、共享夹具、根规则与看板。**只预约修改权，不限制其他人阅读和提出反例。** 表见 [BOARD.md](BOARD.md#shared-surfaces)。

## 5. 两仓、两套独立工作树

以下命令是给合作者执行的操作模板，不代表本次已经创建这些分支。先将两个仓库路径填成自己的真实位置；不要把示例路径提交进代码。

```sh
# Set existing repository roots and a unique task name.
export WXS_ROOT="/absolute/path/to/wanxiangshu"
export WXL_ROOT="/absolute/path/to/wanxiangli"
export TASK="CUT-02-your-name"
export WXS_WT="$(dirname "$WXS_ROOT")/wxs-$TASK"
export WXL_WT="$(dirname "$WXL_ROOT")/wxl-$TASK"

git -C "$WXS_ROOT" status --short --branch
git -C "$WXL_ROOT" status --short --branch
git -C "$WXS_ROOT" fetch origin master
git -C "$WXL_ROOT" fetch origin main
git -C "$WXS_ROOT" worktree add -b "cut/$TASK" "$WXS_WT" origin/master
git -C "$WXL_ROOT" worktree add -b "cut/$TASK" "$WXL_WT" origin/main

git -C "$WXS_WT" rev-parse HEAD
git -C "$WXL_WT" rev-parse HEAD
```

先核对 Node/npm 与仓库声明版本，再在每个工作树执行 `npm ci --ignore-scripts`。这个命令会替换该目录 node_modules，不能在别人的工作树或正在验证的目录执行。新库使用 npm Fable；主仓按 README 准备 Fable/Fantomas 所需工具，但两边都不运行 `dotnet build`。

每人独立拥有 node_modules、dist、.build/.fable-build、临时输出、测试数据库与日志。可以共享不可变下载缓存，不能共享活动产物或把 `NODE_PATH` 指向另一个工作树。不要边修改边让别人用你的工作树跑“最终验收”。

**Git worktree 只隔离源码与产物，不隔离 Git common-dir。** 不同 worktree 仍共享 refs、部分配置、hooks 和 common-dir 下的业务数据；本项目 work.sqlite 正位于该处。测试必须使用夹具建立的独立临时 Git 仓和存储，不能拿真实主仓的 `.git/wanxiangshu` 做破坏试验。需要修改 hooks、运行真实插件或验证 Git 集成时，用独立 clone/临时仓和隔离配置，不在活跃开发仓启动。

只改主仓接线、且无需修改库时，不必创建空的库分支；认领中写明使用的已发布库提交及 tarball。禁止在共享 `master`、`main` 或原 lifecycle-cutover 工作树里多人直接编辑。

## 6. 每刀从开始到结束

| 步骤 | 必须留下什么 | 不合格的替代品 |
|---|---|---|
| A. 定义范围 | 一句职责、当前调用者、状态归属、产品条款、候选删除清单 | 只说“拆这个文件夹” |
| B. 固定契约 | 输入/结果/错误/副作用、状态与身份、当前行为差异裁决 | 大 Context、只列方法签名 |
| C. 先找反例 | 一个可能推翻边界的真实失败；已有正确行为则保留正控，不伪造红 | 在测试里自抛异常再 assert.throws |
| D. 独立实现 | 最简对象、局部规则、编号测试、隔离构建与公开产物 | 把旧代码搬到新目录，仍回读旧源码 |
| E. 实际 npm 消费 | 精确库提交、不可变 tarball、摘要与锁定依赖，干净目录导入 | npm link、workspace link、NODE_PATH |
| F. 接回一条真实链 | C 接线、失败/重复/未知/重启用例、现行 WHAT 回归 | 测试单独调用新库，生产仍走旧实现 |
| G. 旧 owner 退出 | 每个旧符号的调用者去向、实现/工程/过期测试清理，历史数据处置 | 同时保留两个可写账本，用同步器维持一致 |
| H. 审查与集成 | 候选与最新主线比较、证据范围、已知不足、回退方案 | 用测试数量或“全绿”一句话代替风险判断 |

“库就绪”可以独立提交，以便其他人消费，但不能把主卡标成“已抽干”。接线若尚有必要旧路径，应明确缩小已完成职责或登记子卡；不能在 DONE 后暗留原职责。

## 7. 契约协作，不把团队锁成一条长队

上游库 owner 负责公开协议，直接调用方负责人参与评审。先记录数据语义、合法状态和失败，再定具体 F#/JavaScript 形状。冻结的是一个明确版本的协议，不是永远不允许修改。

并行开发可以用已经发布的稳定协议，或真实的小型参与者；纯计算可以用数据样本。模拟“兄弟库将依次调用哪些服务”的替身不能证明独立性。尚未存在的外部能力不假造为成功。

公开协议需要变更时，提出受影响卡、版本兼容范围和接线计划。增量可独立合入；破坏性变化要与调用方在同一个集成候选中兑现。不得复用同版本号替换 tarball，不得先推不兼容库再要求全队立即跟着改。

看板的前置是**施工/验收依赖，不是运行时 import 清单**。例如 WorkRecord 需要确认 Trace 区间语义，但可以接收 C 提供的数据，不因此在自身内部调用 Trace。前置库缺席，不影响无关卡准备和独立验证；真实整链验收缺参与者时不能标完成。

## 8. 两仓发布与主仓集成，统一串行

库发布负责人从已审的精确提交建立干净发布工作树，集中处理 package version、exports、files、锁与产物。以下流程没有 `npm publish`；公共 registry 发布不是本计划的默认动作。

```sh
# In the selected library release worktree.
npm ci --ignore-scripts
npm test
npm pack --json
```

记录 npm pack 返回的实际文件名、SHA-256、库 commit 与验证环境。检查包内容白名单、ESM 与 .d.ts 一致，消费者不安装编译器也能使用；支持 Node/Bun 的声明分别验证。扩展能力并不意味着现有 Linux 证据自动覆盖 macOS/Windows。

由主仓集成者复制该唯一产物到 `vendor/` 并更新依赖。`TARBALL` 填实际文件名，`WXL_RELEASE` 指刚才验收的工作树，不能取任意协作者目录的最新构建。

```sh
# In the host integration worktree.
export TARBALL="wanxiangli-<exact-version>.tgz"
export WXL_RELEASE="/absolute/path/to/validated/library/worktree"
cp "$WXL_RELEASE/$TARBALL" "vendor/$TARBALL"
sha256sum "vendor/$TARBALL"
npm install --save-exact --ignore-scripts "./vendor/$TARBALL"
git diff -- package.json package-lock.json
```

核对 `bundleDependencies` 仍包含 wanxiangli；tarball 被 Git 跟踪，锁指向同一文件与完整性值。若被忽略，只允许对这个明确文件执行定点 `git add -f`，不强加整个 vendor。接线作者不要各自制造不同版本的根锁文件。

建议合入顺序：库实现/测试 → 库发布提交与 tarball → 主仓依赖及调用链 → 旧路径退出 → 主仓验收。跨仓没有原子 Git 提交，故两边 PR 互相记录精确 SHA；主仓未合入前，旧 master 仍锁旧包，不受新库 main 变化影响。

同一批多卡进入集成时，先确定发布批次包含哪些库提交；每卡保持独立回退点。合入前再 fetch 并复核最新 master；若前进，重建合并候选并重跑受影响验证，不把原 base 的证据粘到新候选上。

参与者只 push 自己的分支：

```sh
git add <reviewed-paths>
git commit -m "refactor(CUT-02): adopt the independent process owner"
git push -u origin HEAD
```

只有当轮集成者更新主仓 master / 新库 main。无冲突且祖先关系成立可 fast-forward；需要合并时保留合并输入并验收。禁止 force-push 主线、覆盖他人分支、在其他人的未提交树上 rebase 或 reset。共享工作树跨任务默认也不强推；必要 rebase 先交接其消费者。

## 9. 验收：净改进与完整发布不是一回事

本项目已获用户授权：**候选总体质量高于当前 master，可以合入；无需先消灭所有历史 TODO。** 这不是跳过验证，也不是每个失败都能归为历史债务。

每次比较都固定当前 base 与 candidate，记录：实际行为和故障风险是否改善、理解/修改范围是否缩小、外部后果是否保持、旧 owner 是否退出、资源与性能是否有回退。只比较同输入、同环境下的相关数据；无法测量就标未知。不要用“多了几十个测试”“删了多少行”作净改进证明。

必须阻止合入：本卡引入的未解释回归；真实效果可能重复；用户原文丢失；权限扩大；历史被错误读取/覆盖；资源提前释放；新库反向依赖；依赖无法干净安装；相关失败或取消被藏成 TODO/skip。涉及已知范围缩减，须先得到明确产品裁决，不能由卡 owner 自批。

历史无关 TODO/skip 可以保留，但列出原条款和未完成范围。直接涉及本卡承诺的物理证明缺失时，不把本卡标 DONE；可以缩小且明确标示本轮已完成范围。既有失败未完成运行不能伪造通过数；当前 verify 返回 FAIL 就如实记录 FAIL。

通常执行顺序：

```sh
# New library: replace the module name with this card's actual owner.
npm run build -- process
node --test src/process/tests/*.test.mjs
npm test

# Host: in a separate host worktree.
node scripts/build.mjs
node --test requirements/process-execution/tests/*.test.mjs
npm run format:check
npm run check

# Integration candidate: no file filter and no TODO-acceptance override.
env -u TESTS_MJS_FILES -u WXS_ACCEPT_TODO node scripts/verify.mjs
env -u TESTS_MJS_FILES -u WXS_ACCEPT_TODO node requirements/verification-system/tests/integration/run.mjs
node scripts/verify-package.mjs
```

路径和用例集合按卡替换；不存在的模块不运行示例命令来凑证据。全量验证由集成者负责，避免多人在同机反复竞争完整编译与物理套件。真实 Host canary、平台测试及 release 另按原入口准备环境，不假造凭据、不用模拟服务替代它们。

每份证据保留两仓 commit、dirty 状态、Node/npm/Bun/Host 版本、输入指纹、命令、退出码、pass/fail/skip/TODO/cancel/container counts、计划文件与完成文件数、产物摘要、原始日志位置。先跟踪候选新增文件再生成最终指纹：本仓 Git 跟踪的文档会进入 loop detector 语料，新增跟踪文件后旧 manifest 可能失效。不要为了指纹通过手改 manifest。

## 10. 持久数据、在途执行与回退

代码抽取、协议变化、存储迁移是三件不同的事。默认先保持真实行为与既有存储字节，把旧格式限制留在旧侧适配。不能照搬 CUT-01 的“新 work 不翻译旧历史”到其他职责；每张持久化卡单独确认有哪些真实旧数据仍要服务。

接线前把状态分成未开始、已预留未承接、已绑定未确认、活动执行、已终结、损坏/未知六类；记录由谁核对和谁停止准入。迁移不得靠停进程就断言效果没发生。只做影子读取/纯计算，不开第二条真实写入链。

旧格式必须保留时，优先让 C 读取旧 owner 数据并只交普通数据给新对象；新库不导入旧 codec。需要新格式时，写清冻结点、备份、完整性检查、转换、幂等重跑与回退截止点；不覆盖唯一原件，不对未知外部结果强制结算。

回退按失败位置处理：尚未集成可撤回自己分支；已改主线但尚无新数据/外部效果，可提交显式 revert；已有新数据或效果，则先停止新准入、保留绑定和日志，由兼容读取或向前修复接手。**代码 revert 不会撤销外部命令，也不保证旧代码能读新数据库。** 物理回收只针对确切 owner 与实例，不全局杀 Node、不删除共享 `.git/wanxiangshu`。

## 11. 如何保持一张蓝图

开始一天或接手一棒时只做三次核对：看板有没有别人占同一编辑面；上游协议/主线有没有变化；自己的证据还对应当前候选吗。结束时写明刚闭合的行为、现在卡在哪一列、精确下一步和不该动的文件。

新卡不替代历史 GAP。原计划正在施工的相同职责要由原 owner 与新卡 owner 合为一个工作包，旧编号保留为证据链接，不另起一队重复修。没有确认交接，原工作不默认结束。[对应表](COVERAGE.md#previous-plans)

紧急缺陷走短支线：先由该职责 owner 修复，集成者串行合入，其他相关卡更新 base 并复跑相邻反例。不得用“架构迁移中”拒绝维护现有系统。

本蓝图的结束不是“所有待办都没了”，而是 CUT-38 对全部职责逐项交账：已抽取并删除旧 owner、明确保留、经批准删除、仍未闭合。保留与未闭合都公开列出，不包装成全面重写完成。

## 12. 本次蓝图交付的证明范围

建板时核对了 39 张主卡与前置关系、全部 53 个当前 WHAT 需求包、21 个生产顶层源码区域、独立库已实现/草稿目录、相对文档链接与显式锚点；依赖图无环，卡号不重复，现有区域无未分配主责。核对的是目录级覆盖和施工可追溯性，不是全部实现已符合新架构，也不是任一未来切刀已完成。

本次只建立文档与多人协作入口，没有执行这些切刀、创建任务分支、认领人员或更改生产协议。所有未来库/接线的验证仍由实际卡按其输入取得，不能引用本段作为实现证明。
