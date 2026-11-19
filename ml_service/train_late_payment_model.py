
from __future__ import annotations

import json
import os
from argparse import ArgumentParser
from pathlib import Path

import joblib
import numpy as np
import pandas as pd
import psycopg
from dotenv import load_dotenv
from lightgbm import LGBMClassifier
from sklearn.compose import ColumnTransformer
from sklearn.linear_model import LogisticRegression
from sklearn.metrics import precision_recall_fscore_support, roc_auc_score
from sklearn.pipeline import Pipeline
from sklearn.preprocessing import OneHotEncoder, StandardScaler


PROJECT_ROOT = Path(__file__).resolve().parent
DATA_DIR = PROJECT_ROOT / "data"
MODELS_DIR = PROJECT_ROOT / "models"
REPORTS_DIR = PROJECT_ROOT / "reports"

FEATURES = [
    "montant_ttc",
    "jours_delai",
    "mode_paiement",
    "type_client",
    "ville",
    "anciennete_mois",
    "nombre_factures_precedentes",
    "nombre_retards_precedents",
    "taux_retard_precedent",
    "retard_moyen_jours",
]

NUMERICAL_FEATURES = [
    "montant_ttc",
    "jours_delai",
    "anciennete_mois",
    "nombre_factures_precedentes",
    "nombre_retards_precedents",
    "taux_retard_precedent",
    "retard_moyen_jours",
]

CATEGORICAL_FEATURES = ["mode_paiement", "type_client", "ville"]


def make_preprocessor() -> ColumnTransformer:
    return ColumnTransformer(
        transformers=[
            ("numerical", StandardScaler(), NUMERICAL_FEATURES),
            ("categorical", OneHotEncoder(handle_unknown="ignore"), CATEGORICAL_FEATURES),
        ]
    )


def evaluate_model(name: str, model: Pipeline, x_test: pd.DataFrame, y_test: pd.Series) -> tuple[dict[str, float], pd.DataFrame]:
    probabilities = model.predict_proba(x_test)[:, 1]
    predictions = (probabilities >= 0.5).astype(int)
    precision, recall, f1, _ = precision_recall_fscore_support(
        y_test, predictions, average="binary", zero_division=0
    )
    metrics = {
        "model": name,
        "precision": round(float(precision), 4),
        "recall": round(float(recall), 4),
        "f1": round(float(f1), 4),
        "roc_auc": round(float(roc_auc_score(y_test, probabilities)), 4),
    }
    predictions_frame = pd.DataFrame(
        {
            "facture_key": x_test.index,
            "retard_reel": y_test.to_numpy(),
            "prediction_retard": predictions,
            "probabilite_retard": probabilities.round(6),
            "modele": name,
        }
    )
    return metrics, predictions_frame


def generate_synthetic_data() -> pd.DataFrame:
    """Create invoice history with the same columns as ml_training.retard_features."""
    random = np.random.default_rng(42)
    cities = ["Tunis", "Sfax", "Sousse", "Ariana", "Nabeul", "Monastir"]
    payment_modes = ["Virement", "Cheque", "Especes", "Carte"]
    records: list[dict[str, object]] = []
    invoice_key = 1

    for client_key in range(1, 501):
        type_client = "B2C" if client_key % 5 == 0 else "B2B"
        city = cities[(client_key - 1) % len(cities)]
        seniority = int(random.integers(6, 121))
        behavioral_risk = float(random.uniform(0.05, 0.52))
        previous_lates: list[int] = []
        previous_delay_days: list[int] = []
        issue_date = pd.Timestamp("2024-01-01") + pd.Timedelta(days=int(random.integers(0, 80)))

        for _ in range(20):
            issue_date += pd.Timedelta(days=int(random.integers(16, 42)))
            amount = round(float(random.lognormal(mean=7.7, sigma=0.85)), 3)
            payment_mode = str(random.choice(payment_modes, p=[0.47, 0.22, 0.12, 0.19]))
            payment_terms = int(random.choice([30, 45, 60], p=[0.58, 0.27, 0.15]))
            prior_invoices = len(previous_lates)
            prior_lates = sum(previous_lates)
            late_rate = prior_lates / prior_invoices if prior_invoices else 0.0
            average_late_days = float(np.mean(previous_delay_days)) if previous_delay_days else 0.0

            risk = (
                -2.65
                + behavioral_risk * 3.0
                + late_rate * 2.2
                + (0.55 if amount > 10000 else 0.0)
                + (0.32 if payment_mode == "Cheque" else 0.0)
                + (0.18 if type_client == "B2C" else 0.0)
            )
            probability = 1 / (1 + np.exp(-risk))
            late = int(random.random() < probability)
            delay_days = int(random.integers(3, 46)) if late else -int(random.integers(0, 8))
            previous_lates.append(late)
            previous_delay_days.append(delay_days)
            records.append(
                {
                    "facture_key": invoice_key,
                    "client_key": client_key,
                    "date_emission": issue_date,
                    "montant_ttc": amount,
                    "jours_delai": payment_terms,
                    "mode_paiement": payment_mode,
                    "type_client": type_client,
                    "ville": city,
                    "anciennete_mois": seniority,
                    "nombre_factures_precedentes": prior_invoices,
                    "nombre_retards_precedents": prior_lates,
                    "taux_retard_precedent": round(late_rate, 5),
                    "retard_moyen_jours": round(average_late_days, 3),
                    "retard": late,
                }
            )
            invoice_key += 1

    return pd.DataFrame(records).sort_values(["date_emission", "facture_key"]).reset_index(drop=True)


