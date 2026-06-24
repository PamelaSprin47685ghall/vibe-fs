module VibeFs.Methodology.SecurityReview

open VibeFs.Methodology.SchemaCommon

let schema =
    buildSchema
        "security_review"
        "Reason adversarially about trust boundaries and abuse paths."
        "When tools execute code, read files, spawn subagents, or accept huge backgrounds."
        [ reqStr
              "trust_boundary"
              "Boundary under review (LLM→tool args, plugin→shell, methodology tool backgrounds→files)."
          reqArr
              "assets"
              2
              "What must be protected (repo, secrets, user data, session history)."
          reqArr
              "threat_actors"
              1
              "Malicious or careless LLM, compromised dependency, hostile workspace."
          reqArr
              "abuse_paths"
              3
              "Concrete abuse: path traversal, prompt injection via background, executor rw."
          reqArr
              "existing_controls"
              2
              "Permission matrix, mode ro/rw, validation already present."
          reqStr
              "gap_summary"
              "Missing controls ranked."
          optArr
              "hardening_actions"
              2
              "Specific mitigations without over-engineering logging."
          optStr
              "out_of_scope"
              "Threats explicitly not covered this turn." ]
        "Adversarial pass on tool and subagent boundaries."
        [ "Boundary map"
          "Abuse paths"
          "Control gaps"
          "Hardening"
          "Next actions" ]

let toolSpec = toToolCatalogSpec schema