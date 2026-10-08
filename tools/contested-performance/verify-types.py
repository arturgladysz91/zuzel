"""End-to-end type preservation check using C# capture output."""
import json
from pathlib import Path
import sys

values = json.loads(Path(sys.argv[1]).read_text(encoding="utf-8"))
assert type(values["integer"]) is int and values["integer"] == 1
assert type(values["boolean"]) is bool and values["boolean"] is True
assert values["integer_text"] == "string:1"
assert values["boolean_text"] == "string:True"
assert values["enum_text"] == "string:Hold"
assert values["enum"].startswith("enum:CoreSim.Interactions.InteractionResponse:")
assert values["character"] == "char:0031"
assert values["character_text"] == "string:1"
assert values["decimal"] == "decimal:0000000A:00000000:00000000:00010000"
assert values["decimal_text"] == "string:1.0"
assert values["float"] == "float:3F800000"
assert values["double"] == "double:3FF0000000000000"
assert values["null"] is None
for a, b in [("integer", "integer_text"), ("boolean", "boolean_text"),
             ("enum", "enum_text"), ("character", "character_text"),
             ("decimal", "decimal_text")]:
    assert values[a] != values[b], f"Lost capture type: {a} versus {b}"
print("Real C# capture type preservation: PASS")
