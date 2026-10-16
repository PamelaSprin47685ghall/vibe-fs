namespace Wanxiangshu.Enforcer

[<RequireQualifiedAccess>]
type ChronicleExecution =
    | Completed of string
    | NoLiveCycle

[<RequireQualifiedAccess>]
module ChronicleExecution =

    [<Literal>]
    let EmptyTextError = "CHRONICLE_EMPTY_ENFORCER_061"

    [<Literal>]
    let NoLiveCycleError = "CHRONICLE_NO_LIVE_CYCLE"

    let tryCanonicalText (rawText: string) : Result<string, string> =
        let trimmed = if isNull rawText then "" else rawText.Trim()

        if trimmed.Length = 0 then
            Error EmptyTextError
        else
            Ok trimmed

    /// C2.4/C2.5 / S4：执行期 tipName 检查。读取面（args.Text）已把缺失与
    /// 非 string 归并为空串（S4.1）。判定序：trim 空 → missing-tip-name；
    /// 含换行或回车 → tip-name-multiline；超 200 字符（String.Length）→
    /// tip-name-too-long。成功返回 trim 后原串。
    let tryTipName (rawText: string) : Result<string, string> =
        let trimmed = if isNull rawText then "" else rawText.Trim()

        if trimmed.Length = 0 then
            Error EnforcerCodec.MissingTipNameError
        elif trimmed.Contains "\n" || trimmed.Contains "\r" then
            Error EnforcerCodec.TipNameMultilineError
        elif trimmed.Length > EnforcerCodec.MaxTipNameChars then
            Error EnforcerCodec.TipNameTooLongError
        else
            Ok trimmed

    /// C2.1 / S4：内容字段逐节检查；缺失或空白 → missing-tip-field:<camelCase 字段名>。
    let tryTipField (fieldName: string) (rawText: string) : Result<string, string> =
        let trimmed = if isNull rawText then "" else rawText.Trim()

        if trimmed.Length = 0 then
            Error(EnforcerCodec.MissingTipFieldPrefix + fieldName)
        else
            Ok trimmed

    let decide hasLiveCycle completed =
        if hasLiveCycle then
            ChronicleExecution.Completed completed
        else
            ChronicleExecution.NoLiveCycle