def load_database_data() -> pd.DataFrame:
    load_dotenv(PROJECT_ROOT / ".env")
    database_url = os.getenv("ML_DATABASE_URL")
    if not database_url:
        raise RuntimeError("ML_DATABASE_URL doit etre configure dans ml_service/.env.")
    query = "SELECT * FROM ml_training.retard_features ORDER BY date_emission, facture_key"
    with psycopg.connect(database_url) as connection:
        return pd.read_sql(query, connection)


def main() -> None:
    parser = ArgumentParser()
    parser.add_argument("--source", choices=["database", "synthetic"], default="database")
    args = parser.parse_args()

    for directory in (DATA_DIR, MODELS_DIR, REPORTS_DIR):
        directory.mkdir(exist_ok=True)

    data = load_database_data() if args.source == "database" else generate_synthetic_data()

    if data.empty or data["retard"].nunique() < 2:
        raise RuntimeError("Les donnees d'entrainement doivent contenir les deux classes de paiement.")

    data["date_emission"] = pd.to_datetime(data["date_emission"])
    data.to_csv(DATA_DIR / "retard_features.csv", index=False)

    unique_dates = data["date_emission"].drop_duplicates().sort_values().to_list()
    cutoff_date = unique_dates[int(len(unique_dates) * 0.80)]
    train_data = data[data["date_emission"] < cutoff_date].copy()
    test_data = data[data["date_emission"] >= cutoff_date].copy()
    if train_data.empty or test_data.empty:
        raise RuntimeError("La separation temporelle n'a produit aucun jeu train/test exploitable.")

    x_train = train_data[FEATURES]
    y_train = train_data["retard"].astype(int)
    x_test = test_data.set_index("facture_key")[FEATURES]
    y_test = test_data.set_index("facture_key")["retard"].astype(int)

    logistic_model = Pipeline(
        steps=[
            ("preprocessor", make_preprocessor()),
            ("classifier", LogisticRegression(max_iter=1000, random_state=42)),
        ]
    )
    lightgbm_model = Pipeline(
        steps=[
            ("preprocessor", make_preprocessor()),
            (
                "classifier",
                LGBMClassifier(
                    n_estimators=250,
                    learning_rate=0.05,
                    num_leaves=15,
                    subsample=0.9,
                    colsample_bytree=0.9,
                    random_state=42,
                    n_jobs=-1,
                    verbosity=-1,
                ),
            ),
        ]
    )

    logistic_model.fit(x_train, y_train)
    lightgbm_model.fit(x_train, y_train)
    logistic_metrics, logistic_predictions = evaluate_model("logistic_regression", logistic_model, x_test, y_test)
    lightgbm_metrics, lightgbm_predictions = evaluate_model("lightgbm", lightgbm_model, x_test, y_test)

    # F1 is primary because missed late payments and false alerts both have a business cost.
    candidates = [(logistic_metrics, logistic_model, logistic_predictions), (lightgbm_metrics, lightgbm_model, lightgbm_predictions)]
    best_metrics, best_model, best_predictions = max(
        candidates, key=lambda candidate: (candidate[0]["f1"], candidate[0]["roc_auc"], candidate[0]["recall"])
    )

    metrics = {
        "dataset": {
            "source": args.source,
            "rows": int(len(data)),
            "train_rows": int(len(train_data)),
            "test_rows": int(len(test_data)),
            "late_payment_rate": round(float(data["retard"].mean()), 4),
            "temporal_cutoff_date": cutoff_date.strftime("%Y-%m-%d"),
        },
        "selection_rule": "max F1, then ROC-AUC, then recall",
        "models": [logistic_metrics, lightgbm_metrics],
        "selected_model": best_metrics["model"],
    }
    (REPORTS_DIR / "metrics.json").write_text(json.dumps(metrics, indent=2, ensure_ascii=False), encoding="utf-8")
    pd.DataFrame([logistic_metrics, lightgbm_metrics]).to_csv(REPORTS_DIR / "rapport_evaluation.csv", index=False)
    best_predictions.to_csv(REPORTS_DIR / "predictions_test.csv", index=False)
    joblib.dump(best_model, MODELS_DIR / "retard_model.joblib")

    print(f"Source des donnees: {args.source}")
    print(f"CSV exporte: {DATA_DIR / 'retard_features.csv'}")
    print(f"Modele retenu: {best_metrics['model']}")
    print(f"Metriques: {REPORTS_DIR / 'metrics.json'}")
    print(f"Predictions test: {REPORTS_DIR / 'predictions_test.csv'}")


if __name__ == "__main__":
    main()
