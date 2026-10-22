namespace Wanxiangshu.Repository.Knowledge.Casebook

[<Sealed>]
type CasebookSettlementOwner =
    new: onConflict: (CasebookSemanticCutIncident -> unit) -> CasebookSettlementOwner
    member Observe: failure: CasebookAppendFailure -> CasebookSettlementOutcome
    member Redeliver: incident: CasebookSemanticCutIncident -> CasebookSettlementOutcome
