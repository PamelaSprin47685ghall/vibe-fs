# host-boundary 测试说明

[WHAT](../WHAT.md) 是规则来源。当前测试中的模拟端口、参数与辅助模块是证明设施，不是另一份产品合同。

## 三类证据

1. 信号解码、身份唯一性、类型化错误、快照决策、顺序转换和订阅释放的用例驱动生产 Surface，提供具体合同证据。
2. 026/027/029 等用例检查编译依赖与能力分界；部分源码/项目名称锁定仍待进一步整理，不能用它们代替运行行为或真实 Host 能力。
3. 真实 Host canary 需要实际启动受支持 Host，通过公开 Hook/SDK 完成场景。019 的受控端口只证明生产 transform 的条件分支，不能代替该物理证据；全能力覆盖仍是 TODO。

2026-09-26旧基线曾执行023的独立真实Host canary。该历史结果不作为当前上游基线验收；它也不等于完整项目插件的 Engineer 会话、只读工具和取消现场巡检。

## 本轮调整

- 015 增强工具注册到输出的完整断言，验证多字节文本、完整尾行、超大单行 Unicode 后缀、字节上限及重复限界稳定性。固定预算数值是当前兼容对象的设施配置，测试不替代真实 Host 版本能力核验。
- 019 清退已删除 MagicTodo 协议及源码词形证明。快照定位的有效案例归012；实际 transform 分支通过只转换参数、记录端口调用的生产接缝验证，不再从JS拼装Fable内部对象。Replica真实物理路线仍须019/Long Stroke证据，未用手填runtime Map充数。
- 026 新增真实 Fable 编译诊断分片的用例，先暴露缺少 failure decision 依赖的错误，再补齐该引用。全量构建恰好包含类型定义，并不能证明声明的局部依赖完整。
- 029 由真实子进程调用终止/诊断入口。报告器抛错时，测试安装未捕获异常观察器区分“异常退出”和“确实触发终止”，不能把任意非零退出当作硬退出。持久事实由同一个随后被终止的子进程写入，父进程重开核验；不再用父进程预先写好的文件冒充子进程崩溃证据。
- 029 原来的正常拒绝、stale callback、耗尽模式只打印成功后退出，完全没调用业务路径。已删除这些假证明并保留 TODO；相应产品义务没有删除。真实流程的正常退出证据仍需补齐。
- 032 的 C15 精确检查评审字段首/中/尾的原始键序；C44 经注册 hook 检查只读估计字段与评审字段交错时的完整值、对象身份、原键序、重复 after 及跨 session 同 callID 隔离。C45 检查无法恢复键序的参数在隐藏前原子拒绝，并允许位于协议字段之前的不可配置业务键；C46 触发实际 before 隐藏失败，要求已经隐藏的评审字段同源恢复。C47 在已删除估计字段后让短记字段的首次删除抛出异常，要求两个协议族全部恢复且重抛同一 Error；这是可恢复的一次删除故障，不宣称任意 Proxy 都能回滚。C11 只证明携带已报告错误的 after 回调，C12 只证明正常 after 返回；不再用测试自抛再手动 after 冒充真实执行异常传播。安装版 Host 执行器抛错后自动恢复仍单列 TODO，不以私有 Symbol 名或固定 schema 装饰格式代替合同。
- 032 的 C48–C52 检查 configured 状态下重复 before 的边界：仅相同参数对象及完整 session/call/tool 身份可沿用已隐藏参数；不同身份、不同对象、缺失身份及协议字段重现都不豁免校验，合法的数值 0 重现也须拒绝。非评审工具自有的 contract 保持原对象；评审工具重加 contract 不能冒充重复回调。私有暂存所有权仅服务参数恢复，不授予业务准入。
- 032 的 C53 同时启动两个共用参数对象的不同调用，要求仅一个取得暂存所有权；败方不得恢复胜方仍在使用的参数。获胜调用的重复 before 与最终 after 继续验证同源恢复，不靠定时等待或重跑安排交错。
- 032 的 C54 覆盖未配置 Predictor 的评审工具及已配置时的评审/read：参数对象完成 A 调用后被 B 复用，迟到的 A after 不得恢复 B 正在使用的参数；input/output 两侧参数、未知调用及缺少 session/call 身份分别检查，只有 B after 可以完整恢复。C55 用独立的 throwing getter 检查 join 的同名估计字段及 read 自有 contract 不被协议暂存读取，并核对 before/after 的对象身份、描述符和键序；测试自身也不读取这些 getter。
- C52 已在候选编译产物中复现评审字段重现后恢复到错误键位（`/tmp/vibe-fs-argument-owner-batch3.log`，4 通过、1 失败）；C53 已复现两个不同调用同时取得同一参数对象（`/tmp/vibe-fs-argument-owner-race-red.log`，两个 before 均成功）。这些日志保留为修复前的失败证据。
- C54/C55 的六个独立用例已取得修复前失败证据（`/tmp/vibe-fs-argument-after-business-red.log`，0 通过、6 失败）：三个 after 所有权反例与三个非协议业务 getter 读取反例分别失败。最终候选已完成 Fable 822 模块编译链接，新增 Host 回归 16 通过、0 失败（`/tmp/vibe-fs-host-final-regressions.log`），覆盖共同调用身份、参数恢复和按字段所有权读取；该结果不替代安装版 Host 执行异常后自动调用 after 的待证义务。
- 032真实canary不再把缺失SDK观察默认为成功，不伪造版本、完成状态和取消结论。观察器保存原始参数副本及键序，实际after验证恢复；公开事件唤醒完整SDK读取后确认终态和持久输入。目标版本fixture只声明预期环境，不是通过记录。本轮已启用 integration 并通过该真实 Host canary；所在五文件验证共 103 通过、0 失败、0 跳过、5 TODO（`/tmp/vibe-fs-resume-focused-integration.log`）。这证明已执行的正常恢复场景，安装版 Host 执行器抛错后自动调用 after 仍单列待证。

03 对内部测试入口的限制与本包 029 的物理终止证明方式仍需统一裁决。现有物理 fixture 保留，不新增扫描豁免。

## 运行与限制

先执行 `node scripts/build.mjs`，再运行本包正式用例：

```sh
TESTS_MJS_FILES="$(rg --files requirements/host-boundary/tests | rg '/[0-9]{3}\.test\.mjs$' | sort | paste -sd, -)" node requirements/verification-system/tests/run.mjs
```

019/029/032 的 TODO 阻断完整验收。013旧基线曾出现断言后不退出，未取得本基线闭合证据。023与032需要真实Host及本机端口权限；026真实编译用例需开启integration。本轮主工作区 Fable 构建及 822 模块链接已通过（`/tmp/vibe-fs-resume-main-build.log`），完整 verification harness 为 278 通过、0 失败（`/tmp/vibe-fs-resume-harness-final.log`）。这些结果不消除上述 TODO；完整执行范围见[本轮记录](../../../proposals/20模块上游适配记录-2026-09-28.md)，残余扫描、跨包案例和物理证明缺口见[GAP](../../GAP.md)。
