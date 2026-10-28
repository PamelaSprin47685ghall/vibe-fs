# cognitive-workspace — WHAT

本包划定 `assume` 工具与认知状态的边界：系统里「笃定」只是一个动作承诺点，
不携带画板、待办或压缩状态。待办归编程面工具，上下文压缩归 `resetScope` 屏障。

## [001] 无 session 级持久认知画板

系统的持久状态里没有 session 级 JSON 认知画板这一形态。Life、重启、deliver、
退休等生命周期只携带 durable facts 投影，任何组件都不得引入或恢复画板形态的
session 状态。

## [002] assume 只接受 assumption

`assume` 的参数表只有 `assumption: string` 一个字段。调用方传入其他字段
（`update`、`query`、`todos`、selector、vars 或任何 jq 参数）即为非法调用。

## [003] 历史 Cognition journal 只读

历史 journal 中已存在的 `AgentFact.Cognition/AssumePhaseCommitted` 可被兼容
decoder 读取；fold 对它一律 no-op，不生成 projection、不开新写入口。
读取只为诚实恢复旧数据。

## [004] durable projection 不含 Cognition 字段

`AgentProjectionSet` 只保存会话、任务、关联等结构事实，没有 `Cognition`、
canvas snapshot、owner ordinal 或同义字段。

## [005] 编译图与依赖保持无画板运行时

生产编译图不含 `Participant/Cognition` 的 runtime、workspace、codec、admission
或 fold；包依赖中不含 `jq-wasm`。需要新增认知类状态时，走 durable facts，
不另造画板运行时。

## [006] 压缩边界唯一由 resetScope 划定

`assume` 成功不产生 compression 边界。上下文压缩边界唯一由显式 `resetScope`
宿主工具调用划定（见 context-compression [033] 与 obligation-ledger [001]）。

## [007] 待办唯一写入面是 todowrite-for-<role>

宿主待办写入只经编程面 `todowrite-for-<role>`，插件不维护第二份待办真相，
也不提供任何待办 UI 写入桥。

## [008] 系统不执行 jq

任何 provider 输入都不会在本系统内作为 jq 程序执行；工具实现与包依赖中
都不引入 jq runtime、jq 编译错误分支或 jq 输出数量协议。

## [009] assume 是无状态动作

`assume` 的结果只取决于本次合法调用与 provider 语言；它不读取前一次 assume、
session 私有记忆或跨实例缓存，也不留下可变状态。

## [010] 笃定不等于事实、权限或完成

`assume` 只表示「当前判断已经完成抽象并准备据此行动」。它不证明判断为真、
不授予角色权限、不证明测试通过，也不构成完成或退休证据。
