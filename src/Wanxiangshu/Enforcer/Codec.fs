namespace Wanxiangshu.Enforcer

open System
open Fable.Core

/// docs/what/enforcer.md ENFORCER-020…026：`blog` tip v2 codec.
///
/// raw JSON object → Result<CanonicalBlogCall, string>.
/// tipName = the blogged free name (trimmed original; no catalog lookup);
/// the opinion and advice fields decode with the trim-nonempty family. No score map.
module EnforcerCodec =

    type ChronicleRecord =
        { Charge: string
          Occurrence: string
          Settlement: string
          Consequence: string
          Evidence: string option }

    [<RequireQualifiedAccess>]
    type ChronicleContent =
        | Structured of ChronicleRecord
        | Legacy of text: string * evidence: string option

    /// ENFORCER-004 / 026：领域闭合类型。TipName 与 opinion/advice 必填。
    type CanonicalBlogCall =
        { Content: ChronicleContent
          TipName: string
          Opinion: string
          Advice: string }

    /// ENFORCER-023 / behavior-diagnosis-006：执行期与解码期共用的内部标识。
    [<Literal>]
    let MissingTipNameError = "missing-tip-name"

    [<Literal>]
    let TipNameTooLongError = "tip-name-too-long"

    [<Literal>]
    let TipNameMultilineError = "tip-name-multiline"

    [<Literal>]
    let TipFieldNotAStringError = "tip-field-not-a-string"

    [<Literal>]
    let MissingTipFieldPrefix = "missing-tip-field:"

    [<Literal>]
    let MaxTipNameChars = 200

    [<Literal>]
    let MissingChargeError = "missing required argument: charge"

    [<Literal>]
    let MissingOccurrenceError = "missing required argument: occurrence"

    [<Literal>]
    let MissingSettlementError = "missing required argument: settlement"

    [<Literal>]
    let MissingConsequenceError = "missing required argument: consequence"

    [<Literal>]
    let MissingChronicleContentError = "missing required chronicle content"

    [<Literal>]
    let MixedChronicleProtocolError =
        "structured chronicle fields cannot be mixed with legacy entry/text"

    [<Literal>]
    let InvalidEvidenceError = "optional argument evidence must be a string"

    [<Literal>]
    let EvidenceTooLongError = "optional argument evidence exceeds 1024 characters"

    [<Literal>]
    let MaxInlineEvidenceChars = 1024

    /// ENFORCER-022：Chronicle 字符串字段统一抽取（trim；空 → None）。
    let private tryStringArg (rawArgs: Map<string, obj>) (key: string) : string option =
        rawArgs
        |> Map.tryFind key
        |> Option.bind (function
            | :? string as s ->
                let t = s.Trim()
                if t.Length = 0 then None else Some t
            | _ -> None)

    [<Emit("$0 === undefined")>]
    let private isUndefined (value: obj) : bool = jsNative

    /// D3：内容字段名。字段顺序即 schema 与解码顺序。
    let tipSectionNames = [ "opinion"; "advice" ]

    let private missingTipFieldError (fieldName: string) = MissingTipFieldPrefix + fieldName

    /// D2：tipName 判定序。缺键或 undefined → missing-tip-name；非 string
    /// （含 null）→ tip-field-not-a-string；trim 空 → missing-tip-name；
    /// 含换行或回车 → tip-name-multiline；超 200 字符 → tip-name-too-long。
    /// 返回 trim 后原串（保留大小写与内部字节）。
    let private decodeTipName (rawArgs: Map<string, obj>) : Result<string, string> =
        match Map.tryFind "tipName" rawArgs with
        | None -> Error MissingTipNameError
        | Some value when isUndefined value -> Error MissingTipNameError
        | Some value ->
            match value with
            | :? string as rawText ->
                let trimmed = rawText.Trim()

                if trimmed.Length = 0 then
                    Error MissingTipNameError
                elif trimmed.Contains "\n" || trimmed.Contains "\r" then
                    Error TipNameMultilineError
                elif trimmed.Length > MaxTipNameChars then
                    Error TipNameTooLongError
                else
                    Ok trimmed
            | _ -> Error TipFieldNotAStringError

    /// D3：单节解码。缺键或空白 → missing-tip-field:<camelCase 字段名>；
    /// 非 string → tip-field-not-a-string；返回 trim 后文本。
    let private decodeTipField (fieldName: string) (rawArgs: Map<string, obj>) : Result<string, string> =
        match Map.tryFind fieldName rawArgs with
        | None -> Error(missingTipFieldError fieldName)
        | Some value when isUndefined value -> Error(missingTipFieldError fieldName)
        | Some value ->
            match value with
            | :? string as rawText ->
                let trimmed = rawText.Trim()

                if trimmed.Length = 0 then
                    Error(missingTipFieldError fieldName)
                else
                    Ok trimmed
            | _ -> Error TipFieldNotAStringError

    let private decodeTipSections (rawArgs: Map<string, obj>) : Result<string * string, string> =
        match decodeTipField "opinion" rawArgs, decodeTipField "advice" rawArgs with
        | Ok opinion, Ok advice -> Ok(opinion, advice)
        | Error error, _ -> Error error
        | _, Error error -> Error error

    let private structuredFieldNames =
        [ "charge"; "occurrence"; "settlement"; "consequence" ]

    let private legacyFieldNames = [ "entry"; "text" ]

    let private hasAnyField names (rawArgs: Map<string, obj>) =
        names |> List.exists (fun name -> Map.containsKey name rawArgs)

    let private decodeEvidence (rawArgs: Map<string, obj>) : Result<string option, string> =
        match Map.tryFind "evidence" rawArgs with
        | None
        | Some null -> Ok None
        | Some(:? string as evidence) when String.IsNullOrWhiteSpace evidence -> Ok None
        | Some(:? string as evidence) when evidence.Length <= MaxInlineEvidenceChars -> Ok(Some evidence)
        | Some(:? string) -> Error EvidenceTooLongError
        | Some _ -> Error InvalidEvidenceError

    let private decodeStructured (rawArgs: Map<string, obj>) : Result<ChronicleContent, string> =
        match
            tryStringArg rawArgs "charge",
            tryStringArg rawArgs "occurrence",
            tryStringArg rawArgs "settlement",
            tryStringArg rawArgs "consequence"
        with
        | Some charge, Some occurrence, Some settlement, Some consequence ->
            decodeEvidence rawArgs
            |> Result.map (fun evidence ->
                ChronicleContent.Structured
                    { Charge = charge
                      Occurrence = occurrence
                      Settlement = settlement
                      Consequence = consequence
                      Evidence = evidence })
        | None, _, _, _ -> Error MissingChargeError
        | _, None, _, _ -> Error MissingOccurrenceError
        | _, _, None, _ -> Error MissingSettlementError
        | _, _, _, None -> Error MissingConsequenceError

    let private decodeLegacy (rawArgs: Map<string, obj>) : Result<ChronicleContent, string> =
        let text =
            tryStringArg rawArgs "entry" |> Option.orElse (tryStringArg rawArgs "text")

        match text with
        | Some value -> Ok(ChronicleContent.Legacy(value, tryStringArg rawArgs "evidence"))
        | None -> Error MissingChronicleContentError

    let private decodeContent (rawArgs: Map<string, obj>) : Result<ChronicleContent, string> =
        let hasStructured = hasAnyField structuredFieldNames rawArgs
        let hasLegacy = hasAnyField legacyFieldNames rawArgs

        match hasStructured, hasLegacy with
        | true, true -> Error MixedChronicleProtocolError
        | true, false -> decodeStructured rawArgs
        | false, true -> decodeLegacy rawArgs
        | false, false -> Error MissingChronicleContentError

    /// ENFORCER-020/021/023：解析一个 blog 调用。
    ///
    /// 先解 tipName 与 opinion/advice（形态无关的顶层必填字段），再解内容形态。
    /// 缺键/空白/非 string/超长/含换行 → C4.1 内部标识；不映射工具文案。
    /// 新协议必须完整提供 charge/occurrence/settlement/consequence，可选 evidence。
    /// entry/text 仅作为升级前 transcript 的 legacy recovery 身份，且不得与
    /// structured fields 混用；legacy evidence 仍可随旧 entry/text 解码。
    /// 其它 property 忽略（ENFORCER-024）。不默认 tip。
    let decodeCall (rawArgs: Map<string, obj>) : Result<CanonicalBlogCall, string> =
        match decodeTipName rawArgs, decodeTipSections rawArgs with
        | Ok tipName, Ok(opinion, advice) ->
            decodeContent rawArgs
            |> Result.map (fun content ->
                { Content = content
                  TipName = tipName
                  Opinion = opinion
                  Advice = advice })
        | Error error, _ -> Error error
        | _, Error error -> Error error

    /// ENFORCER-022/061：能进入 CanonicalBlogCall 的内容已经完整且非空。
    let hasValidText (_: CanonicalBlogCall) : bool = true
