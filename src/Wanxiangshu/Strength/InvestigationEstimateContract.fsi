namespace Wanxiangshu.Strength

/// P2 共享输入合同：推测性调查只读轮次估计与短记成对规范。
/// 纯逻辑契约，不依赖宿主环境、不读配置、不写事件存储。
module InvestigationEstimateContract =

    /// 新输入字段名常量
    val EstimatedReadonlyRoundsField: string

    /// 稳定协议版本常量（现行版本 1 的后续稳定演进）
    val ProtocolRevision: int

    /// 逐工具判定策略
    [<RequireQualifiedAccess>]
    type InvestigationToolPolicy =
        | EstimateAfterCall
        | NoEstimate
        | Unreviewed

    /// 逐工具判定：固定已知名称逐项匹配，禁止前缀匹配
    val classifyTool: toolName: string -> InvestigationToolPolicy

    /// 严格只读轮次估计值类型（[0, 2147483647] 非负整数）
    [<Struct>]
    type EstimatedReadonlyRounds = private EstimatedReadonlyRounds of int

    module EstimatedReadonlyRounds =
        val value: rounds: EstimatedReadonlyRounds -> int
        val toExecutionBudget: rounds: EstimatedReadonlyRounds -> ReadonlyRoundBudget

    /// 严格成对解析错误类型
    [<RequireQualifiedAccess>]
    type EstimateArgumentError =
        | MissingEstimate
        | WrongNumberType
        | InvalidRange
        | NotePresentWhenZero
        | MissingOrBlankNoteWhenPositive
        | NoteNotString
        | MixedProtocolFields
        | InvalidArgumentObject

    /// 解析成对参数：估计值与非空白短记（当估计大于 0 时）
    val parseParticipatingArguments:
        arguments: obj -> Result<EstimatedReadonlyRounds * string option, EstimateArgumentError>
