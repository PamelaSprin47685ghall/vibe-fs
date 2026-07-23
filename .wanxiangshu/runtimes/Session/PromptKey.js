
import { Record, Union } from "../fable_modules/fable-library-js.5.6.0/Types.js";
import { int32_type, record_type, option_type, string_type, union_type } from "../fable_modules/fable-library-js.5.6.0/Reflection.js";
import { printf, toText, concat, split, isNullOrWhiteSpace } from "../fable_modules/fable-library-js.5.6.0/String.js";
import { FSharpResult$2 } from "../fable_modules/fable-library-js.5.6.0/Result.js";
import { item, equalsWith } from "../fable_modules/fable-library-js.5.6.0/Array.js";
import { unescapeDataString, int32ToString, escapeDataString, defaultOf } from "../fable_modules/fable-library-js.5.6.0/Util.js";
import { MessageIdModule_create, TurnIdModule_create, SessionIdModule_create, MessageIdModule_value, TurnIdModule_value, SessionIdModule_value, MessageId_$reflection, TurnId_$reflection, SessionId_$reflection } from "../Kernel/Identity.js";
import { parse } from "../fable_modules/fable-library-js.5.6.0/Int32.js";

export class PromptPurpose extends Union {
    constructor(tag, fields) {
        super();
        this.tag = tag;
        this.fields = fields;
    }
    cases() {
        return ["ContinueTodo", "RetryTurn", "SwitchModel", "ReviewChanges", "RunChild", "ReturnToParent"];
    }
}

export function PromptPurpose_$reflection() {
    return union_type("Wanxiangshu.Next.Session.PromptPurpose", [], PromptPurpose, () => [[], [], [], [], [], []]);
}

export function PromptPurposeModule_toStableString(purpose) {
    switch (purpose.tag) {
        case 1:
            return "RetryTurn";
        case 2:
            return "SwitchModel";
        case 3:
            return "ReviewChanges";
        case 4:
            return "RunChild";
        case 5:
            return "ReturnToParent";
        default:
            return "ContinueTodo";
    }
}

export class Model extends Record {
    constructor(ProviderId, ModelId, Variant) {
        super();
        this.ProviderId = ProviderId;
        this.ModelId = ModelId;
        this.Variant = Variant;
    }
}

export function Model_$reflection() {
    return record_type("Wanxiangshu.Next.Session.Model", [], Model, () => [["ProviderId", string_type], ["ModelId", string_type], ["Variant", option_type(string_type)]]);
}

export function ModelModule_create(providerId, modelId, variant) {
    return new Model(providerId, modelId, variant);
}

export function ModelModule_ofString(s) {
    if (isNullOrWhiteSpace(s)) {
        return new FSharpResult$2(1, ["Model string cannot be empty"]);
    }
    else {
        const parts = split(s, ["/"], undefined, 0);
        if (parts.some(isNullOrWhiteSpace)) {
            return new FSharpResult$2(1, [concat("Model string contains empty segment: \'", s, "\'")]);
        }
        else if (!equalsWith((x, y) => (x === y), parts, defaultOf()) && (parts.length === 2)) {
            return new FSharpResult$2(0, [new Model(item(0, parts), item(1, parts), undefined)]);
        }
        else if (!equalsWith((x_1, y_1) => (x_1 === y_1), parts, defaultOf()) && (parts.length === 3)) {
            const v = item(2, parts);
            return new FSharpResult$2(0, [new Model(item(0, parts), item(1, parts), v)]);
        }
        else {
            return new FSharpResult$2(1, [concat("Invalid model string format (expected provider/model or provider/model/variant): \'", s, "\'")]);
        }
    }
}

export function ModelModule_toStableString(m) {
    const matchValue = m.Variant;
    if (matchValue == null) {
        return toText(printf("%s/%s"))(m.ProviderId)(m.ModelId);
    }
    else {
        const v = matchValue;
        return toText(printf("%s/%s/%s"))(m.ProviderId)(m.ModelId)(v);
    }
}

export class PromptKey extends Record {
    constructor(SessionId, TurnId, Purpose, Model, Attempt, TriggerMessageId, PayloadHash) {
        super();
        this.SessionId = SessionId;
        this.TurnId = TurnId;
        this.Purpose = Purpose;
        this.Model = Model;
        this.Attempt = (Attempt | 0);
        this.TriggerMessageId = TriggerMessageId;
        this.PayloadHash = PayloadHash;
    }
}

