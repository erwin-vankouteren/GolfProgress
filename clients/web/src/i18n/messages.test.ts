import { parse, TYPE, type MessageFormatElement } from "@formatjs/icu-messageformat-parser";
import { describe, expect, it } from "vitest";
import { deviceLocale, localeFor } from "./language";
import { messages, messagesWithValues, plainMessages } from "./messages";
import { nl } from "./nl";

/**
 * `messages` merges two objects, so a name in both would silently replace one message
 * with the other: the Habits pillar once showed an explanation's raw ICU text this way.
 */
describe("messages", () => {
  it("never use a name twice", () => {
    const shared = Object.keys(plainMessages).filter((name) => name in messagesWithValues);
    expect(shared).toEqual([]);
  });

  it("have unique ids, since translations are keyed by them", () => {
    const ids = Object.values(messages).map((message) => message.id);
    const repeated = ids.filter((id, index) => ids.indexOf(id) !== index);
    expect(repeated).toEqual([]);
  });
});

/** The placeholders a message uses, with their kind: "count:plural", "date:argument". */
function placeholders(elements: MessageFormatElement[], found = new Set<string>()): Set<string> {
  for (const element of elements) {
    if (element.type === TYPE.argument || element.type === TYPE.number || element.type === TYPE.date) {
      found.add(`${element.value}:${TYPE[element.type]}`);
    } else if (element.type === TYPE.plural || element.type === TYPE.select) {
      found.add(`${element.value}:${TYPE[element.type]}`);
      for (const option of Object.values(element.options)) placeholders(option.value, found);
    } else if (element.type === TYPE.tag) {
      placeholders(element.children, found);
    }
  }
  return found;
}

describe("Dutch", () => {
  const english = Object.values(messages);

  it("has every message, and nothing else", () => {
    const ids = english.map((message) => message.id);
    expect(ids.filter((id) => !(id in nl))).toEqual([]);
    expect(Object.keys(nl).filter((id) => !ids.includes(id))).toEqual([]);
  });

  it.each(english.map((message) => [message.id, message.defaultMessage as string]))(
    "%s parses and uses the same placeholders as English",
    (id, defaultMessage) => {
      const dutch = nl[id]!;
      expect(dutch.trim()).not.toBe("");
      expect([...placeholders(parse(dutch))].sort()).toEqual([...placeholders(parse(defaultMessage))].sort());
    },
  );
});

describe("the app's language", () => {
  it("follows the profile's choice", () => {
    expect(localeFor("Dutch", "en")).toBe("nl");
    expect(localeFor("English", "nl")).toBe("en");
  });

  it("follows the device without a choice, or with one this version doesn't know", () => {
    expect(localeFor(null, "nl")).toBe("nl");
    expect(localeFor(undefined, "en")).toBe("en");
    expect(localeFor("German", "nl")).toBe("nl");
  });

  it("is the first of the browser's languages the app comes in, else English", () => {
    expect(deviceLocale(["nl-NL", "en-US"])).toBe("nl");
    expect(deviceLocale(["de-DE", "nl-BE", "en"])).toBe("nl");
    expect(deviceLocale(["en-GB", "nl"])).toBe("en");
    expect(deviceLocale(["fr-FR"])).toBe("en");
    expect(deviceLocale([])).toBe("en");
  });
});
