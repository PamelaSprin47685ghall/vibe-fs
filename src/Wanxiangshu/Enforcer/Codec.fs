namespace Wanxiangshu.Enforcer

open Wanxiangshu.Context.Prefix
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Foundation
open Wanxiangshu.Host
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt

open System

/// docs/what/enforcer.md ENFORCER-020…026：`blog` tip v2 codec.
///
/// raw JSON object → Result<CanonicalBlogCall, string>.
/// tip = catalog field exact match only; no score map, no fuzzy field mapping.
module EnforcerCodec =

    /// ENFORCER-004 / 026：领域闭合类型。Tip 必填。
    type CanonicalBlogCall =
        { Text: string option
          Evidence: string option
          Tip: EnforcerTip }

    /// ENFORCER-023 错误面。
    [<Literal>]
    let MissingTipError = "missing required argument: tip"

    let unknownTipError (tipValue: string) = sprintf "UnknownTip %s" tipValue

    /// ENFORCER-022：text / tip / evidence 的字符串抽取（trim；空 → None）。
    let private tryStringArg (rawArgs: Map<string, obj>) (key: string) : string option =
        rawArgs
        |> Map.tryFind key
        |> Option.bind (function
            | :? string as s ->
                let t = s.Trim()
                if t.Length = 0 then None else Some t
            | _ -> None)

    /// ENFORCER-020/021/023：解析一个 blog 调用。
    ///
    /// 缺 tip / tip 非 string / 未知 field → Error。
    /// 其它 property 忽略（ENFORCER-024）。不默认 tip。
    let decodeCall (rules: EnforcerRule list) (rawArgs: Map<string, obj>) : Result<CanonicalBlogCall, string> =
        let text =
            tryStringArg rawArgs "entry" |> Option.orElse (tryStringArg rawArgs "text")

        let evidence = tryStringArg rawArgs "evidence"

        match Map.tryFind "tip" rawArgs with
        | None -> Error MissingTipError
        | Some null -> Error MissingTipError
        | Some value ->
            match value with
            | :? string as tipRaw ->
                let tipValue = tipRaw.Trim()

                if tipValue.Length = 0 then
                    Error MissingTipError
                else
                    match EnforcerCatalog.tryFindByField tipValue rules with
                    | None -> Error(unknownTipError tipValue)
                    | Some rule ->
                        Ok
                            { Text = text
                              Evidence = evidence
                              Tip = EnforcerTip.ofRule rule }
            | _ -> Error MissingTipError

    /// ENFORCER-022/061：text 必须存在且规范化后非空。
    let hasValidText (call: CanonicalBlogCall) : bool = call.Text.IsSome