export function PromptKey_$reflection() {
    return record_type("Wanxiangshu.Next.Session.PromptKey", [], PromptKey, () => [["SessionId", SessionId_$reflection()], ["TurnId", TurnId_$reflection()], ["Purpose", PromptPurpose_$reflection()], ["Model", option_type(Model_$reflection())], ["Attempt", int32_type], ["TriggerMessageId", option_type(MessageId_$reflection())], ["PayloadHash", string_type]]);
}

export function PromptKeyModule_create(sessionId, turnId, purpose, model, attempt, triggerMessageId, payloadHash) {
    return new PromptKey(sessionId, turnId, purpose, model, attempt, triggerMessageId, payloadHash);
}

export function PromptKeyModule_sessionId(pk) {
    return pk.SessionId;
}

export function PromptKeyModule_turnId(pk) {
    return pk.TurnId;
}

export function PromptKeyModule_purpose(pk) {
    return pk.Purpose;
}

export function PromptKeyModule_model(pk) {
    return pk.Model;
}

export function PromptKeyModule_attempt(pk) {
    return pk.Attempt | 0;
}

export function PromptKeyModule_triggerMessageId(pk) {
    return pk.TriggerMessageId;
}

export function PromptKeyModule_payloadHash(pk) {
    return pk.PayloadHash;
}

function PromptKeyModule_escapeField(s) {
    return escapeDataString(s);
}

export function PromptKeyModule_asString(pk) {
    const sId = PromptKeyModule_escapeField(SessionIdModule_value(pk.SessionId));
    const tId = PromptKeyModule_escapeField(TurnIdModule_value(pk.TurnId));
    const purp = PromptKeyModule_escapeField(PromptPurposeModule_toStableString(pk.Purpose));
    let mdl;
    const matchValue = pk.Model;
    mdl = ((matchValue == null) ? "default" : PromptKeyModule_escapeField(ModelModule_toStableString(matchValue)));
    const att = PromptKeyModule_escapeField(int32ToString(pk.Attempt));
    let trig;
    const matchValue_1 = pk.TriggerMessageId;
    trig = ((matchValue_1 == null) ? "none" : PromptKeyModule_escapeField(MessageIdModule_value(matchValue_1)));
    const payload = PromptKeyModule_escapeField(pk.PayloadHash);
    return toText(printf("%s:%s:%s:%s:%s:%s:%s"))(sId)(tId)(purp)(mdl)(att)(trig)(payload);
}

export function PromptKeyModule_parse(s) {
    if (isNullOrWhiteSpace(s)) {
        return undefined;
    }
    else {
        const parts = split(s, [":"], undefined, 0);
        if (parts.length !== 7) {
            return undefined;
        }
        else {
            try {
                const unescape = unescapeDataString;
                const sId = SessionIdModule_create(unescape(item(0, parts)));
                const tId = TurnIdModule_create(unescape(item(1, parts)));
                const purpStr = unescape(item(2, parts));
                const purp = (purpStr === "ContinueTodo") ? (new PromptPurpose(0, [])) : ((purpStr === "RetryTurn") ? (new PromptPurpose(1, [])) : ((purpStr === "SwitchModel") ? (new PromptPurpose(2, [])) : ((purpStr === "ReviewChanges") ? (new PromptPurpose(3, [])) : ((purpStr === "RunChild") ? (new PromptPurpose(4, [])) : (new PromptPurpose(5, []))))));
                const mdlStr = unescape(item(3, parts));
                let mdl;
                if (mdlStr === "default") {
                    mdl = undefined;
                }
                else {
                    const matchValue = ModelModule_ofString(mdlStr);
                    mdl = ((matchValue.tag === 1) ? undefined : matchValue.fields[0]);
                }
                const att = parse(unescape(item(4, parts)), 511, false, 32) | 0;
                const trigStr = unescape(item(5, parts));
                return new PromptKey(sId, tId, purp, mdl, att, (trigStr === "none") ? undefined : MessageIdModule_create(unescape(trigStr)), unescape(item(6, parts)));
            }
            catch (matchValue_1) {
                return undefined;
            }
        }
    }
}

