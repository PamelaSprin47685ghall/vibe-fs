namespace Wanxiangshu.Host

open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity

/// HNI P-18: the tool-call context, what one product tool call knows about its
/// invocation, independent of the host that decoded it.
type HostToolContext =
    { SessionId: string
      Agent: string option
      ToolCallId: ToolCallId option
      ProviderRunId: ProviderRunIdentity option
      PromptText: string option
      AttachAbort: (unit -> unit) -> (unit -> unit) }

[<RequireQualifiedAccess>]
type ToolAdmission =
    | OfficeRole of (HostToolContext -> Role -> bool)
    | PrivateAttachment of (HostToolContext -> bool)

module ToolResultText =

    type TomlValue =
        | TString of string
        | TInt of int
        | TInt64 of int64
        | TBool of bool
        | TTable of (string * TomlValue) list

    let private scalarMember name value =
        match value with
        | TString text -> Some(LlmFacing.Data.stringMember name text)
        | TInt number -> Some(LlmFacing.Data.intMember name number)
        | TInt64 number -> Some(LlmFacing.Data.int64Member name number)
        | TBool flag -> Some(LlmFacing.Data.boolMember name flag)
        | TTable _ -> None

    let rec private blocksForField (name, value) =
        match value with
        | TString text -> [ LlmFacing.Data.stringField name text ]
        | TInt number -> [ LlmFacing.Data.intField name number ]
        | TInt64 number -> [ LlmFacing.Data.int64Field name number ]
        | TBool flag -> [ LlmFacing.Data.boolField name flag ]
        | TTable entries ->
            let local =
                entries
                |> List.choose (fun (fieldName, fieldValue) -> scalarMember fieldName fieldValue)

            let nested = entries |> List.collect nestedBlocksForField
            LlmFacing.Data.table name local :: nested

    and private nestedBlocksForField (name, value) =
        match value with
        | TTable _ -> blocksForField (name, value)
        | _ -> []

    let private objectBlocks fields = fields |> List.collect blocksForField

    let tomlObject (fields: (string * TomlValue) list) : string =
        LlmFacing.empty |> LlmFacing.withData (objectBlocks fields) |> LlmFacing.render

    let tomlObjectWithInstructions (instructions: string list) (fields: (string * TomlValue) list) : string =
        LlmFacing.instructions instructions
        |> LlmFacing.withData (objectBlocks fields)
        |> LlmFacing.render

    let private tableMember (name: string, value: TomlValue) =
        match value with
        | TString text -> LlmFacing.Data.stringMember name text
        | TInt number -> LlmFacing.Data.intMember name number
        | TInt64 number -> LlmFacing.Data.int64Member name number
        | TBool flag -> LlmFacing.Data.boolMember name flag
        | TTable _ -> invalidArg "entries" "nested table values are not valid inside a table-array row"

    let tomlTable (name: string) (entries: (string * TomlValue) list list) : string =
        let blocks =
            entries
            |> List.map (fun entry -> LlmFacing.Data.tableArray name (entry |> List.map tableMember))

        LlmFacing.empty |> LlmFacing.withData blocks |> LlmFacing.render
