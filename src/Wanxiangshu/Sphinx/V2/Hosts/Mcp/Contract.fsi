namespace Wanxiangshu.Sphinx.V2.Hosts

[<RequireQualifiedAccess>]
type SphinxTool =
    | InquiryStart
    | WorkSubmit
    | InquiryStatus
    | InquiryCancel
    | InquiryExport

module Contract =
    val toolName: SphinxTool -> string

    /// The complete public tool list, in a stable order.
    val all: SphinxTool list

    /// The business API version. Independent of the MCP protocol revision.
    [<Literal>]
    val apiVersion: string = "2"

    val roleOf: SphinxTool -> string

    /// True when a name is one of the five public tools.
    val isTool: string -> bool
