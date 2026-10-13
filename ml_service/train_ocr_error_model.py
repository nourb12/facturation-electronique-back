"""Train the OCR extraction-quality model used by the mobile review flow.

The data are deliberately synthetic: each clean invoice is generated first, then
typical OCR defects are injected and kept as labels. This makes the experiment
reproducible while clearly documenting that it is not yet trained on production
corrections.
"""

from __future__ import annotations

import argparse
import json
from pathlib import Path

import joblib
import numpy as np
import pandas as pd
from sklearn.compose import ColumnTransformer
from sklearn.linear_model import LogisticRegression
from sklearn.metrics import average_precision_score, f1_score, precision_score, recall_score, roc_auc_score
from sklearn.model_selection import train_test_split
from sklearn.pipeline import Pipeline
from sklearn.preprocessing import OneHotEncoder, StandardScaler

try:
    from lightgbm import LGBMClassifier
except ImportError as exc:  # requirements.txt includes LightGBM
    raise SystemExit("LightGBM est requis. Installez les dépendances avec pip install -r requirements.txt.") from exc


ROOT = Path(__file__).resolve().parent
DATA = ROOT / "data"
REPORTS = ROOT / "reports"
MODELS = ROOT / "models"
NUMERIC_FEATURES = [
    "ocr_confidence",
    "missing_fields_count",
    "character_error_count",
    "invalid_date",
    "amount_parse_error",
    "calculation_inconsistent",
    "supplier_missing",
    "tax_id_invalid",
]
CATEGORICAL_FEATURES = ["document_type"]
FEATURES = NUMERIC_FEATURES + CATEGORICAL_FEATURES


def generate_dataset(rows: int, seed: int) -> pd.DataFrame:
    rng = np.random.default_rng(seed)
    document_types = np.array(["facture", "avoir", "bon_commande", "paiement_recu"])
    records: list[dict[str, object]] = []

    for index in range(rows):
        document_type = str(rng.choice(document_types, p=[0.67, 0.08, 0.13, 0.12]))
        has_error = rng.random() < 0.27
        character_errors = 0
        missing = 0
        invalid_date = 0
        amount_parse_error = 0
        calculation_inconsistent = 0
        supplier_missing = 0
        tax_id_invalid = 0
        issues: list[str] = []

        if has_error:
            defect_count = int(rng.integers(1, 4))
            defects = rng.choice(
                ["character", "missing", "date", "amount", "calculation", "supplier", "tax_id"],
                size=defect_count,
                replace=False,
            )
            for defect in defects:
                if defect == "character":
                    character_errors += int(rng.integers(1, 3))
                    issues.append("Caractère confondu dans une valeur extraite")
                elif defect == "missing":
                    missing += 1
                    issues.append("Champ obligatoire manquant")
                elif defect == "date":
                    invalid_date = 1
                    issues.append("Format de date invalide")
                elif defect == "amount":
                    amount_parse_error = 1
                    issues.append("Format de montant invalide")
                elif defect == "calculation":
                    calculation_inconsistent = 1
                    issues.append("Incohérence de calcul HT + TVA ≠ TTC")
                elif defect == "supplier":
                    supplier_missing = 1
                    missing += 1
                    issues.append("Fournisseur non identifié")
                elif defect == "tax_id":
                    tax_id_invalid = 1
                    issues.append("Matricule fiscal invalide")

        confidence = 0.96 - (character_errors * 0.17) - (missing * 0.20) - (invalid_date * 0.22) - (amount_parse_error * 0.24) - (calculation_inconsistent * 0.16)
        confidence += float(rng.normal(0, 0.035))
        confidence = float(np.clip(confidence, 0.05, 0.99))

        records.append(
            {
                "invoice_id": f"OCR-{index + 1:06d}",
                "document_type": document_type,
                "ocr_confidence": round(confidence, 4),
                "missing_fields_count": missing,
                "character_error_count": character_errors,
                "invalid_date": invalid_date,
                "amount_parse_error": amount_parse_error,
                "calculation_inconsistent": calculation_inconsistent,
                "supplier_missing": supplier_missing,
                "tax_id_invalid": tax_id_invalid,
                "ocr_error": int(has_error),
                "injected_issues": " | ".join(issues) if issues else "",
            }
        )

    return pd.DataFrame(records)


