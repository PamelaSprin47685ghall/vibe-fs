namespace Wanxiangshu.Repository.Programming.Js.OpenCode

open System
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Foundation
open Wanxiangshu.OpenCode
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Repository.Programming.Js

/// Manager review read-only tools: read-manager, glob-manager, grep-manager.
/// These tools run fixed, read-only JavaScript programs in the JsProgram sandbox
/// without mutation capabilities, executing with capabilities = { Read, Glob, Grep }.
module ManagerReadTools =

    let private directoryFor (scope: ToolRuntimeScope) (context: HostToolContext) : string =
        let dir =
            if String.IsNullOrWhiteSpace context.SessionId then
                scope.WorkspaceDirectory
            else
                scope.DirectoryFor context.SessionId |> Option.orElse scope.WorkspaceDirectory

        defaultArg dir ""

    let private managerAdmission: ToolAdmission =
        ToolAdmission.OfficeRole(fun _ r -> r = Role.Manager)

    let private runFixedProgram
        (workspaceRoot: string)
        (groundingObservation: HostToolContext -> string list -> string list -> Task<unit>)
        (ctx: HostToolContext)
        (programSource: string)
        : Task<string> =
        task {
            let deadlineEpochMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 10000L
            let prose = JsDescriptionAssets.load (ProviderLanguageBinding.readGlobalPreference ())
            let capabilities = set [ JsCapability.Read; JsCapability.Glob; JsCapability.Grep ]
            let baseClassSource = JsCanonicalDescription.runtimeBaseClass prose capabilities

            let! outcome =
                JsToolWorkflow.runWithFileAccessObservation
                    capabilities
                    workspaceRoot
                    baseClassSource
                    programSource
                    10000
                    deadlineEpochMs
                    (1 <<< 20)
                    None
                    (groundingObservation ctx)

            return JsToolsResult.render outcome
        }

    let private buildReadProgram (filePath: string) (offset: int) (limit: int) : string =
        let jsonFilePath = JS.JSON.stringify filePath

        [ "class Js extends JsProgram {"
          "  async run() {"
          sprintf "    const p = %s;" jsonFilePath
          sprintf "    const offset = %d;" offset
          sprintf "    const limit = %d;" limit
          "    const maxLineLength = 2000;"
          "    const f = await this.file(p);"
          "    const source = f.text();"
          "    const rawLines = source.length === 0 ? [] : source.split(/\\r?\\n/);"
          "    const totalLines = rawLines.length;"
          "    const startIndex = Math.max(0, offset - 1);"
          "    const endIndex = Math.min(totalLines, startIndex + limit);"
          "    let isTruncated = (endIndex < totalLines) || (startIndex > 0 && startIndex < totalLines);"
          "    const lines = [];"
          "    for (let i = startIndex; i < endIndex; i++) {"
          "      let lineText = rawLines[i];"
          "      if (lineText.length > maxLineLength) {"
          "        lineText = lineText.slice(0, maxLineLength) + \"... [truncated]\";"
          "        isTruncated = true;"
          "      }"
          "      lines.push((i + 1) + \": \" + lineText);"
          "    }"
          "    return {"
          "      path: p,"
          "      offset: offset,"
          "      limit: limit,"
          "      totalLines: totalLines,"
          "      lines: lines,"
          "      truncated: isTruncated"
          "    };"
          "  }"
          "}" ]
        |> String.concat "\n"

    let private buildGlobProgram (pattern: string) (pathOpt: string option) : string =
        let jsonPattern = JS.JSON.stringify pattern

        let jsonPath =
            match pathOpt with
            | Some p -> JS.JSON.stringify p
            | None -> "null"

        [ "class Js extends JsProgram {"
          "  async run() {"
          sprintf "    const pattern = %s;" jsonPattern
          sprintf "    const path = %s;" jsonPath
          "    let finalPattern = pattern;"
          "    if (path && typeof path === \"string\" && path.trim() !== \"\" && path !== \".\") {"
          "      const cleanPath = path.replace(/\\\\/g, \"/\").replace(/\\/+$/, \"\");"
          "      const cleanPattern = pattern.replace(/^\\/+/, \"\");"
          "      finalPattern = cleanPath + \"/\" + cleanPattern;"
          "    }"
          "    const res = await this.glob(finalPattern);"
          "    return res;"
          "  }"
          "}" ]
        |> String.concat "\n"

    let private buildGrepProgram (pattern: string) (pathOpt: string option) (includeOpt: string option) : string =
        let jsonPattern = JS.JSON.stringify pattern

        let jsonPath =
            match pathOpt with
            | Some p -> JS.JSON.stringify p
            | None -> "null"

        let jsonInclude =
            match includeOpt with
            | Some inc -> JS.JSON.stringify inc
            | None -> "null"

        [ "class Js extends JsProgram {"
          "  async run() {"
          sprintf "    const pattern = %s;" jsonPattern
          sprintf "    const path = %s;" jsonPath
          sprintf "    const include = %s;" jsonInclude
          "    let needle;"
          "    try {"
          "      needle = new RegExp(pattern);"
          "    } catch (_) {"
          "      needle = pattern;"
          "    }"
          "    let filePattern = (include && typeof include === \"string\" && include.trim() !== \"\") ? include.trim() : \"**\";"
          "    if (path && typeof path === \"string\" && path.trim() !== \"\" && path !== \".\") {"
          "      const cleanPath = path.replace(/\\\\/g, \"/\").replace(/\\/+$/, \"\");"
          "      const cleanInclude = filePattern.replace(/^\\/+/, \"\");"
          "      if (!cleanInclude.includes(\"/\") && !cleanInclude.startsWith(\"**\")) {"
          "        filePattern = cleanPath + \"/**/\" + cleanInclude;"
          "      } else {"
          "        filePattern = cleanPath + \"/\" + cleanInclude;"
          "      }"
          "    }"
          "    const res = await this.grep(needle, filePattern);"
          "    return res;"
          "  }"
          "}" ]
        |> String.concat "\n"

    let readManagerSpec
        (factory: HostToolFactory)
        (scope: ToolRuntimeScope)
        (groundingObservation: HostToolContext -> string list -> string list -> Task<unit>)
        : ToolSpec =
        { Name = "read-manager"
          Description = "Read file content from the filesystem during manager review."
          Arguments =
            [ "filePath", ToolHostCodec.stringSchemaDescribed "The path to the file to read" factory
              "offset",
              ToolHostCodec.optionalBoundedIntegerSchema 1 2147483647 "The line number to start reading from (1-indexed)" factory
              "limit",
              ToolHostCodec.optionalBoundedIntegerSchema 1 2000 "The maximum number of lines to read (defaults to 2000, max 2000)" factory ]
          Admission = managerAdmission
          Execute =
            fun args ctx ->
                task {
                    match args.OptionalText "filePath" with
                    | None | Some "" ->
                        return
                            ToolHostCodec.tomlObject
                                [ "error", ToolHostCodec.TString "filePath is required" ]
                    | Some filePath ->
                        let offset =
                            match args.OptionalNonNegativeInteger "offset" with
                            | Ok (Some n) when n >= 1 -> n
                            | _ -> 1

                        let limit =
                            match args.OptionalNonNegativeInteger "limit" with
                            | Ok (Some n) when n >= 1 -> min n 2000
                            | _ -> 2000

                        let program = buildReadProgram filePath offset limit
                        let root = directoryFor scope ctx
                        return! runFixedProgram root groundingObservation ctx program
                } }

    let globManagerSpec
        (factory: HostToolFactory)
        (scope: ToolRuntimeScope)
        (groundingObservation: HostToolContext -> string list -> string list -> Task<unit>)
        : ToolSpec =
        { Name = "glob-manager"
          Description = "Enumerate files matching a glob pattern during manager review."
          Arguments =
            [ "pattern", ToolHostCodec.stringSchemaDescribed "The glob pattern to match files against" factory
              "path", ToolHostCodec.optionalStringSchemaDescribed "The directory to search in (optional)" factory ]
          Admission = managerAdmission
          Execute =
            fun args ctx ->
                task {
                    match args.OptionalText "pattern" with
                    | None | Some "" ->
                        return
                            ToolHostCodec.tomlObject
                                [ "error", ToolHostCodec.TString "pattern is required" ]
                    | Some pattern ->
                        let pathOpt = args.OptionalText "path"
                        let program = buildGlobProgram pattern pathOpt
                        let root = directoryFor scope ctx
                        return! runFixedProgram root groundingObservation ctx program
                } }

    let grepManagerSpec
        (factory: HostToolFactory)
        (scope: ToolRuntimeScope)
        (groundingObservation: HostToolContext -> string list -> string list -> Task<unit>)
        : ToolSpec =
        { Name = "grep-manager"
          Description = "Search file contents for regex or literal matches during manager review."
          Arguments =
            [ "pattern", ToolHostCodec.stringSchemaDescribed "The regex or literal pattern to search for in file contents" factory
              "path", ToolHostCodec.optionalStringSchemaDescribed "The directory to search in (optional)" factory
              "include",
              ToolHostCodec.optionalStringSchemaDescribed "File pattern to include in the search (optional, e.g. \"*.js\", \"*.{ts,tsx}\")" factory ]
          Admission = managerAdmission
          Execute =
            fun args ctx ->
                task {
                    match args.OptionalText "pattern" with
                    | None | Some "" ->
                        return
                            ToolHostCodec.tomlObject
                                [ "error", ToolHostCodec.TString "pattern is required" ]
                    | Some pattern ->
                        let pathOpt = args.OptionalText "path"
                        let includeOpt = args.OptionalText "include"
                        let program = buildGrepProgram pattern pathOpt includeOpt
                        let root = directoryFor scope ctx
                        return! runFixedProgram root groundingObservation ctx program
                } }
