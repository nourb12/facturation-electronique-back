"""FastAPI endpoints for TuniFlow ML model artifacts."""

from __future__ import annotations

import json
import hmac
import os
import re
from datetime import date, datetime, timedelta
from pathlib import Path

import joblib
import numpy as np
import pandas as pd
from fastapi import FastAPI, HTTPException, Query, Request
from fastapi.responses import JSONResponse
from pydantic import BaseModel, Field
from sklearn.decomposition import PCA
from sklearn.ensemble import IsolationForest
from sklearn.preprocessing import StandardScaler


APP_ROOT = Path(__file__).resolve().parents[1]
MODELS_DIR = APP_ROOT / "models"
REPORTS_DIR = APP_ROOT / "reports"

app = FastAPI(title="Service ML - TuniFlow")


@app.middleware("http")
async def require_internal_api_key(request: Request, call_next):
    """Allow health probes only; every model endpoint is private to the .NET API."""
    if request.url.path == "/health":
        return await call_next(request)

    expected_key = os.environ.get("TUNIFLOW_ML_API_KEY", "").strip()
    supplied_key = request.headers.get("X-Internal-Api-Key", "")
    if not expected_key:
        return JSONResponse(status_code=503, content={"detail": "Clé interne du service IA non configurée."})
    if not hmac.compare_digest(supplied_key, expected_key):
        return JSONResponse(status_code=401, content={"detail": "Accès non autorisé au service IA."})
    return await call_next(request)


class FacturePredictionRequest(BaseModel):
    montant_ttc: float
    jours_delai: int
    mode_paiement: str
    type_client: str
    ville: str
    anciennete_mois: int
    nombre_factures_precedentes: int
    nombre_retards_precedents: int
    taux_retard_precedent: float
    retard_moyen_jours: float


class TransactionAnomalyRequest(BaseModel):
    time: float = Field(ge=0)
    amount: float = Field(ge=0)
    components: list[float] = Field(min_length=28, max_length=28)


class CompanyTransactionInput(BaseModel):
    transaction_id: str
    date: datetime
    amount: float = Field(gt=0)
    label: str
    counterparty: str | None = None
    transaction_type: str
    source: str
    has_document: bool = False
    ocr_confidence: float | None = Field(default=None, ge=0, le=100)


class CompanyAnomalyReportRequest(BaseModel):
    transactions: list[CompanyTransactionInput] = Field(min_length=6, max_length=5000)


class OcrFieldInput(BaseModel):
    key: str
    label: str | None = None
    value: str | None = None
    confidence: float = Field(default=0, ge=0, le=100)
    required: bool = False


class OcrErrorRequest(BaseModel):
    document_type: str = "facture"
    fields: list[OcrFieldInput] = Field(min_length=1)


class CashForecastInvoice(BaseModel):
    invoice_id: str
    number: str
    customer: str
    due_date: date
    amount: float = Field(gt=0)
    late_probability: float = Field(ge=0, le=100)


class CashForecastOutflow(BaseModel):
    outflow_id: str
    label: str
    scheduled_date: date
    amount: float = Field(gt=0)
    category: str = "Charge"


class CashForecastRequest(BaseModel):
    opening_balance: float
    invoices: list[CashForecastInvoice] = Field(default_factory=list)
    outflows: list[CashForecastOutflow] = Field(default_factory=list)
    horizon_weeks: int = Field(default=8, ge=4, le=26)
    recovery_rate: float = Field(default=0.85, ge=0, le=1)


def load_model(name: str):
    path = MODELS_DIR / name
    if not path.exists():
        raise HTTPException(
            status_code=503,
            detail=f"Modèle {name} indisponible. Lancez le script d'entraînement correspondant.",
        )
    return joblib.load(path)


def load_json_report(name: str) -> dict:
    path = REPORTS_DIR / name
    if not path.exists():
        raise HTTPException(status_code=503, detail=f"Rapport {name} indisponible.")
    return json.loads(path.read_text(encoding="utf-8"))


@app.get("/health")
def health():
    return {
        "status": "ok",
        "late_payment_model": (MODELS_DIR / "retard_model.joblib").exists(),
        "transaction_anomaly_model": (MODELS_DIR / "transaction_anomaly_model.joblib").exists(),
        "ocr_error_model": (MODELS_DIR / "ocr_error_model.joblib").exists(),
    }


