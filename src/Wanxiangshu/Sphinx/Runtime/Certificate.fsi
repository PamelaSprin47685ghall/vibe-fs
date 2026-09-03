namespace Wanxiangshu.Sphinx.Runtime

open Wanxiangshu.Sphinx.Core

type CertificateError =
    { Code: string
      Message: string }

type CertificatePatchRequest =
    { Slot: string
      Value: obj option
      Lower: float option
      Upper: float option
      Summary: obj option
      Constraints: obj option
      Posterior: obj option
      ResidualValue: float option
      GuaranteeKind: string option
      Level: float option
      Error: float option
      Assumptions: string[] option
      Scope: string option
      Witnesses: EventId list
      Derivations: EventId list }

module Certificate =
    val empty: nodeId: NodeId -> ValueCertificate
    val apply:
        certificate: ValueCertificate ->
        patch: CertificatePatchRequest ->
            Result<ValueCertificate, CertificateError>
