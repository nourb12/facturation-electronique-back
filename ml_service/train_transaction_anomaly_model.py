"""Train and compare unsupervised transaction anomaly detectors.

The script accepts the ULB/Kaggle ``creditcard.csv`` dataset (Time, V1..V28,
Amount, Class).  ``Class`` is deliberately excluded from model training and
used only to evaluate the detectors afterwards.  A deterministic synthetic
source is included so the full TuniFlow ML demo can run without a Kaggle key.
"""

from __future__ import annotations

import json
from argparse import ArgumentParser
from pathlib import Path

import joblib
import numpy as np
import pandas as pd
from sklearn.ensemble import IsolationForest
from sklearn.metrics import (
    average_precision_score,
    precision_recall_fscore_support,
    precision_score,
    roc_auc_score,
)
from sklearn.neighbors import LocalOutlierFactor
from sklearn.pipeline import Pipeline
from sklearn.decomposition import PCA
from sklearn.preprocessing import StandardScaler


PROJECT_ROOT = Path(__file__).resolve().parent
DATA_DIR = PROJECT_ROOT / "data"
MODELS_DIR = PROJECT_ROOT / "models"
REPORTS_DIR = PROJECT_ROOT / "reports"
FEATURES = ["Time", *[f"V{index}" for index in range(1, 29)], "Amount"]
CONTAMINATION = 0.02
RANDOM_STATE = 42


def generate_synthetic_data(rows: int = 18_000) -> pd.DataFrame:
    """Create an ULB-compatible, intentionally imbalanced demonstration set."""
    random = np.random.default_rng(RANDOM_STATE)
    fraud_count = max(1, int(rows * 0.017))
    labels = np.zeros(rows, dtype=int)
    labels[random.choice(rows, size=fraud_count, replace=False)] = 1

    records: dict[str, np.ndarray] = {
        "Time": random.uniform(0, 172_800, size=rows),
        "Amount": random.lognormal(mean=4.2, sigma=1.05, size=rows).clip(0.5, 12_000),
    }
    for index in range(1, 29):
        records[f"V{index}"] = random.normal(0, 1, size=rows)

    fraud_mask = labels == 1
    # The shifts emulate unusual combinations, not a business rule per field.
    for index, shift in ((1, -4.2), (3, 3.7), (4, 2.8), (10, -3.4), (14, -4.0), (17, -3.0)):
        records[f"V{index}"][fraud_mask] += random.normal(shift, 0.85, size=fraud_count)
    records["Amount"][fraud_mask] *= random.uniform(7, 26, size=fraud_count)
    records["Time"][fraud_mask] = random.choice(
        np.concatenate([random.uniform(0, 9_000, fraud_count), random.uniform(163_000, 172_800, fraud_count)]),
        size=fraud_count,
        replace=False,
    )

    frame = pd.DataFrame(records)
    frame["Class"] = labels
    return frame.sort_values("Time").reset_index(drop=True)


def load_data(source: str, input_path: Path | None, rows: int) -> tuple[pd.DataFrame, str]:
    if input_path:
        data = pd.read_csv(input_path)
        return data, f"fichier:{input_path.name}"
    if source == "synthetic":
        return generate_synthetic_data(rows), "synthetic"
    raise RuntimeError("Indiquez --input chemin/vers/creditcard.csv ou utilisez --source synthetic.")


def validate_data(data: pd.DataFrame) -> pd.DataFrame:
    missing = sorted(set([*FEATURES, "Class"]) - set(data.columns))
    if missing:
        raise RuntimeError(f"Colonnes manquantes: {', '.join(missing)}")
    result = data[[*FEATURES, "Class"]].copy()
    result = result.apply(pd.to_numeric, errors="coerce").dropna()
    result["Class"] = result["Class"].astype(int)
    if result["Class"].nunique() < 2:
        raise RuntimeError("La colonne Class doit contenir les transactions normales et les fraudes connues.")
    return result.sort_values("Time").reset_index(drop=True)


def make_detector(name: str) -> Pipeline:
    if name == "isolation_forest":
        detector = IsolationForest(
            n_estimators=300,
            contamination=CONTAMINATION,
            random_state=RANDOM_STATE,
            n_jobs=-1,
        )
    else:
        detector = LocalOutlierFactor(
            n_neighbors=35,
            contamination=CONTAMINATION,
            novelty=True,
            n_jobs=-1,
        )
    return Pipeline([("scale", StandardScaler()), ("detector", detector)])


def normalize_scores(raw_scores: np.ndarray, lower: float, upper: float) -> np.ndarray:
    denominator = max(upper - lower, 1e-9)
    return np.clip((raw_scores - lower) / denominator, 0, 1)


def evaluate_detector(
    name: str,
    model: Pipeline,
    x_test: pd.DataFrame,
    y_test: pd.Series,
    prediction_ids: pd.Series,
) -> tuple[dict[str, float | str], pd.DataFrame, dict[str, float]]:
    # score_samples is lower for more unusual points; negate it for a human score where 1 is suspicious.
    raw_scores = -model.score_samples(x_test)
    lower, upper = np.quantile(raw_scores, [0.01, 0.99])
    anomaly_scores = normalize_scores(raw_scores, float(lower), float(upper))
    predictions = (model.predict(x_test) == -1).astype(int)
    precision, recall, f1, _ = precision_recall_fscore_support(
        y_test, predictions, average="binary", zero_division=0
    )
    top_k = max(1, int(y_test.sum()))
    top_indexes = np.argsort(anomaly_scores)[-top_k:]
    metrics: dict[str, float | str] = {
        "model": name,
        "precision": round(float(precision), 4),
        "recall": round(float(recall), 4),
        "f1": round(float(f1), 4),
        "roc_auc": round(float(roc_auc_score(y_test, anomaly_scores)), 4),
        "pr_auc": round(float(average_precision_score(y_test, anomaly_scores)), 4),
        "precision_at_k": round(float(precision_score(y_test.iloc[top_indexes], np.ones(top_k), zero_division=0)), 4),
    }
    predictions_frame = pd.DataFrame(
        {
            "transaction_id": prediction_ids.to_numpy(),
            "time": x_test["Time"].to_numpy().round(3),
            "amount": x_test["Amount"].to_numpy().round(3),
            "fraud_known_for_evaluation": y_test.to_numpy(),
            "anomaly_detected": predictions,
            "anomaly_score": anomaly_scores.round(6),
            "model": name,
        }
    )
    return metrics, predictions_frame, {"score_lower": float(lower), "score_upper": float(upper)}


