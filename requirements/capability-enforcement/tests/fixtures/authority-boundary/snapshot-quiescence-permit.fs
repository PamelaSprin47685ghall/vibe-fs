namespace Foreign

// DSL-AUTHORITY: Capability
type QuiescencePermit = private QuiescencePermit of obj

module QuiescenceAdmission =
    let issue value = QuiescencePermit value

type ArbitrarySnapshot =
    { CurrentPermit: QuiescencePermit option }
