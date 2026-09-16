import importlib.util
import json
import pathlib
import unittest


ROOT = pathlib.Path(__file__).resolve().parents[2]
SPEC = importlib.util.spec_from_file_location(
    "inspect_pge_start_source", ROOT / "tools" / "calibration" / "inspect_pge_start_source.py"
)
MODULE = importlib.util.module_from_spec(SPEC)
assert SPEC.loader is not None
SPEC.loader.exec_module(MODULE)


def ranking(heat_no="1", gate="a"):
    gate_value = {"stringValue": gate} if gate is not None else {"nullValue": None}
    return {
        "mapValue": {
            "fields": {
                heat_no: {
                    "arrayValue": {
                        "values": [{
                            "mapValue": {
                                "fields": {
                                    "no": {"integerValue": heat_no},
                                    "gate": gate_value,
                                }
                            }
                        }]
                    }
                }
            }
        }
    }


def document():
    return {
        "fields": {
            "gates": {
                "mapValue": {
                    "fields": {
                        "enabled": {"booleanValue": True},
                        "data": {
                            "mapValue": {
                                "fields": {
                                    "position": ranking(gate="d"),
                                    "speed_2s": ranking(gate="a"),
                                    "curve_speed": ranking(gate="c"),
                                }
                            }
                        },
                    }
                }
            }
        }
    }


class InspectPgeStartSourceTests(unittest.TestCase):
    def test_raw_rankings_never_parse_as_physical_speeds(self):
        raw = document()
        self.assertEqual("RankingCategory", MODULE.gate_ranking_records(raw, "speed_2s")[0]["classification"])
        self.assertEqual("none", MODULE.gate_ranking_records(raw, "curve_speed")[0]["unit"])
        self.assertEqual(
            {metric: None for metric in MODULE.PHYSICAL_METRICS},
            MODULE.recovered_physical_metrics(raw),
        )

    def test_exact_field_paths_and_units_are_explicit(self):
        rows = MODULE.schema_map(document())
        speed = next(row for row in rows if row["source_field"] == "speed_2s")
        curve = next(row for row in rows if row["source_field"] == "curve_speed")
        self.assertEqual(
            "fields.gates.mapValue.fields.data.mapValue.fields.speed_2s.mapValue.fields."
            "<heat_no>.arrayValue.values[<index>].mapValue.fields.gate.stringValue|nullValue",
            speed["source_path"],
        )
        self.assertIn("curve_speed.mapValue.fields.<heat_no>", curve["source_path"])
        self.assertEqual("none", speed["unit"])
        self.assertEqual("km/h", next(row for row in rows if row["source_field"] == "speed_at_2s_kph")["unit"])

    def test_blank_physical_value_stays_missing(self):
        self.assertIsNone(MODULE.parse_optional_physical_number(""))
        self.assertIsNone(MODULE.parse_optional_physical_number("   "))
        self.assertIsNone(MODULE.parse_optional_physical_number(None))
        self.assertEqual(0.0, MODULE.parse_optional_physical_number("0"))

    def test_null_gate_is_missing_category_not_zero(self):
        raw = document()
        raw["fields"]["gates"]["mapValue"]["fields"]["data"]["mapValue"]["fields"] \
            ["speed_2s"] = ranking(gate=None)
        record = MODULE.gate_ranking_records(raw, "speed_2s")[0]
        self.assertEqual("", record["gate"])

    def test_quantiles_are_deterministic_r7(self):
        expected = {"p10": 1.4, "p25": 2.0, "p50": 3.0, "p75": 4.0, "p90": 4.6}
        self.assertEqual(expected, MODULE.quantiles([5.0, 1.0, 3.0, 2.0, 4.0]))
        self.assertEqual(expected, MODULE.quantiles([4.0, 2.0, 3.0, 5.0, 1.0]))

    def test_restart_attempts_are_separated_by_heat_uid(self):
        groups = MODULE.group_attempts([
            {"match_id": "7734", "heat_uid": "7734_2_0", "rider_id": "1"},
            {"match_id": "7734", "heat_uid": "7734_2_1", "rider_id": "1"},
        ])
        self.assertEqual({("7734", "7734_2_0"), ("7734", "7734_2_1")}, set(groups))

    def test_motoarena_selector_is_exact_and_has_no_aliases(self):
        exact = dict(MODULE.MOTOARENA_SELECTOR)
        self.assertTrue(MODULE.is_exact_motoarena(exact))
        for replacement in ("Toruń", "motoarena im. Mariana Rosego", "Motoarena im. Mariana Rosego "):
            candidate = dict(exact, source_track_label=replacement)
            self.assertFalse(MODULE.is_exact_motoarena(candidate))
        self.assertFalse(MODULE.is_exact_motoarena(dict(exact, league="pgee")))

    def test_output_is_deterministic_utf8_lf_json(self):
        first = MODULE.render_evidence([document()])
        second = MODULE.render_evidence([document()])
        self.assertEqual(first, second)
        self.assertNotIn("\r", first)
        self.assertTrue(first.endswith("\n"))
        parsed = json.loads(first)
        self.assertEqual("UnsupportedNumericByAvailableSource", parsed["classification"])
        self.assertEqual("No recoverable 0–60 m physical speed series found", parsed["series_0_60m_or_0_3s"])


if __name__ == "__main__":
    unittest.main()
