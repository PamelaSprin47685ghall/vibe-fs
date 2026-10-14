# Lifecycle cutover 收尾验收

## 判定

先前复现的三个 lifecycle 缺陷已修复；继续检查发现的五个集成失败、安装包版本断言漂移和嵌套验收输出损坏也已有修复及回归证据。完整接受仍未通过，不能合入 master。未降低 TODO/skip 门禁，没有删除或跳过失败用例来换取绿色结果。

工作基点为 `feat/lifecycle-cutover` 的 `cf49c32d9cf3fb15ed8c9d8dc662a44ccfca1ed0`。本轮只在 cutover 工作区施工并向该分支交付；master 指针 `fc87bc4632b220fb770b32a69cdbdc951d676190` 未修改，原 master checkout 未修改。`wanxiangli` 独立仓为 `46d9c07`、版本 0.5.2，本轮没有修改其源码。

## 已闭合的行为缺陷

### 一次投影只描述一个 revision

`DurableWorkHost.inspectedJournal` 只向 durable owner 读取一次 records，再以纯 Work 从同一批数据生成 snapshot 与 history。不会在两次存储读取之间混入并发提交。

`work-033` 用第二条真实 SQLite 连接在读取间提交接力：旧 active/rounds 必须与旧 retirements 一致，下一次读取才看到新任。原失败用例通过，未增加跨库锁或第二份可写投影。

### DevOps 身份不再冒充模型目标

宿主分别暴露 DevOpsId 与 DevOpsTarget。目标从同一批已验证历史的 DevOpsBound 事实取得；恢复路由只使用 DevOpsTarget，不再把身份字符串交给模型路由器。`work-033` 检查两个字段确实不同。

### 单个坏 work 不阻断其他 work

首次读历史与整个异步恢复过程都有逐 work 的失败边界。诊断保留 workId 和原因；原记录不修改，业务正常读取仍拒绝损坏历史。其他独立 work 继续恢复。

`work-048` 包含两种真实反例：启动前已有坏记录；初次读取正常，但等待 Host 消息时被另一连接追加非法记录。两者均不能阻止另一项有完整证据的交付结算。后一个反例先观察到失败，再加异步边界使其通过。

### 交付、资源和发现边界

历史测试入口现在真实经过 DeliveryRequested → DeliverySessionBound → Accepted retirement，不能绕过库的 session 绑定检查。正常交付在首 prompt 返回后重新读取资源，恢复同样使用已安装的资源 owner；阻塞期间保留 pending，不消耗固定 Accepted 请求身份，排空后结算同一 operation。

发现请求最多取两条候选，满页不能证明唯一。缺身份、错误 agent、错误响应、非列表或模糊匹配不得过滤成“唯一执行者”。`work-039`、`work-048` 的正反例经注册 Surface 返回普通 JavaScript 数据，不依赖 F# union 的内部布局。

Manager 的 opening root 来自 Git 确认的实际工作树根，不来自插件进入时的任意子目录。共享工作树的双实例测试已通过；真正的 root/sequence 冲突仍拒绝。

## 集成和交付残留

独立编译发现 authority 推进还先获取旧 WorkspaceEventStore，但该对象未被使用。删除这项前置调用及无用的获取函数，并移除 Manager 文件中未使用的旧 OpenCode 导入；没有把旧依赖补回工程声明。

这消除了 `durable-events-022` 的三个独立编译失败。原来的正向闭包编译与反向依赖拒绝保持不变。

另外两个集成失败来自旧断言：工具清单把共享 deliver 列了两次；checkpoint 测试仍按旧生命周期投影要求至少十五个 Journal 切片。现在要求工具清单无重复，逐项精确核对十四个现有切片，并另外核对独立 lifecycle work 完全未变。没有仅调低数量阈值。

真实安装包消费者曾硬编码依赖版本 0.5.1，而包内已经是 0.5.2。现在以根锁文件的精确版本验证被测包内依赖，缺少锁定版本直接拒绝。包内实际版本不是自己的预期值来源。

## 验收器输出边界

一次完整复跑出现 `Unable to deserialize cloned data`，四个用例未完成。没有把该轮计为完整通过。

新增真实嵌套 node:test 反例，让子进程输出类似 V8 协议头的二进制字节，稳定复现同一错误。监督器原来继承 stdout/stderr，允许子输出直接进入外层测试协议流。现在以管道接收，经增量 UTF-8 解码后由父进程转发；IPC 仍是唯一权威结果通道。

反例由红转绿；合法跨块中文和 emoji 保留。非法 UTF-8 可显示替换字符，诊断文本不冒充二进制存档。`verification-system-021` 全部 19 项通过，最终全量复跑无失败容器。没有改变 verdict、TODO、skip、取消或文件收尾的判定规则。

## 验证证据

环境为 Linux、Node 25.9.0、npm 11.12.1、Bun 1.3.14。通过调整本次命令的 PATH 选择仓库声明的 npm，未修改全局工具链。所有完整运行均未设置测试文件过滤或 TODO 接受开关。

| 验证 | 已取得结果 |
|---|---|
| 独立 wanxiangli `npm test` | 276 通过，0 失败、0 skip、0 TODO |
| 最终固定 verify：格式、静态门禁、Fable 构建 | 均通过 |
| 最终固定 verify：unit | 5039 通过、0 失败，832/832 文件完成，无失败容器；129 skip、356 TODO 使完整接受失败 |
| 最终候选完整 integration suites | 767 通过、0 失败，44/44 文件完成；21 skip、14 TODO，仍拒绝完整接受 |
| integration 的 package 子组 | 3 通过、0 失败，1 既有 TODO，仍拒绝完整接受 |
| integration 的 harness 子组 | 290 通过、0 失败 |
| 最终候选实际 pack/extract/isolated consumer | 通过；generation 51，2069 个成员、12,980,151 解包字节 |

上表各项均已取得最终回执。verify 在 unit 的待证明门禁处停止，因此 integration 与 pack 另行完整执行，不能据此把整个 verify 标成通过。不同层的用例有重叠，不将这些数字加成一个覆盖率。

实际产物为 `.fable-build/verify-logs/package/wanxiangshu-0.9.0.tgz`，SHA-256 为 `92e5a4159c4d43744ca03d5182e3d7863aa67c3baf84fd1824790c7b487f63b5`。收尾构建新鲜度检查通过，generation 51 的 1813 个源输入、891 个构建产物与工作区一致。

固定验收日志：`.fable-build/verify-logs/2026-10-14T23-26-53-455Z/`。

完整命令：

```sh
env -u TESTS_MJS_FILES -u WXS_ACCEPT_TODO node scripts/verify.mjs
env -u TESTS_MJS_FILES -u WXS_ACCEPT_TODO node requirements/verification-system/tests/integration/run.mjs
node scripts/verify-package.mjs
```

本机原始输出：

```text
/tmp/lifecycle-cutover-closeout-verify-final.log
/tmp/lifecycle-cutover-closeout-integration-final.log
/tmp/lifecycle-cutover-closeout-package-final.log
/tmp/lifecycle-cutover-final-library.log
/tmp/lifecycle-cutover-nested-output-regression.log
```

## 合并边界

没有新增或扩大待办接受名单。完整 verify 仍因 356 TODO、129 skip 失败；单独执行 integration 也仍有未完成证明。特别是 `work-011` 的真实在途 Host 中断与后继派发顺序、完整 release/Long Stroke 及相关平台证明，不能用普通单元通过替代。

本轮只消除已复现的缺陷并交付可回归的修补，不宣称全部 TODO/GAP 或 cutover 最终验收完成。不执行 master merge。
