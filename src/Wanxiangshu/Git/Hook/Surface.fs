namespace Wanxiangshu.Git.Hook

open Fable.Core.JsInterop

/// Plain-data hook installation surface. HookDispatcher owns the physical
/// membrane; this module prevents its DU and path types crossing into tests.
[<RequireQualifiedAccess>]
module HookSurface =

    let private text (value: obj) =
        if isNull value then "" else string value

    let private kindOf (value: string) =
        match value with
        | "ReferenceTransaction" -> HookDispatcher.HookKind.ReferenceTransaction
        | "PrePush" -> HookDispatcher.HookKind.PrePush
        | other -> failwith $"HookSurface: unknown hook kind '{other}'"

    let private verdictName verdict =
        match verdict with
        | HookDispatcher.HookInstallVerdict.Installed -> "Installed"
        | HookDispatcher.HookInstallVerdict.AlreadyOwned -> "AlreadyOwned"
        | HookDispatcher.HookInstallVerdict.ForeignHook _ -> "ForeignHook"
        | HookDispatcher.HookInstallVerdict.DiagnoseIncomplete _ -> "DiagnoseIncomplete"

    let classifyExistingHook (existingBody: obj) : string =
        let body =
            if isNull existingBody then
                None
            else
                Some(text existingBody)

        HookDispatcher.classifyExistingHook body |> verdictName

    let installOrDiagnose (hooksDirectory: string) (kind: string) (shimBody: string) : string =
        HookDispatcher.installOrDiagnose hooksDirectory (kindOf kind) shimBody
        |> verdictName
