namespace Wanxiangshu.Sphinx

module GecInquiry =
    val toolGenericStart: string
    val toolGenericSubmit: string
    val toolGenericStatus: string
    val toolGenericExport: string
    val toolGenericCancel: string

    val codeRevisionConflict: string
    val codeUnknownInquiry: string

    type GecInquiryEntry =
        { InquiryId: string
          InquiryQuestion: string
          InquiryProfile: string
          InquiryPlugins: obj
          InquiryExecutionMode: string
          InquiryBudget: obj
          InquiryRevision: int
          InquiryCancelled: bool
          InquiryResults: obj list }

    [<RequireQualifiedAccess>]
    type InquiryFault =
        | UnknownInquiry of inquiryId: string
        | InquiryCancelled of inquiryId: string
        | RevisionConflict of inquiryId: string * current: int

    val faultCode: fault: InquiryFault -> string
    val faultMessage: fault: InquiryFault -> string

    val BuildStart:
        question: string * profile: string * plugins: obj * executionMode: string * budget: obj -> GecInquiryEntry

    val DecideSubmit:
        entry: GecInquiryEntry * expectedRevision: int * results: obj list -> Result<GecInquiryEntry, InquiryFault>

    val DecideCancel: entry: GecInquiryEntry -> Result<GecInquiryEntry, InquiryFault>

    [<Sealed>]
    type Registry =
        new: unit -> Registry

        member Restore: entry: GecInquiryEntry -> unit

        member Start:
            question: string * profile: string * plugins: obj * executionMode: string * budget: obj -> GecInquiryEntry

        member TryFind: inquiryId: string -> GecInquiryEntry option

        member Submit:
            inquiryId: string * expectedRevision: int * results: obj list -> Result<GecInquiryEntry, InquiryFault>

        member Cancel: inquiryId: string -> Result<GecInquiryEntry, InquiryFault>

    val entryView: entry: GecInquiryEntry -> obj
    val submitView: entry: GecInquiryEntry -> accepted: int -> obj
    val exportView: entry: GecInquiryEntry -> obj
    val cancelView: entry: GecInquiryEntry -> obj
