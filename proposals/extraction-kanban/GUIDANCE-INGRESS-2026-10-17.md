# CUT-17-R1：指导呈现与 Host 原件入口

输入干净主仓 `2aaaf60772d6b9ea38ba88ac172a3ef8bc3ae232`，库 `10bbb3247853653543d26eb269ac49008e1b5623 / 0.6.4`。按[核查计划](REMAINING-REPAIRS-2026-10-16.md#guidance-followup)执行；R12-U1继续暂缓。

## 依据与边界

WHAT[prefix-stability-014]要求指导只参与 provider 呈现，不能进入 XTrace、Blogger、WorkRecord 或 compaction。原件入口仍须保存业务原字节，不能按正文、NUL+BOM 或指导文本猜测来源后删除。

基线 `TMPDIR=/private/tmp node --test requirements/prefix-stability/tests/014.test.mjs` exit 0，其中直接 wire→Trace 的 TODO 仍执行失败：保存了 `file content\r\n\0\uFEFFa unique guidance sentence`。日志 `/private/tmp/g108-baseline-2aaaf6077.log`。该用例绕过 Plugin 的来源处理，不单独证明正常 Host 可达。

当前管道先通过 `canonicalCaptureMessages` 捕获，后执行指导呈现；工具结果注入复制 message/parts/state，不修改原始 Host 对象。仍须实跑后续请求、工具完成和快照补捕获，核对普通引用、重复消息与独立进程冷读，不能只凭调用顺序验收。

## 工作进度

| 工作包 | 状态 | 验收目标 |
|---|---|---|
| 真实请求与入口盘点 | 已证明所列路径 | 实际 OpenCode Host、模拟 provider 网络请求、Host 持久消息及 Trace 逐项对照；指导不回写工具原件 |
| 反例与修复 | 完成 | 用户带完整包装的指导引用确有误删；来源证明进入呈现层，Trace 保真规则未放宽 |
| 回归与冷恢复 | 完成 | Node/Bun核心各12 pass/0 fail/1 integration skip；实际Host与独立冷恢复1/1；三组1350项0普通失败，独立Trace正反编译1/1 |
| 交付 | 随本原子提交交付 | 精确包与隔离Node/Bun实际消费通过；文档、看板和剩余计划同步更新，遵守 AGENTS.md 正常提交推送 master |

## 问题与归因

| 编号 | 性质 | 证据与处理 |
|---|---|---|
| G108-01 | 原 review 反例层次不符 | 将呈现 wire 直接交给无来源的 Trace，再要求 Trace 删除后缀，违反原件保真。该输入确实保留全部字节，但未证明正常 Host 污染。现改为保真负控，以注册 Plugin 和真实 Host 的正反链路承接 WHAT-014 |
| G108-02 / P1 | 输入已有的呈现误删 | 原 `stripCapturedResult` 对用户消息绕过原文证明，直接按所有旧 marker 做 Replace。实际 Host 中4201字符的普通引用在 provider 请求中只剩2107字符；持久用户消息与Trace仍完整。`/private/tmp/g108-host-probe-baseline2.log`、`/private/tmp/g108-plugin-probe-baseline2.log`。正式两项引用反例先红：`/private/tmp/g108-formal-red2.log` |
| G108-E1 | 本轮测试身份错误 | 初稿几个隔离工作区共用同一 session 身份，命中进程级准入记录，未触达引用断言。现每个场景使用独立身份；`/private/tmp/g108-formal-red.log` 不算产品失败 |
| G108-E2 | 本轮测试组合错误 | 把最终 provider wire（含无物理地址的展示占位消息）作为新 Host 原始请求重送，等待未完成；再误用不同 opaque Journal wrapper、局部 import，一度未进入呈现器。现由原始物理消息重建真实后续请求，另以该 owner 自己的公开 journal 入口验证确切 carrier 的冷重放；没有增加测试 facade 或放宽准入 |
| G108-E3 | 本轮探针环境与 mock 错误 | 首次模拟 provider 把标题请求当作 read 请求，未发生工具执行；首轮裸 Bun 测试的 homedir 指向启动时的用户目录，sandbox 拒绝写入。分别区分带 tools 的请求、在独立 HOME 启动 Bun，不修改用户配置或产品规则；失败日志保留 |
| G108-E4 | 既有共享夹具的目录别名问题，已修正 | 默认临时目录是 `/var/folders/...`，Git返回 `/private/var/folders/...`，fixture却把两者当不同工作根。旧精确包的C36/grounding代表例同样失败；统一fixture传出的真实路径后，完整相关组转绿。只改测试入口，不放宽生产根目录校验 |
| G108-E5 | 既有歧义夹具已落后于T-01，已修正 | 旧测试要求新捕获接受同一无part call的两份不同正文，已被T-01在写入前拒绝。旧精确包同样在此断言失败。现在先验证新捕获拒绝且零追加，再经公开fact入口构造明确的旧历史格式，原来两个呈现owner的歧义拒绝断言全部保留 |
| G108-E6 | 既有单测前置状态缺失，已修正 | 单选C36时没有配置Predictor，却要求恢复只在配置后保存的字段；旧精确包同样失败。该测试现在显式设置并恢复自己的配置；新旧包单选均1/1，完整032仍61 pass/0 fail/1 skip/1 TODO |

不将 G108-01 写成已确认的正常 Host 原件污染，也不将 G108-02 归给本轮修复或原模块拆分；它已存在于输入 `2aaaf6077`。本轮未改库包、持久事实或历史原件。

## 来源与职责

实际请求顺序为物理 Host 消息→普通 Plugin 管道的 canonical capture→Trace/Companion→XWire/指导/grounding 呈现→provider。工具完成事件和 `session.messages` 补捕获读取原始 Host 状态；指导注入复制 message/parts/state，Host 数据库没有这些附加字节。真实 Host 后续请求重新读取持久消息，不拿上轮 provider wire 当新原件。

工具结果原有的原文证明继续使用；用户 carrier 现在同样按 message、物理 part（缺失时仅用已记录的位置身份）查询 Trace 原文，再以该位置 durable occurrence 的完整附加字节做精确匹配。匹配才剥离对应 owner 的呈现；字面引用保持原文。多候选身份拒绝为歧义，不按文字猜胜者。普通无后缀用户材料不额外查询该证明。

Trace 仅新增原文查询，不接收指导文本、不保存第二份正文或 occurrence 账本；临时呈现映射不跨请求保留。旧缺来源历史不补造身份，已错写的旧事实不在本包自动迁移。完整 CUT-17 抽取、GAP-101/109 和 R12-U1 不借本包关闭。

## 当前验收证据

- 生产Fable587，收紧查询条件后588；均174 surfaces / 839 modules。`/private/tmp/g108-build-{first,second}.log`，未使用 dotnet build。
- 正式核心 `/private/tmp/g108-core-node-final.log`：13项 / 12 pass / 0 fail / 1 integration skip / 0 TODO；原件、快照和独立进程对照均为实际公开 Surface。旧直接捕获TODO已用保真负控及实际入口证明替换。
- 正式真实Host `/private/tmp/g108-host-integration-first.log`：`WXS_TIER_INTEGRATION=1` 按 `actual OpenCode Host` 名称执行1/1。运行原生 `opencode serve`、真实 read、本地模拟provider；完整引用进入网络请求，后续请求保持旧呈现，Host持久消息不变，Host退出后的新进程冷读保留原文。
- 独立实际Host探针修前 `preserved=false`，修后 `preserved=true`，日志 `/private/tmp/g108-host-probe-{baseline2,first-green}.log`。正式用例另核对网络与持久/冷恢复，不以此探针代替全部证据。
- 三组不重叠的正式默认组共 **1350项 / 1299 pass / 0普通fail / 12 skip / 39 TODO**。呈现/prefix/grounding/provider：412/392/0/4/16；Trace/WorkRecord/压缩：657/633/0/4/20；Host边界：281/274/0/4/3。命令为 `WXS_ACCEPT_TODO=1 TESTS_MJS_FILES="<对应目录编号测试的逗号列表>" node requirements/verification-system/tests/run.mjs`，日志 `/private/tmp/g108-{presentation,trace,host-boundary}-verified.log`，144个计划文件全部完成，三个监管收尾均 accepted=true。TODO/skip不计通过。
- C36补齐自己的配置后，完整032为63项/61 pass/0 fail/1 skip/1 TODO，另新旧包独立单选各1/1；`/private/tmp/g108-host-032-final.log`、`/private/tmp/g108-{old-,}c36-isolation-fixed.log`。它们替换上述Host组相应用例的验证，不重复相加。Trace opaque consumer独立正反Fable编译1/1，`/private/tmp/g108-opaque-compile-final.log`。
- 旧包对照使用原229980精确包，SHA-256 `22998049cad15b0b4cf580c87def35f95b0656b55835fc775d4e3607f7f4176c`。相同五个既有测试文件在明确私有TMPDIR下为153项/148 pass/1 fail/3 skip/1 TODO，唯一普通失败是旧歧义夹具；默认TMPDIR的根目录代表例2/2失败。夹具修后旧包对应正控通过。日志 `/private/tmp/g108-old-package-adjacent.log`、`/private/tmp/g108-old-default-temp-root.log`、`/private/tmp/g108-old-fixtures-fixed.log`。此对照借主仓runtime依赖，不冒充隔离安装；新旧dist只有本轮Materialization、PairProgrammingThoughtTransform两个JS文件不同。
- 原失败组日志 `/private/tmp/g108-{presentation,host-boundary}-final.log` 保留，不算通过；错误接缝、opaque wrapper、局部import和最终wire重送的日志 `/private/tmp/g108-core-node-{second,third,fourth,fifth}.log` 保留，不归给产品。本轮没有用skip或放宽断言消除失败。
- 精确主包generation590：**2076文件 / 13,375,004字节**，SHA-256 `f047aa1c23adfe1a9602f64aae05e59b50c55b6f90bd7a503da020557d1ae70b`。真实pack、独立安装及默认入口通过，`/private/tmp/g108-package-final.log`。同一包另做隔离安装，逐个核对全部文件；Node26.6.0/Bun1.3.14实际捕获→呈现→新journal重开→引用与重复呈现均通过，使用包内wanxiangli0.6.4，不借主仓依赖。包和安装文件前后不变，临时目录已清理，`/private/tmp/g108-isolated-package-final.log`。
- 最终完整静态门禁和格式检查通过：`/private/tmp/g108-check-delivery.log`、`/private/tmp/g108-format-final.log`；JS边界无违规，不增加豁免。收尾文档/测试仅刷新元数据，生产字节仍为588；完整包闭包再次核对为2076文件零差异，`/private/tmp/g108-final-package-closure.log`，旧包解压目录已清理。

## 交付边界

CUT-17-R1按所列正常Host入口、指导引用保真及冷恢复证据记DONE，GAP-108的包内014独立oracle与真实Host证明已落地。实际Host为安装的OpenCode1.18.29，版本日志 `/private/tmp/g108-host-version.log`；本地模拟provider只控制响应，不替代真实Host、read或Plugin管道。没有非作者复审，本轮为实现与验收，不能把独立OS进程证据称作非作者review。

下一包按原计划处理CUT-02-R6-B/GAP-092完整wire预算，其后R6-C、R6-A。旧无来源/错写历史不自动迁移；GAP-101/109、guidance的其余GAP-115/116、CUT-17整卡和R12-U1保持各自边界。本包库0.6.4未改，不强推master，未部署用户运行中的Host。
