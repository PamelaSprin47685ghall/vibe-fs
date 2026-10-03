namespace Wanxiangshu.Sphinx.V2.Hosts

open System.Threading.Tasks
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.OpenCode
open Wanxiangshu.Sphinx.V2.Core
open Wanxiangshu.Sphinx.V2.Runtime

/// The OpenCode Host adapter. Every receipt comes from the real Host; it owns no
/// inquiry state and executes only already-persisted work.
type OpenCodeHostPort =
    new: sessions: ISessionHostPort -> OpenCodeHostPort

    /// The capabilities this adapter can actually honour.
    member Capabilities: unit -> string list

    /// Dispatches already-persisted work to a real session and returns its receipt.
    member Dispatch: InquiryId -> WorkSpec -> JsonEnvelope -> JsonEnvelope -> Task<Result<DispatchReceipt, string>>

    /// The session port exposes no status query, so the status is not established here.
    member ReadStatus: InquiryId -> WorkId -> Attempt -> string -> Task<PhysicalStatus>

    /// Fails: the session port exposes no message or tool-result read.
    member ReadResult: InquiryId -> WorkId -> Attempt -> string -> Task<Result<string option, string>>

    /// An unconfirmed abort is an error, so the inquiry stays cancelling.
    member RequestCancel: InquiryId -> WorkId -> Attempt -> string -> Task<Result<Unit, string>>

    /// Fails: the session port exposes no dispatch lookup.
    member Reconcile: InquiryId -> string -> Task<Result<string option, string>>
