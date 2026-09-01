namespace Wanxiangshu.Sphinx

module DecodePrimitives =
    val isNullish: value: obj -> bool
    val jsType: value: obj -> string
    val isArray: value: obj -> bool
    val asString: value: obj -> Result<string, string>
    val asFloat: value: obj -> Result<float, string>
    val asBool: value: obj -> Result<bool, string>

    val required: name: string -> decoder: (obj -> Result<'value, string>) -> value: obj -> Result<'value, string>

    val optional:
        name: string ->
        decoder: (obj -> Result<'value, string>) ->
        fallback: 'value ->
        value: obj ->
            Result<'value, string>

    val asArray: decoder: (obj -> Result<'value, string>) -> value: obj -> Result<'value list, string>
    val stringList: value: obj -> Result<string list, string>
    val stringMap: value: obj -> Result<Map<string, float>, string>
    val formMap: value: obj -> Result<Map<QuestionForm, float>, string>
    val parseEvidenceKind: string -> EvidenceKind
