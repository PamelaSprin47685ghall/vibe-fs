
import { Operators_IsNull } from "../fable_modules/fable-library-js.5.6.0/FSharp.Core.js";
import { PromptKeyModule_asString, PromptKeyModule_parse } from "../Session/PromptKey.js";
import { forAll, length, tryPick, exists } from "../fable_modules/fable-library-js.5.6.0/List.js";
import { PromptKeyRefModule_create, TurnIdModule_create, MessageOrigin } from "../Kernel/Identity.js";
import { value } from "../fable_modules/fable-library-js.5.6.0/Option.js";

export function isSyntheticPart(partObj) {
    if (Operators_IsNull(partObj)) {
        return false;
    }
    else {
        const synthetic = partObj.synthetic;
        if (!Operators_IsNull(synthetic)) {
            return synthetic;
        }
        else {
            return false;
        }
    }
}

export function isCompactionPart(partObj) {
    if (Operators_IsNull(partObj)) {
        return false;
    }
    else {
        const pType = partObj.type;
        if (!Operators_IsNull(pType)) {
            return pType === "compaction";
        }
        else {
            return false;
        }
    }
}

export function tryExtractPromptKey(partObj) {
    if (Operators_IsNull(partObj)) {
        return undefined;
    }
    else {
        const meta = partObj.metadata;
        if (Operators_IsNull(meta)) {
            return undefined;
        }
        else {
            const keyStr = meta.wanxiangshu_prompt_key;
            if (!Operators_IsNull(keyStr)) {
                return PromptKeyModule_parse(keyStr);
            }
            else {
                const keyStr2 = meta.promptKey;
                if (!Operators_IsNull(keyStr2)) {
                    return PromptKeyModule_parse(keyStr2);
                }
                else {
                    return undefined;
                }
            }
        }
    }
}

export function decodeUserMessageOrigin(userMsg) {
    const parts = userMsg.parts;
    const hasCompaction = exists(isCompactionPart, parts);
    const pluginKeyOpt = tryPick(tryExtractPromptKey, parts);
    if (pluginKeyOpt == null) {
        if (hasCompaction) {
            return new MessageOrigin(2, []);
        }
        else if ((length(parts) > 0) && forAll(isSyntheticPart, parts)) {
            return new MessageOrigin(2, []);
        }
        else {
            return new MessageOrigin(0, [TurnIdModule_create(userMsg.id)]);
        }
    }
    else {
        return new MessageOrigin(1, [PromptKeyRefModule_create(PromptKeyModule_asString(pluginKeyOpt))]);
    }
}

export function isCompactionAssistant(msg) {
    const matchValue = msg.summary;
    let matchResult;
    if (matchValue != null) {
        if (matchValue) {
            matchResult = 0;
        }
        else {
            matchResult = 1;
        }
    }
    else {
        matchResult = 1;
    }
    switch (matchResult) {
        case 0:
            return true;
        default: {
            const matchValue_1 = msg.agent;
            let matchResult_1;
            if (matchValue_1 != null) {
                if (matchValue_1 === "compaction") {
                    matchResult_1 = 0;
                }
                else {
                    matchResult_1 = 1;
                }
            }
            else {
                matchResult_1 = 1;
            }
            switch (matchResult_1) {
                case 0:
                    return true;
                default:
                    return false;
            }
        }
    }
}

export function isAbortedError(errorObj) {
    if (errorObj != null) {
        const err = value(errorObj);
        if (Operators_IsNull(err)) {
            return false;
        }
        else {
            const name = err.name;
            if (!Operators_IsNull(name)) {
                if (name === "MessageAbortedError") {
                    return true;
                }
                else {
                    return name === "AbortError";
                }
            }
            else {
                return false;
            }
        }
    }
    else {
        return false;
    }
}

