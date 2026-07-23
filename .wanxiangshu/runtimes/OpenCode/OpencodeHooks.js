
import { SessionDrivers__Activate_3FD309E6, SessionDrivers__BumpLocalEpochOnHuman_Z6488BD67, SessionDriversKey } from "../Session/Driver.js";
import { decodeUserMessageOrigin } from "./MessageOriginDecoder.js";
import { defaultOf } from "../fable_modules/fable-library-js.5.6.0/Util.js";
import { tryGetValue } from "../fable_modules/fable-library-js.5.6.0/MapUtil.js";
import { FSharpRef } from "../fable_modules/fable-library-js.5.6.0/Types.js";
import { SessionInboxEvent, FifoInbox_$ctor_Z524259A4 } from "../Session/Inbox.js";
import { concat, join } from "../fable_modules/fable-library-js.5.6.0/String.js";
import { empty, choose } from "../fable_modules/fable-library-js.5.6.0/List.js";
import { Operators_IsNull } from "../fable_modules/fable-library-js.5.6.0/FSharp.Core.js";
import { MessageIdModule_create, SessionIdModule_create, PromptKeyRefModule_value } from "../Kernel/Identity.js";
import { createCancellationToken } from "../fable_modules/fable-library-js.5.6.0/Async.js";
import { OpencodeUserMessage, OpencodeModel } from "./OpencodeTypes.js";
import { PromptOutcome } from "../Kernel/Fact.js";

function processUserMessage(gateway, drivers, inboxMap, sessionId, userMsg) {
    const key = new SessionDriversKey(gateway.RuntimeId, sessionId);
    const origin = decodeUserMessageOrigin(userMsg);
    let inbox;
    let matchValue;
    let outArg = defaultOf();
    matchValue = [tryGetValue(inboxMap, sessionId, new FSharpRef(() => outArg, (v) => {
        outArg = v;
    })), outArg];
    if (matchValue[0]) {
        inbox = matchValue[1];
    }
    else {
        const ib_1 = FifoInbox_$ctor_Z524259A4(1000);
        inboxMap.set(sessionId, ib_1);
        inbox = ib_1;
    }
    const text = join("\n", choose((p) => {
        if (Operators_IsNull(p.text)) {
            return undefined;
        }
        else {
            return p.text;
        }
    }, userMsg.parts));
    switch (origin.tag) {
        case 1: {
            inbox.TryPost(new SessionInboxEvent(1, [PromptKeyRefModule_value(origin.fields[0]), text]));
            break;
        }
        case 2: {
            break;
        }
        default: {
            SessionDrivers__BumpLocalEpochOnHuman_Z6488BD67(drivers, key);
            inbox.TryPost(new SessionInboxEvent(0, [origin.fields[0], text]));
            SessionDrivers__Activate_3FD309E6(drivers, key, createCancellationToken());
        }
    }
}

export function handleChatMessage(gateway, drivers, inboxMap, input, outputObj) {
    if (!Operators_IsNull(outputObj) && !Operators_IsNull(outputObj.message)) {
        const msg = outputObj.message;
        const role = msg.role;
        if (role === "user") {
            const sessionId = SessionIdModule_create(input.sessionID);
            let rawModel;
            const matchValue = input.model;
            if (matchValue == null) {
                if (!Operators_IsNull(msg.model)) {
                    const mObj = msg.model;
                    rawModel = (new OpencodeModel(mObj.providerID, mObj.modelID, Operators_IsNull(mObj.variant) ? undefined : mObj.variant));
                }
                else {
                    rawModel = undefined;
                }
            }
            else {
                rawModel = matchValue;
            }
            const rawParts = Operators_IsNull(msg.parts) ? empty() : msg.parts;
            let rawAgent;
            const matchValue_1 = input.agent;
            rawAgent = ((matchValue_1 == null) ? (Operators_IsNull(msg.agent) ? undefined : msg.agent) : matchValue_1);
            processUserMessage(gateway, drivers, inboxMap, sessionId, new OpencodeUserMessage(msg.id, role, input.sessionID, rawAgent, rawModel, rawParts));
        }
    }
}

export function handleToolExecuteBefore(input, output) {
    const argsObj = output.args;
    if (!Operators_IsNull(argsObj)) {
        delete argsObj["warn_tdd"];
        delete argsObj["warn_reuse"];
        delete argsObj["warn_context"];
    }
}

export function handleEvent(gateway, inboxMap, eventObj) {
    if (!Operators_IsNull(eventObj)) {
        const eventType = eventObj.type;
        const properties = eventObj.properties;
        if (!Operators_IsNull(properties) && !Operators_IsNull(properties.sessionID)) {
            const sessionId = SessionIdModule_create(properties.sessionID);
            let matchValue;
            let outArg = defaultOf();
            matchValue = [tryGetValue(inboxMap, sessionId, new FSharpRef(() => outArg, (v) => {
                outArg = v;
            })), outArg];
            if (matchValue[0]) {
                const inbox = matchValue[1];
                switch (eventType) {
                    case "session.idle": {
                        inbox.TryPost(new SessionInboxEvent(6, ["session.idle"]));
                        break;
                    }
                    case "session.status": {
                        const statusType = properties.status.type;
                        inbox.TryPost(new SessionInboxEvent(6, [concat("session.status:", statusType)]));
                        break;
                    }
                    case "message.updated": {
                        const msgObj = properties.info;
                        if (!Operators_IsNull(msgObj)) {
                            if (msgObj.role === "assistant") {
                                const msgId = MessageIdModule_create(msgObj.id);
                                const parentId = Operators_IsNull(msgObj.parentID) ? MessageIdModule_create("") : MessageIdModule_create(msgObj.parentID);
                                const outcome = !Operators_IsNull(msgObj.error) ? (new PromptOutcome(3, ["assistant-error"])) : (new PromptOutcome(0, [msgId]));
                                inbox.TryPost(new SessionInboxEvent(2, [parentId, msgId, outcome]));
                            }
                        }
                        break;
                    }
                    default:
                        undefined;
                }
            }
        }
    }
}

