namespace Wanxiangshu.Sphinx.V2.Hosts

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
    val toolName: SphinxTool -> string

    /// The complete public tool list, in a stable order.
    val all: SphinxTool list

    /// The business API version. Independent of the MCP protocol revision.
    [<Literal>]
    val apiVersion: string = "2"

    val roleOf: SphinxTool -> string

    /// Read-only tools never create a lease, call a model, or change business state.
    val isReadOnly: SphinxTool -> bool

    /// True when a name is one of the seven public tools.
    val isTool: string -> bool
