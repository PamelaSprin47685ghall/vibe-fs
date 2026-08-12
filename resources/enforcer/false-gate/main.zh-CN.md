# false-gate — Main

## 现在该做什么
让这个 gate 先证明：它真的有能力拒绝自己声称要禁止的东西。

在 advertised scope 内放入一个最小 known-bad fixture，然后走正式入口。持续修复 discovery、matching、assertion、exit propagation 与 CI wiring，直到这个 fixture 真的把 gate 打红。

之后可以移除或隔离 fixture，但必须保留能够证明 guard 仍有牙齿的 self-test。

## 为什么重要
团队并不是只“看一下 gate 输出”。一旦一个 check 被命名为 required gate，人类就会合理地把一部分 vigilance **委托给它**，不再每次手工重证那个 property。

这种委托只有在 green 真有含义时才安全。

所以 false gate 不是“弱一点的 test”，而是一个坏掉的组织契约：它告诉所有人“你可以信任这里”，机器却没有能力赢得这种信任。CI badge 越漂亮、脚本名字越权威、覆盖面越号称“全仓”，这个谎言越危险。

## 修复策略
沿着“property → pipeline”完整追踪：

- **subject discovery**：证明真正应该被检查的文件/案例确实被枚举；
- **detector**：证明 known violation 会被识别；
- **failure semantics**：证明识别之后产生非零/拒绝状态；
- **wrapper**：证明 shell、npm、task runner 不会吞掉这个状态；
- **pipeline**：证明 CI 最终真的红；
- **scope drift**：证明未来目录、扩展名或生成方式改变时，扫描集合不会悄悄缩成 0 而无人发现。

只有当“空 scope 本身就是配置错误”时才 fail closed。不要机械规定所有 empty set 都失败；property 自己决定什么叫错误。

## 决策分支
- **0 个 subject，但按契约本应存在：**修 discovery，并加 sentinel assertion 证明 expected scope 非空。
- **发现了 violation，却没被 classify：**修 detector 或 rule boundary。
- **本地 detector 红，CI 仍绿：**修 status propagation，不要乱改 detection logic。
- **baseline / exception 吞掉新增 violation：**让 admission 显式、可审查；否则就承认这个 check 只是 advisory。不要自动 grandfather debt 还继续叫 enforcement。
- **本来就只想做 advisory：**改名、改文档、改 CI 呈现，让任何人都不会把 green 误读成 compliance guarantee。

## 常见假修复
- 加更多 logging。更详细地描述失效过程，不会创造 enforcement。
- 只把 glob 调宽到“今天能扫到文件”，却没有 bad fixture 防止明天再漂移。
- 加 `|| true`、`continue-on-error` 或“临时” soft-fail wrapper。临时假 gate 往往最永久。
- 只通过内部 helper 测 detector，而生产入口走的是另一套 wrapper。
- 只断言“扫描到了文件”。扫对文件不等于能识别 forbidden state。
- 每次自动重生成 baseline，然后把消失的 delta 叫作 ratchet。那不是 ratchet，是 debt laundering。

## 验证
必须通过**同一个标准入口**证明两个方向：

- known bad → red；
- known valid → green。

条件允许时，再故意破坏关键链路：错误 path、detector violation、child non-zero status。Pipeline 不能把这些静默洗成 green。

Invariant：

> Green 有意义，是因为相关 red state 已经沿同一条路径被真实证明过。

## 完成条件
一个普通 maintainer 能回答“什么具体 defect 会把这个 gate 打红？”，并能指向 committed example，而正式 local/CI command 可以亲自证明这个答案。

在那之前，别叫它 gate。
