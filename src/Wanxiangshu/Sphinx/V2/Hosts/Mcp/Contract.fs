namespace Wanxiangshu.Sphinx.V2.Hosts

/// The v2 MCP tool contract.
///
/// WHAT[sphinx-v2-036]: the tool list is closed. Seven tools, no aliases, and nothing
/// that used to be a stage entry point. `status` and `export` are read-only: they never
/// create a lease, never call a model and never change business state.
///
/// WHAT[sphinx-v2-035]: Sphinx `apiVersion = "2"` is a business contract. It is not the
/// MCP protocol revision, and changing one does not change the other.

[<RequireQualifiedAccess>]
type SphinxTool =
    | InquiryStart
    | WorkNext
    | WorkSubmit
    | InquiryStatus
    | InquiryCancel
    | InquiryExport
    | GoalAmend

module Contract =

    let toolName (tool: SphinxTool) : string =
        match tool with
        | SphinxTool.InquiryStart -> "sphinx_inquiry_start"
        | SphinxTool.WorkNext -> "sphinx_work_next"
        | SphinxTool.WorkSubmit -> "sphinx_work_submit"
        | SphinxTool.InquiryStatus -> "sphinx_inquiry_status"
        | SphinxTool.InquiryCancel -> "sphinx_inquiry_cancel"
        | SphinxTool.InquiryExport -> "sphinx_inquiry_export"
        | SphinxTool.GoalAmend -> "sphinx_goal_amend"

    /// The complete public tool list, in a stable order so a tool listing is comparable
    /// across runs.
    let all: SphinxTool list =
        [ SphinxTool.InquiryStart
          SphinxTool.WorkNext
          SphinxTool.WorkSubmit
          SphinxTool.InquiryStatus
          SphinxTool.InquiryCancel
          SphinxTool.InquiryExport
          SphinxTool.GoalAmend ]

    /// The business API version. Independent of the MCP protocol revision.
    [<Literal>]
    let apiVersion = "2"

    /// Roles. The distinction that matters: a worker can submit results for the work it
    /// holds, and nothing else.
    let roleOf (tool: SphinxTool) : string =
        match tool with
        | SphinxTool.InquiryStart -> "orchestrator"
        | SphinxTool.WorkNext -> "authorized-executor"
        | SphinxTool.WorkSubmit -> "work-executor"
        | SphinxTool.InquiryStatus -> "inquiry-reader"
        | SphinxTool.InquiryCancel -> "controller"
        | SphinxTool.InquiryExport -> "export-owner"
        | SphinxTool.GoalAmend -> "user-authorized-controller"

    /// Read-only tools never create a lease, call a model, or change business state.
    let isReadOnly (tool: SphinxTool) : bool =
        match tool with
        | SphinxTool.InquiryStatus
        | SphinxTool.InquiryExport -> true
        | _ -> false

    /// True when a name is one of the seven public tools. Everything else — including
    /// the old assess/propose/investigate/synthesize stages — is not a Sphinx tool.
    let isTool (name: string) : bool =
        all |> List.exists (fun tool -> toolName tool = name)