def main() -> None:
    parser = ArgumentParser()
    parser.add_argument("--source", choices=["synthetic"], default="synthetic")
    parser.add_argument("--input", type=Path, help="Chemin vers le creditcard.csv ULB/Kaggle")
    parser.add_argument("--rows", type=int, default=18_000, help="Taille de la source synthétique")
    args = parser.parse_args()

    for directory in (DATA_DIR, MODELS_DIR, REPORTS_DIR):
        directory.mkdir(exist_ok=True)

    data, source_name = load_data(args.source, args.input, args.rows)
    data = validate_data(data)
    data.insert(0, "transaction_id", [f"TXN-{index:06d}" for index in range(1, len(data) + 1)])
    data.to_csv(DATA_DIR / "transaction_anomaly_features.csv", index=False)

    # Temporal split avoids evaluating on rows that are randomly mixed with the training period.
    cutoff = data["Time"].quantile(0.80)
    train_data = data[data["Time"] < cutoff].copy()
    test_data = data[data["Time"] >= cutoff].copy()
    if train_data.empty or test_data.empty:
        raise RuntimeError("La séparation temporelle n'a produit aucun jeu train/test exploitable.")

    x_train, x_test = train_data[FEATURES], test_data[FEATURES]
    y_test = test_data["Class"].astype(int)
    candidates: list[tuple[dict[str, float | str], Pipeline, pd.DataFrame, dict[str, float]]] = []

    for name in ("isolation_forest", "local_outlier_factor"):
        model = make_detector(name)
        # The unsupervised fit receives only feature columns; Class is intentionally absent.
        model.fit(x_train)
        metrics, predictions, calibration = evaluate_detector(
            name, model, x_test, y_test, test_data["transaction_id"]
        )
        candidates.append((metrics, model, predictions, calibration))

    # PR-AUC is primary for rare anomalies, then F1 and recall for operational triage.
    best_metrics, best_model, best_predictions, calibration = max(
        candidates,
        key=lambda candidate: (
            float(candidate[0]["pr_auc"]),
            float(candidate[0]["f1"]),
            float(candidate[0]["recall"]),
        ),
    )

    pca = PCA(n_components=2, random_state=RANDOM_STATE)
    pca_input = StandardScaler().fit_transform(x_test)
    pca_points = pca.fit_transform(pca_input)
    pca_frame = best_predictions[["transaction_id", "anomaly_score", "anomaly_detected"]].copy()
    pca_frame["pca_x"] = pca_points[:, 0].round(6)
    pca_frame["pca_y"] = pca_points[:, 1].round(6)
    pca_frame["fraud_known_for_evaluation"] = y_test.to_numpy()

    metrics_report = {
        "dataset": {
            "source": source_name,
            "rows": int(len(data)),
            "train_rows": int(len(train_data)),
            "test_rows": int(len(test_data)),
            "known_fraud_rate": round(float(data["Class"].mean()), 4),
            "temporal_cutoff": round(float(cutoff), 3),
        },
        "training": {
            "supervision": "non_supervisee",
            "label_usage": "Class est exclue de l'entraînement; elle sert uniquement à l'évaluation.",
            "contamination": CONTAMINATION,
        },
        "selection_rule": "max PR-AUC, puis F1, puis recall",
        "models": [candidate[0] for candidate in candidates],
        "selected_model": best_metrics["model"],
    }
    (REPORTS_DIR / "anomaly_metrics.json").write_text(
        json.dumps(metrics_report, indent=2, ensure_ascii=False), encoding="utf-8"
    )
    pd.DataFrame([candidate[0] for candidate in candidates]).to_csv(
        REPORTS_DIR / "anomaly_evaluation.csv", index=False
    )
    best_predictions.sort_values("anomaly_score", ascending=False).to_csv(
        REPORTS_DIR / "anomaly_predictions.csv", index=False
    )
    pca_frame.sample(min(2_000, len(pca_frame)), random_state=RANDOM_STATE).to_csv(
        REPORTS_DIR / "anomaly_pca.csv", index=False
    )
    joblib.dump(
        {
            "model": best_model,
            "features": FEATURES,
            "selected_model": best_metrics["model"],
            "calibration": calibration,
            "contamination": CONTAMINATION,
        },
        MODELS_DIR / "transaction_anomaly_model.joblib",
    )

    print(f"Source des données: {source_name}")
    print(f"CSV d'entraînement: {DATA_DIR / 'transaction_anomaly_features.csv'}")
    print(f"Modèle retenu: {best_metrics['model']}")
    print(f"Rapport: {REPORTS_DIR / 'anomaly_metrics.json'}")
    print(f"Prédictions: {REPORTS_DIR / 'anomaly_predictions.csv'}")


if __name__ == "__main__":
    main()
