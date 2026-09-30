namespace Wanxiangshu.Enforcer

open System

/// docs/what/enforcer.md ENFORCER-020…026：`blog` tip v2 codec.
///
/// raw JSON object → Result<CanonicalBlogCall, string>.
/// tip = exact catalog field or deterministic nearest edit-distance match; no score map.
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

    /// ENFORCER-004 / 026：领域闭合类型。Tip 必填。
    type CanonicalBlogCall =
        { Content: ChronicleContent
          Tip: EnforcerTip }

    /// ENFORCER-023 错误面。
    [<Literal>]
    let MissingTipError = "missing required argument: tip"

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

    let private decodeTipString
        (rules: EnforcerRule list)
        (content: ChronicleContent)
        (tipRaw: string)
        : Result<CanonicalBlogCall, string> =
        let tipValue = tipRaw.Trim()

        if tipValue.Length = 0 then
            Error MissingTipError
        else
            EnforcerCatalog.resolveByField tipValue rules
            |> Option.map (fun rule ->
                Ok
                    { Content = content
                      Tip = EnforcerTip.ofRule rule })
            |> Option.defaultValue (Error MissingTipError)

    let private decodeTip
        (rules: EnforcerRule list)
        (content: ChronicleContent)
        (value: obj)
        : Result<CanonicalBlogCall, string> =
        match value with
        | :? string as tipRaw -> decodeTipString rules content tipRaw
        | _ -> Error MissingTipError

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
    /// 缺 tip / tip 非 string / 未知 field → Error。
    /// 新协议必须完整提供 charge/occurrence/settlement/consequence，可选 evidence。
    /// entry/text 仅作为升级前 transcript 的 legacy recovery 身份，且不得与
    /// structured fields 混用；legacy evidence 仍可随旧 entry/text 解码。
    /// 其它 property 忽略（ENFORCER-024）。不默认 tip。
    let decodeCall (rules: EnforcerRule list) (rawArgs: Map<string, obj>) : Result<CanonicalBlogCall, string> =
        match Map.tryFind "tip" rawArgs with
        | None -> Error MissingTipError
        | Some null -> Error MissingTipError
        | Some value ->
            decodeContent rawArgs
            |> Result.bind (fun content -> decodeTip rules content value)

    /// ENFORCER-022/061：能进入 CanonicalBlogCall 的内容已经完整且非空。
    let hasValidText (_: CanonicalBlogCall) : bool = true