@app.post("/predict/late-payment")
def predict_late_payment(facture: FacturePredictionRequest):
    model = load_model("retard_model.joblib")
    data = pd.DataFrame([facture.model_dump()])
    probability = float(model.predict_proba(data)[0, 1])
    prediction = int(model.predict(data)[0])

    if probability >= 0.70:
        risk_level = "élevé"
    elif probability >= 0.40:
        risk_level = "moyen"
    else:
        risk_level = "faible"

    return {
        "prediction": "retard" if prediction == 1 else "paiement_a_temps",
        "probabilite_retard": round(probability * 100, 2),
        "niveau_risque": risk_level,
    }


@app.post("/predict/transaction-anomaly")
def predict_transaction_anomaly(transaction: TransactionAnomalyRequest):
    """Score one ULB-compatible transaction with the selected unsupervised model."""
    bundle = load_model("transaction_anomaly_model.joblib")
    row = {"Time": transaction.time, "Amount": transaction.amount}
    row.update({f"V{index}": value for index, value in enumerate(transaction.components, start=1)})
    features = bundle["features"]
    model = bundle["model"]
    raw_score = float(-model.score_samples(pd.DataFrame([row])[features])[0])
    lower = float(bundle["calibration"]["score_lower"])
    upper = float(bundle["calibration"]["score_upper"])
    score = float(np.clip((raw_score - lower) / max(upper - lower, 1e-9), 0, 1))
    suspicious = bool(model.predict(pd.DataFrame([row])[features])[0] == -1)
    return {
        "model": bundle["selected_model"],
        "anomaly_score": round(score, 4),
        "classification": "suspecte" if suspicious else "normale",
        "requires_review": suspicious,
    }


