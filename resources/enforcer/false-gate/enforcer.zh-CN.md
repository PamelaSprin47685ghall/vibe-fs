# false-gate — Enforcer

## 定义
当一个 gate 的“绿色”状态，与它声称要禁止的 defect **可以同时成立**，这个 gate 就是假的。

真正坏掉的不是某一条 assertion，而是下面这条逻辑关系：

> gate 绿色 ⇒ 被保护的 property 成立

只要左边可以为真、右边仍然为假，这个 gate 就只是仪式。假 gate 甚至比没有 gate 更危险，因为“没有证据”会被它包装成“组织已经替你检查过”。

## 支配原则
一个 guard 只有在证明“真实可达的 violation 会把它打红”之后，才配得到信任。

现实中的假绿往往极其朴素：glob 一个文件都没匹配到；grep 还在扫旧目录；command 打印了错误却 exit 0；wrapper 丢了 child status；test 只证明 setup 跑过；baseline 自动把新增违规吸收掉；一个永远只打印 warning 的脚本，却被 CI badge 和文档叫做“gate”。

自动化让这种病尤其隐蔽。绿色 badge 看起来像 authority，即使底下其实什么都没检查。

## 何时触发
只要存在普通、可达的路径，使一个明确属于 advertised scope 的已知 violation 仍能通过标准 gate，就触发。典型包括：

- subject discovery 可以得到 0 个相关对象，却仍成功；
- detector 扫的是 stale、错误、generated 或不完整 surface；
- 发现 failure 后仍返回成功 exit status；
- CI / shell / npm wrapper 吞掉或覆盖了非零状态；
- assertion 是 tautology，或只证明 test harness 自己运行过；
- exclusion / baseline 让新违规自动消失，却没有显式、可审查的 admission；
- check 实际只是 advisory，却在使用语义上被宣传成 enforcement。

## 不应触发
- Check 明确只是 advisory，且没有人把 green 当作 compliance proof。
- 已有 known-bad fixture 通过**同一个生产入口**证明该 violation 会稳定打红，并且当前 scope 仍被那个入口覆盖。
- Gate 确实能针对目标 property 失败，只是 test proposition 太弱；这通常是 `coverage-theater`。
- Detector 已正确失败，只是 caller 后来无视失败；此时 `tool-error-ignored` 更准确。

## 与相邻规则区分
`coverage-theater` 是“仪器活着，但它测的问题太贫乏”。`false-gate` 是“green 与 advertised property 根本没有可靠蕴含关系”。

`missing-architecture-gate` 是没有 guard；本规则更危险，因为 guard 已经存在，人们因此停止人工警惕。

Tie-break 只问一句：把一个 known violation 放进它声称覆盖的范围，走标准入口，是否仍可能 green？如果可以，就是本规则。

## 判定程序
种下一个最小、代表性的 known violation。不要绕过生产路径直接调用 detector helper；必须使用人和 CI 真正信任的入口。

逐层检查：

1. discovery 找到了 subject；
2. detection 识别了 violation；
3. detector 返回 failure；
4. wrapper 保留 failure；
5. pipeline 真的变红。

任意一环断裂，都意味着这个 property 没有被 gate 住。

## 例子
- positive：项目已经迁到 `packages/*/src`，但 `npm run check` 还扫 `src/**/*.ts`；0 个文件，CI 仍绿色。
- positive：脚本打印 `FAIL: forbidden import`，但最终从未把 failure count 变成非零 exit。
- positive：baseline 每次运行自动重生成，所以所有新 debt 都瞬间“合法化”。
- near-miss：复杂度报告永远 exit 0，但从名字、文档到 CI 都始终明确它只是 advisory。
- counterexample：启用 committed bad fixture 后，同一个正式 CI 命令稳定打红。

## Nudge
不要问 gate 有没有运行。

问 defect 有没有能力让它失败。

一个从未证明自己会红的绿灯，只是带权限的装饰。
