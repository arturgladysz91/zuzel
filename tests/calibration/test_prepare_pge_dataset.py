import importlib.util
import pathlib
import unittest


ROOT = pathlib.Path(__file__).resolve().parents[2]
SPEC = importlib.util.spec_from_file_location(
    "prepare_pge_dataset", ROOT / "tools" / "calibration" / "prepare_pge_dataset.py"
)
MODULE = importlib.util.module_from_spec(SPEC)
assert SPEC.loader is not None
SPEC.loader.exec_module(MODULE)


def source(**overrides):
    row = {
        "match_id": "1",
        "heat_no": "2",
        "heat_uid": "1_2_0",
        "rider_id": "7",
        "gate": "a",
        "points": "0",
        "result_position": "4",
        "telemetry_present": "1",
        "result_present": "1",
        "detail_time": "62.00",
        "detail_l1_time": "17.00",
        "detail_l2_time": "15.00",
        "detail_l3_time": "15.00",
        "detail_l4_time": "15.00",
        "detail_max_speed": "115.0",
        "detail_distance": "1426.0",
        "detail_motorbike": "B",
        "gate_rank_position": "1",
        "gate_rank_speed_2s": "0",
        "gate_rank_curve_speed": "1",
    }
    row.update(overrides)
    return row


MATCH = {
    "match_id": "1",
    "date": "2026-01-01T00:00:00+00:00",
    "season": "2026",
    "league": "PGEE",
    "track": "Track label",
}


class PrepareDatasetTests(unittest.TestCase):
    def test_league_filter_is_exact(self):
        selected = MODULE.filter_pgee_matches(
            [
                {"match_id": "1", "league": "PGEE"},
                {"match_id": "2", "league": "pgee"},
                {"match_id": "3", "league": "PGEE "},
            ]
        )
        self.assertEqual(["1"], list(selected))

    def test_blank_numeric_is_not_zero(self):
        row = MODULE.normalize_row(source(detail_l2_time=""), MATCH)
        self.assertEqual("", row["l2_time_s"])
        self.assertEqual(1, row["audit_only"])

    def test_complete_requires_all_four_laps(self):
        row = MODULE.normalize_row(source(detail_l4_time=""), MATCH)
        self.assertEqual(0, row["complete_telemetry"])

    def test_heat_time_consistency_uses_point_zero_five_seconds(self):
        boundary = MODULE.normalize_row(source(detail_time="62.05"), MATCH)
        outside = MODULE.normalize_row(source(detail_time="62.051"), MATCH)
        self.assertEqual(1, boundary["complete_telemetry"])
        self.assertEqual(0, outside["complete_telemetry"])

    def test_zero_points_is_a_legal_clean_result(self):
        row = MODULE.normalize_row(source(points="0"), MATCH)
        self.assertEqual(1, row["complete_telemetry"])
        self.assertEqual(1, row["clean_physics"])

    def test_missing_result_is_not_clean(self):
        row = MODULE.normalize_row(source(result_present="0"), MATCH)
        self.assertEqual(0, row["clean_physics"])
        self.assertEqual(1, row["audit_only"])

    def test_telemetry_only_and_result_only_rows_are_audit_only(self):
        telemetry_only = MODULE.normalize_row(source(result_present="0"), MATCH)
        result_only = MODULE.normalize_row(
            source(
                telemetry_present="0",
                detail_time="",
                detail_l1_time="",
                detail_l2_time="",
                detail_l3_time="",
                detail_l4_time="",
                detail_max_speed="",
                detail_distance="",
            ),
            MATCH,
        )
        self.assertEqual(1, telemetry_only["audit_only"])
        self.assertEqual(1, result_only["audit_only"])

    def test_flying_lap_ten_percent_filter_is_inclusive(self):
        boundary = MODULE.normalize_row(
            source(detail_time="62.00", detail_l2_time="13.50", detail_l3_time="15.00", detail_l4_time="16.50"),
            MATCH,
        )
        outside = MODULE.normalize_row(
            source(detail_time="61.99", detail_l2_time="13.49", detail_l3_time="15.00", detail_l4_time="16.50"),
            MATCH,
        )
        self.assertEqual(1, boundary["clean_physics"])
        self.assertEqual(0, outside["clean_physics"])
        self.assertEqual(1, outside["eventful"])
        self.assertEqual(0, outside["audit_only"])

    def test_heat_uid_preserves_restarted_attempt_identity(self):
        first = MODULE.normalize_row(source(heat_uid="7734_2_0"), MATCH)
        second = MODULE.normalize_row(source(heat_uid="7734_2_1"), MATCH)
        self.assertNotEqual(first["heat_uid"], second["heat_uid"])
        self.assertEqual(first["heat_no"], second["heat_no"])

    def test_derived_metrics_use_required_formulas(self):
        row = MODULE.normalize_row(source(), MATCH)
        self.assertAlmostEqual(15.0, float(row["flying_lap_median_s"]))
        self.assertAlmostEqual(2.0, float(row["l1_penalty_s"]))
        self.assertAlmostEqual(1426.0 / 62.0, float(row["average_speed_mps"]), places=9)

    def test_gate_rank_fields_remain_categorical_metadata(self):
        row = MODULE.normalize_row(source(), MATCH)
        self.assertEqual("0", row["gate_rank_speed_2s"])
        self.assertEqual("1", row["gate_rank_curve_speed"])
        self.assertNotIn("speed_2s", MODULE.METRICS)
        self.assertNotIn("curve_speed", MODULE.METRICS)

    def test_raw_gate_speed_fields_validate_as_ranked_gate_categories(self):
        ranking = {
            "mapValue": {
                "fields": {
                    "1": {
                        "arrayValue": {
                            "values": [
                                {
                                    "mapValue": {
                                        "fields": {
                                            "no": {"integerValue": "1"},
                                            "gate": {"stringValue": "a"},
                                        }
                                    }
                                }
                            ]
                        }
                    }
                }
            }
        }
        document = {
            "responses": {
                "match": {
                    "fields": {
                        "match": {
                            "mapValue": {
                                "fields": {
                                    "track": {"mapValue": {"fields": {"id": {"integerValue": "19"}}}}
                                }
                            }
                        }
                    }
                },
                "telemetry": {
                    "fields": {
                        "gates": {
                            "mapValue": {
                                "fields": {
                                    "data": {
                                        "mapValue": {
                                            "fields": {"speed_2s": ranking, "curve_speed": ranking}
                                        }
                                    }
                                }
                            }
                        }
                    }
                },
            }
        }
        MODULE.validate_raw_match_structure(document)
        gate = document["responses"]["telemetry"]["fields"]["gates"]
        gate["mapValue"]["fields"]["data"]["mapValue"]["fields"]["speed_2s"] \
            ["mapValue"]["fields"]["1"]["arrayValue"]["values"][0] \
            ["mapValue"]["fields"]["gate"]["stringValue"] = "83.4"
        with self.assertRaises(ValueError):
            MODULE.validate_raw_match_structure(document)

    def test_quantile_and_fold_are_deterministic(self):
        self.assertEqual(2.5, MODULE.linear_quantile([0.0, 10.0], 0.25))
        self.assertEqual(MODULE.development_fold("7734"), MODULE.development_fold("7734"))
        self.assertIn(MODULE.development_fold("7734"), range(5))


if __name__ == "__main__":
    unittest.main()