@app.get("/anomaly/report")
def anomaly_report(
    limit: int = Query(default=10, ge=1, le=50),
    points_limit: int = Query(default=500, ge=50, le=2000),
):
    """Expose real evaluation artifacts for the TuniFlow analysis screen."""
    metrics = load_json_report("anomaly_metrics.json")
    predictions_path = REPORTS_DIR / "anomaly_predictions.csv"
    pca_path = REPORTS_DIR / "anomaly_pca.csv"
    if not predictions_path.exists() or not pca_path.exists():
        raise HTTPException(status_code=503, detail="Prédictions d'anomalies indisponibles.")

    predictions = pd.read_csv(predictions_path)
    suspicious = predictions[predictions["anomaly_detected"] == 1].head(limit)
    pca = pd.read_csv(pca_path)
    sample = pca.groupby("anomaly_detected", group_keys=False).apply(
        lambda group: group.sample(min(len(group), max(1, points_limit // 2)), random_state=42)
    )
    return {
        "metrics": metrics,
        "summary": {
            "transactions_analyzed": int(len(predictions)),
            "suspected_transactions": int(predictions["anomaly_detected"].sum()),
            "normal_transactions": int((predictions["anomaly_detected"] == 0).sum()),
            "selected_model": metrics["selected_model"],
        },
        "top_suspicious": suspicious.to_dict(orient="records"),
        "pca_points": sample.to_dict(orient="records"),
    }


@app.post("/anomaly/company-report")
def company_anomaly_report(payload: CompanyAnomalyReportRequest):
    """Detect unusual behaviour in the company's own recorded transactions.

    Unlike the academic ULB endpoint above, this endpoint never uses the test
    dataset. It fits an Isolation Forest to the enterprise portfolio supplied by
    the .NET API and returns a local, explainable review queue.
    """
    frame = pd.DataFrame([item.model_dump() for item in payload.transactions])
    frame["date"] = pd.to_datetime(frame["date"], utc=True)
    frame["log_amount"] = np.log1p(frame["amount"].astype(float))
    frame["weekday"] = frame["date"].dt.weekday.astype(float)
    frame["days_from_first"] = (frame["date"] - frame["date"].min()).dt.total_seconds() / 86400.0
    frame["type_code"] = pd.factorize(frame["transaction_type"].fillna("inconnu"))[0].astype(float)
    frame["source_code"] = pd.factorize(frame["source"].fillna("inconnu"))[0].astype(float)
    frame["has_document_value"] = frame["has_document"].astype(int).astype(float)
    frame["ocr_confidence_value"] = frame["ocr_confidence"].fillna(50).astype(float) / 100.0
    counterparty_counts = frame["counterparty"].fillna("sans tiers").value_counts()
    frame["counterparty_rarity"] = frame["counterparty"].fillna("sans tiers").map(lambda value: 1.0 / counterparty_counts[value])

    features = [
        "log_amount", "weekday", "days_from_first", "type_code", "source_code",
        "has_document_value", "ocr_confidence_value", "counterparty_rarity",
    ]
    scaled = StandardScaler().fit_transform(frame[features])
    contamination = min(0.20, max(0.10, 3 / len(frame)))
    model = IsolationForest(n_estimators=300, contamination=contamination, random_state=42)
    predictions = model.fit_predict(scaled)
    raw_scores = -model.score_samples(scaled)
    lower, upper = np.percentile(raw_scores, [5, 95])
    scores = np.clip((raw_scores - lower) / max(upper - lower, 1e-9), 0, 1)

    amount_z = np.abs((frame["log_amount"] - frame["log_amount"].mean()) / (frame["log_amount"].std(ddof=0) or 1))
    rare_counterparty = frame["counterparty_rarity"] >= frame["counterparty_rarity"].quantile(0.85)
    low_confidence = frame["ocr_confidence"].notna() & (frame["ocr_confidence"] < 65)

    def reason_at(index: int) -> str:
        reasons: list[str] = []
        if amount_z.iloc[index] >= 1.25:
            reasons.append("montant inhabituel")
        if rare_counterparty.iloc[index]:
            reasons.append("tiers peu fréquent")
        if low_confidence.iloc[index]:
            reasons.append("confiance OCR faible")
        if not frame["has_document"].iloc[index]:
            reasons.append("justificatif absent")
        return ", ".join(reasons[:2]) or "combinaison de caractéristiques inhabituelle"

    points = PCA(n_components=2, random_state=42).fit_transform(scaled)
    results = []
    for index, row in frame.iterrows():
        results.append({
            "transaction_id": row["transaction_id"],
            "date": row["date"].isoformat(),
            "amount": round(float(row["amount"]), 3),
            "label": row["label"],
            "counterparty": row["counterparty"],
            "anomaly_score": round(float(scores[index]), 4),
            "anomaly_detected": bool(predictions[index] == -1),
            "reason": reason_at(index),
            "pca_x": round(float(points[index, 0]), 5),
            "pca_y": round(float(points[index, 1]), 5),
        })

    suspicious = sorted((item for item in results if item["anomaly_detected"]), key=lambda item: item["anomaly_score"], reverse=True)
    return {
        "source": "transactions_tuniflow",
        "selected_model": "isolation_forest",
        "model_description": "Isolation Forest entraîné sur les transactions de cette entreprise",
        "summary": {
            "transactions_analyzed": len(results),
            "suspected_transactions": len(suspicious),
            "normal_transactions": len(results) - len(suspicious),
            "selected_model": "isolation_forest",
        },
        "top_suspicious": suspicious[:50],
        "pca_points": results,
        "review_note": "Une alerte signale un écart statistique et nécessite une vérification humaine.",
    }


def _normalise_confidence(value: float) -> float:
    return value / 100 if value > 1 else value


def _is_valid_date(value: str | None) -> bool:
    if not value:
        return False
    for pattern in ("%d/%m/%Y", "%d-%m-%Y", "%Y-%m-%d", "%d.%m.%Y"):
        try:
            datetime.strptime(value.strip(), pattern)
            return True
        except ValueError:
            pass
    return False


def _parse_amount(value: str | None) -> float | None:
    if not value:
        return None
    cleaned = re.sub(r"(?i)(tnd|dt|eur|€|\$)", "", value).replace(" ", "").strip()
    if re.search(r"[A-Za-z]", cleaned):
        return None
    # Tunisian OCR commonly returns either 1 200,500 or 1200.500.
    if cleaned.count(",") == 1 and cleaned.count(".") >= 1:
        cleaned = cleaned.replace(".", "").replace(",", ".")
    else:
        cleaned = cleaned.replace(",", ".")
    try:
        return float(cleaned)
    except ValueError:
        return None


@app.post("/predict/ocr-error")
def predict_ocr_error(payload: OcrErrorRequest):
    """Assess an OCR extraction and return field-level explanations for review."""
    bundle = load_model("ocr_error_model.joblib")
    fields = {field.key.lower(): field for field in payload.fields}
    values = {key: (field.value or "").strip() for key, field in fields.items()}
    confidence_values = [_normalise_confidence(field.confidence) for field in fields.values()]
    average_confidence = float(np.mean(confidence_values)) if confidence_values else 0.0
    missing = sum(1 for field in fields.values() if field.required and not (field.value or "").strip())
    date_value = values.get("issue_date") or values.get("date")
    invalid_date = int(bool(date_value) and not _is_valid_date(date_value))
    numeric_keys = ("total_ht", "net_ht", "total_ttc", "total_to_pay", "amount", "tva", "total_tva")
    numeric_values = {key: _parse_amount(values.get(key)) for key in numeric_keys if key in values}
    amount_parse_error = int(any(values.get(key) and amount is None for key, amount in numeric_values.items()))
    character_error_count = sum(
        1 for key, raw in values.items()
        if key in numeric_keys and raw and re.search(r"[A-Za-z]", raw)
    )
    total_ht = numeric_values.get("total_ht", numeric_values.get("net_ht"))
    tva = numeric_values.get("tva", numeric_values.get("total_tva"))
    total_ttc = numeric_values.get("total_ttc", numeric_values.get("total_to_pay"))
    calculation_inconsistent = int(
        total_ht is not None and tva is not None and total_ttc is not None and abs((total_ht + tva) - total_ttc) > 0.02
    )
    supplier_missing = int(not (values.get("supplier_name") or values.get("fournisseur")))
    tax_id = values.get("supplier_tax_id") or values.get("matricule_fiscal") or ""
    tax_id_invalid = int(bool(tax_id) and len(re.sub(r"[^A-Za-z0-9]", "", tax_id)) < 6)
    model_row = pd.DataFrame([{
        "ocr_confidence": average_confidence,
        "missing_fields_count": missing,
        "character_error_count": character_error_count,
        "invalid_date": invalid_date,
        "amount_parse_error": amount_parse_error,
        "calculation_inconsistent": calculation_inconsistent,
        "supplier_missing": supplier_missing,
        "tax_id_invalid": tax_id_invalid,
        "document_type": payload.document_type.lower(),
    }])
    probability = float(bundle["pipeline"].predict_proba(model_row[bundle["features"]])[0, 1])
    thresholds = bundle["thresholds"]
    assessments = []
    for field in payload.fields:
        key = field.key.lower()
        reasons: list[str] = []
        if field.required and not (field.value or "").strip():
            reasons.append("Champ obligatoire manquant")
        if key in ("issue_date", "date") and invalid_date:
            reasons.append("Format de date invalide")
        if key in numeric_keys and values.get(key) and numeric_values.get(key) is None:
            reasons.append("Caractère confondu ou format de montant invalide")
        if key in ("total_ttc", "total_to_pay") and calculation_inconsistent:
            reasons.append("HT + TVA ≠ TTC")
        if key in ("supplier_name", "fournisseur") and supplier_missing:
            reasons.append("Fournisseur non identifié")
        if key in ("supplier_tax_id", "matricule_fiscal") and tax_id_invalid:
            reasons.append("Matricule fiscal invalide")
        if field.confidence < 55 and not reasons:
            reasons.append("Confiance OCR faible")
        status = "suspect" if reasons and (field.confidence < 55 or len(reasons) > 0) else "ok"
        assessments.append({
            "key": field.key,
            "label": field.label or field.key.replace("_", " ").title(),
            "value": field.value or "—",
            "confidence": round(_normalise_confidence(field.confidence), 2),
            "error_detected": " · ".join(reasons) if reasons else "—",
            "status": status,
        })
    review_count = sum(1 for item in assessments if item["status"] == "suspect")
    global_status = "suspect" if probability >= thresholds["suspect"] else "a_revoir" if probability >= thresholds["review"] else "ok"
    return {
        "model": bundle["metadata"]["selected_model"],
        "error_probability": round(probability * 100, 1),
        "global_status": global_status,
        "requires_review": probability >= thresholds["review"],
        "fields_to_review": review_count,
        "field_assessments": assessments,
        "summary": {
            "character_errors": character_error_count,
            "missing_fields": missing,
            "calculation_inconsistencies": calculation_inconsistent,
            "invalid_dates": invalid_date,
        },
    }


def _week_start(value: date) -> date:
    return value - timedelta(days=value.weekday())


def _cash_week_label(value: date) -> str:
    end = value + timedelta(days=6)
    return f"{value.strftime('%d/%m')} - {end.strftime('%d/%m')}"


@app.post("/forecast/cash")
def forecast_cash(payload: CashForecastRequest):
    """Build an explainable weekly cash forecast from phase 1 late-payment scores."""
    today = date.today()
    first_week = _week_start(today)
    weeks = [first_week + timedelta(days=7 * index) for index in range(payload.horizon_weeks)]
    buckets = {
        week: {
            "week_start": week.isoformat(),
            "week_label": _cash_week_label(week),
            "optimistic_inflows": 0.0,
            "on_time_expected": 0.0,
            "deferred_expected": 0.0,
            "outflows": 0.0,
        }
        for week in weeks
    }

    def add_to_week(target_date: date, key: str, amount: float) -> None:
        target_week = _week_start(max(target_date, today))
        if target_week in buckets:
            buckets[target_week][key] += amount

    expected_deferred = 0.0
    invoice_forecasts = []
    for invoice in payload.invoices:
        risk = invoice.late_probability / 100
        # The phase 1 model predicts a probability, not an exact payment date.
        # This documented assumption converts risk into a prudent payment delay.
        estimated_delay_days = round(5 + 35 * risk)
        paid_on_time = invoice.amount * (1 - risk)
        delayed_recovery = invoice.amount * risk * payload.recovery_rate
        expected_deferred += delayed_recovery
        add_to_week(invoice.due_date, "optimistic_inflows", invoice.amount)
        add_to_week(invoice.due_date, "on_time_expected", paid_on_time)
        add_to_week(invoice.due_date + timedelta(days=estimated_delay_days), "deferred_expected", delayed_recovery)
        invoice_forecasts.append({
            "invoice_id": invoice.invoice_id,
            "number": invoice.number,
            "customer": invoice.customer,
            "due_date": invoice.due_date.isoformat(),
            "amount": round(invoice.amount, 3),
            "late_probability": round(invoice.late_probability, 2),
            "on_time_amount": round(paid_on_time, 3),
            "deferred_amount": round(delayed_recovery, 3),
            "estimated_delay_days": estimated_delay_days,
            "estimated_payment_date": (invoice.due_date + timedelta(days=estimated_delay_days)).isoformat(),
        })

    for outflow in payload.outflows:
        add_to_week(outflow.scheduled_date, "outflows", outflow.amount)

    optimistic_balance = payload.opening_balance
    realistic_balance = payload.opening_balance
    pessimistic_balance = payload.opening_balance
    weekly = []
    for week in weeks:
        bucket = buckets[week]
        realistic_inflows = bucket["on_time_expected"] + bucket["deferred_expected"]
        optimistic_balance += bucket["optimistic_inflows"] - bucket["outflows"]
        realistic_balance += realistic_inflows - bucket["outflows"]
        pessimistic_balance -= bucket["outflows"]
        weekly.append({
            **{key: round(value, 3) if isinstance(value, float) else value for key, value in bucket.items()},
            "realistic_inflows": round(realistic_inflows, 3),
            "optimistic_balance": round(optimistic_balance, 3),
            "realistic_balance": round(realistic_balance, 3),
            "pessimistic_balance": round(pessimistic_balance, 3),
        })

    total_outflows = sum(item.amount for item in payload.outflows)
    total_invoices = sum(item.amount for item in payload.invoices)
    final_realistic = weekly[-1]["realistic_balance"]
    return {
        "source": "late_payment_prediction",
        "assumption": {
            "recovery_rate": payload.recovery_rate,
            "delay_rule": "5 + 35 × probabilité de retard (jours)",
        },
        "summary": {
            "opening_balance": round(payload.opening_balance, 3),
            "invoices_total": round(total_invoices, 3),
            "outflows_total": round(total_outflows, 3),
            "deferred_expected": round(expected_deferred, 3),
            "final_optimistic": weekly[-1]["optimistic_balance"],
            "final_realistic": final_realistic,
            "final_pessimistic": weekly[-1]["pessimistic_balance"],
            "alert": "Risque de tension de trésorerie" if final_realistic < 0 else "Prévision maîtrisée",
        },
        "invoice_forecasts": invoice_forecasts,
        "weekly": weekly,
    }