def metrics(y_true: pd.Series, probabilities: np.ndarray) -> dict[str, float]:
    threshold = 0.42  # favour recall: a correct invoice can be reviewed, a defect must not be missed
    predicted = (probabilities >= threshold).astype(int)
    return {
        "precision": round(float(precision_score(y_true, predicted, zero_division=0)), 4),
        "recall": round(float(recall_score(y_true, predicted, zero_division=0)), 4),
        "f1": round(float(f1_score(y_true, predicted, zero_division=0)), 4),
        "roc_auc": round(float(roc_auc_score(y_true, probabilities)), 4),
        "pr_auc": round(float(average_precision_score(y_true, probabilities)), 4),
        "threshold": threshold,
    }


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--rows", type=int, default=4500)
    parser.add_argument("--seed", type=int, default=202608)
    args = parser.parse_args()

    DATA.mkdir(exist_ok=True)
    REPORTS.mkdir(exist_ok=True)
    MODELS.mkdir(exist_ok=True)

    df = generate_dataset(args.rows, args.seed)
    df.to_csv(DATA / "ocr_error_features.csv", index=False)

    train, test = train_test_split(df, test_size=0.2, random_state=args.seed, stratify=df["ocr_error"])
    preprocessor = ColumnTransformer(
        [
            ("numeric", StandardScaler(), NUMERIC_FEATURES),
            ("categorical", OneHotEncoder(handle_unknown="ignore"), CATEGORICAL_FEATURES),
        ]
    )
    candidates = {
        "logistic_regression": LogisticRegression(max_iter=1500, class_weight="balanced", random_state=args.seed),
        "lightgbm": LGBMClassifier(
            n_estimators=240,
            learning_rate=0.045,
            num_leaves=19,
            class_weight="balanced",
            random_state=args.seed,
            verbosity=-1,
        ),
    }
    result_rows: list[dict[str, object]] = []
    fitted: dict[str, Pipeline] = {}
    for name, classifier in candidates.items():
        pipeline = Pipeline([("preprocessor", preprocessor), ("classifier", classifier)])
        pipeline.fit(train[FEATURES], train["ocr_error"])
        probabilities = pipeline.predict_proba(test[FEATURES])[:, 1]
        result_rows.append({"model": name, **metrics(test["ocr_error"], probabilities)})
        fitted[name] = pipeline

    evaluation = pd.DataFrame(result_rows).sort_values(["recall", "f1", "pr_auc"], ascending=False)
    winner_name = str(evaluation.iloc[0]["model"])
    winner = fitted[winner_name]
    probabilities = winner.predict_proba(test[FEATURES])[:, 1]
    predictions = test[["invoice_id", "ocr_error", "injected_issues"]].copy()
    predictions["error_probability"] = np.round(probabilities, 4)
    predictions["review_status"] = np.where(probabilities >= 0.70, "suspect", np.where(probabilities >= 0.42, "a_revoir", "ok"))
    predictions.to_csv(REPORTS / "ocr_error_predictions.csv", index=False)
    evaluation.to_csv(REPORTS / "ocr_error_evaluation.csv", index=False)

    report = {
        "data_source": "synthetic_injected_ocr_errors",
        "rows": int(len(df)),
        "train_rows": int(len(train)),
        "test_rows": int(len(test)),
        "error_rate": round(float(df["ocr_error"].mean()), 4),
        "selected_model": winner_name,
        "selection_rule": "maximise recall, puis F1 et PR-AUC",
        "metrics": evaluation.to_dict(orient="records"),
        "limitations": "Les erreurs sont simulées. Le modèle devra être réentraîné avec les corrections OCR réelles validées par les utilisateurs.",
    }
    (REPORTS / "ocr_error_metrics.json").write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
    joblib.dump({"pipeline": winner, "features": FEATURES, "thresholds": {"review": 0.42, "suspect": 0.70}, "metadata": report}, MODELS / "ocr_error_model.joblib")
    print(json.dumps({"selected_model": winner_name, "metrics": report["metrics"], "rows": len(df)}, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
