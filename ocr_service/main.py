from __future__ import annotations

import io
import logging
import os
import re
from dataclasses import dataclass
from functools import lru_cache
from typing import Iterable

import fitz  # PyMuPDF
import numpy as np
from fastapi import FastAPI, File, HTTPException, UploadFile
from fastapi.middleware.cors import CORSMiddleware
os.environ.setdefault("PADDLE_PDX_DISABLE_MODEL_SOURCE_CHECK", "True")
from paddleocr import PaddleOCR
from PIL import Image
from pydantic import BaseModel

logging.basicConfig(level=logging.INFO)
log = logging.getLogger(__name__)

AR_RNE = "\u0627\u0644\u0633\u062c\u0644 \u0627\u0644\u0648\u0637\u0646\u064a \u0644\u0644\u0645\u0624\u0633\u0633\u0627\u062a"
AR_FISCAL = "\u0628\u0637\u0627\u0642\u0629 \u0627\u0644\u062a\u0639\u0631\u064a\u0641 \u0627\u0644\u062c\u0628\u0627\u0626\u064a\u0629"
AR_CIN = "\u0628\u0637\u0627\u0642\u0629 \u0627\u0644\u062a\u0639\u0631\u064a\u0641 \u0627\u0644\u0648\u0637\u0646\u064a\u0629"
AR_ADDRESS = "\u0627\u0644\u0639\u0646\u0648\u0627\u0646"
AR_MANAGER = "\u0627\u0644\u0645\u0633\u064a\u0631"
AR_FIRST_NAME = "\u0627\u0644\u0627\u0633\u0645"
AR_LAST_NAME = "\u0627\u0644\u0644\u0642\u0628"
AR_SARL = "\u0634\u0631\u0643\u0629 \u0630\u0627\u062a \u0627\u0644\u0645\u0633\u0624\u0648\u0644\u064a\u0629 \u0627\u0644\u0645\u062d\u062f\u0648\u062f\u0629"
AR_SA = "\u0634\u0631\u0643\u0629 \u0645\u0633\u0627\u0647\u0645\u0629"
AR_EI = "\u0645\u0624\u0633\u0633\u0629 \u0641\u0631\u062f\u064a\u0629"

ALLOWED_MIME = {
    "image/jpeg", "image/jpg", "image/png", "image/webp", "image/tiff", "application/pdf"
}

MF_LONG_RE = re.compile(r"\b(\d{7}[A-Za-z][\s/\\-]*[A-Za-z][\s/\\-]*[A-Za-z][\s/\\-]*\d{3})\b")
MF_SHORT_RE = re.compile(r"\b(\d{6,7}[\s/\\-]*[A-Za-z])\b")
DATE_RE = re.compile(r"\b(\d{2}/\d{2}/\d{4})\b")
CIN_RE = re.compile(r"\b\d{8}\b")
ADDRESS_LINE_RE = re.compile(rf"(adresse|{AR_ADDRESS})\s*[:?\-]?\s*(.+)", re.IGNORECASE)
NAME_LINE_RE = re.compile(rf"(nom|prenom|prenom|gerant|gerant|responsable|{AR_MANAGER}|{AR_FIRST_NAME}|{AR_LAST_NAME})\s*[:?\-]?\s*(.+)", re.IGNORECASE)

FORME_MAP = {
    "SARL": ["SARL", "SUARL", "SOCIETE A RESPONSABILITE LIMITEE", AR_SARL],
    "SA": ["SA", "SOCIETE ANONYME", AR_SA],
    "EI": ["ENTREPRISE INDIVIDUELLE", "EI", AR_EI],
    "SNC": ["SNC", "SOCIETE EN NOM COLLECTIF"],
}

BUSINESS_PREFIXES = (
    "STE", "STE.", "SOCIETE", "SARL", "SUARL", "SA", "SNC", "ENTREPRISE", "ETABLISSEMENT"
)

BUSINESS_STOPWORDS = (
    "REPUBLIQUE", "MINISTERE", "MINISTERE DES FINANCES", "STRUCTURE DE CONTROLE",
    "CARTE D", "ASSUJETTI", "ACTIVITE", "A COMPTER DU", "ADRESSE", "CHEF DE LA STRUCTURE"
)


@dataclass
class OcrLine:
    text: str
    score: float


class OcrExtractionResult(BaseModel):
    ocrSuccess: bool
    confidenceScore: float
    typeDocument: str | None = None
    matriculeFiscalExtrait: str | None = None
    raisonSocialeExtraite: str | None = None
    nomGerantExtrait: str | None = None
    nomExtrait: str | None = None
    prenomExtrait: str | None = None
    cinExtrait: str | None = None
    formeJuridiqueExtraite: str | None = None
    adresseExtraite: str | None = None
    dateCreationExtraite: str | None = None
    kycScore: int = 0
    texteBrut: str = ""
    erreurMessage: str | None = None


app = FastAPI(
    title="EY-Factify OCR Service",
    description="Extraction KYC locale via PaddleOCR (francais + arabe)",
    version="4.0.0",
)

app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_methods=["POST", "GET"],
    allow_headers=["*"],
)


@lru_cache(maxsize=1)
def get_latin_ocr() -> PaddleOCR:
    return PaddleOCR(use_angle_cls=True, lang="latin", show_log=False)


@lru_cache(maxsize=1)
def get_arabic_ocr() -> PaddleOCR:
    return PaddleOCR(use_angle_cls=True, lang="arabic", show_log=False)


def normalize_space(value: str | None) -> str:
    if not value:
        return ""
    return re.sub(r"\s+", " ", value).strip()


def dedupe_lines(lines: Iterable[OcrLine]) -> list[OcrLine]:
    seen: set[str] = set()
    result: list[OcrLine] = []
    for line in lines:
        key = normalize_space(line.text).lower()
        if not key or key in seen:
            continue
        seen.add(key)
        result.append(line)
    return result


def flatten_result(result: object) -> list[OcrLine]:
    lines: list[OcrLine] = []

    def walk(node: object) -> None:
        if isinstance(node, (list, tuple)):
            if len(node) >= 2 and isinstance(node[1], (list, tuple)):
                payload = node[1]
                if len(payload) >= 2 and isinstance(payload[0], str):
                    text = normalize_space(payload[0])
                    try:
                        score = float(payload[1])
                    except Exception:
                        score = 0.0
                    if text:
                        lines.append(OcrLine(text=text, score=score))
                    return
            for child in node:
                walk(child)

    walk(result)
    return dedupe_lines(lines)


def image_from_bytes(file_bytes: bytes) -> np.ndarray:
    image = Image.open(io.BytesIO(file_bytes)).convert("RGB")
    return np.array(image)


def pdf_to_images(file_bytes: bytes) -> list[np.ndarray]:
    document = fitz.open(stream=file_bytes, filetype="pdf")
    images: list[np.ndarray] = []
    matrix = fitz.Matrix(2, 2)
    for page in document:
        pix = page.get_pixmap(matrix=matrix, alpha=False)
        image = Image.open(io.BytesIO(pix.tobytes("png"))).convert("RGB")
        images.append(np.array(image))
    return images


def iter_images(file_bytes: bytes, mime_type: str) -> list[np.ndarray]:
    return pdf_to_images(file_bytes) if mime_type == "application/pdf" else [image_from_bytes(file_bytes)]


def ocr_image(image: np.ndarray) -> list[OcrLine]:
    lines: list[OcrLine] = []
    for engine in (get_latin_ocr(), get_arabic_ocr()):
        try:
            raw = engine.ocr(image, cls=True)
            lines.extend(flatten_result(raw))
        except Exception as exc:
            log.warning("OCR engine error: %s", exc)
    return dedupe_lines(lines)


def detect_document_type(text: str) -> str:
    upper = text.upper()
    if "RELEVE D'IDENTITE BANCAIRE" in upper or "RIB" in upper or "BANCAIRE" in upper:
        return "RIB"
    if "REGISTRE NATIONAL DES ENTREPRISES" in upper or AR_RNE in text:
        return "RNE"
    if any(token in upper for token in ("IDENTIFICATION FISCALE", "FISCALE", "PATENTE", "TVA", "CONTROLE DES IMPOTS")) or AR_FISCAL in text:
        return "FISCAL"
    if any(token in upper for token in ("CARTE D'IDENTITE NATIONALE", "CARTE D'LDENTITE NATIONALE", "CIN")) or AR_CIN in text:
        return "CIN"
    return "INCONNU"


def extract_matricule_fiscal(text: str) -> str | None:
    for pattern in (MF_LONG_RE, MF_SHORT_RE):
        match = pattern.search(text)
        if match:
            value = re.sub(r"[\s\\-]+", "", match.group(1)).upper()
            if "/" not in value:
                full = re.match(r"(\d{6,7})([A-Z].*)", value)
                if full:
                    return f"{full.group(1)}/{full.group(2)}"
            return value
    digits = re.findall(r"\b\d{7,8}\b", text)
    return digits[0] if digits else None


def extract_forme_juridique(text: str) -> str | None:
    upper = text.upper()
    for canonical, values in FORME_MAP.items():
        if any(value.upper() in upper or value in text for value in values):
            return canonical
    return None


def extract_date(text: str) -> str | None:
    match = DATE_RE.search(text)
    return match.group(1) if match else None


def extract_cin(text: str) -> str | None:
    matches = CIN_RE.findall(text)
    return matches[0] if matches else None


def extract_address(lines: list[OcrLine]) -> str | None:
    for index, line in enumerate(lines):
        match = ADDRESS_LINE_RE.search(line.text)
        if match:
            direct = normalize_space(match.group(2)).strip(":- ")
            if direct and any(ch.isalnum() for ch in direct):
                return direct
        if match:
            neighbors = []
            if index > 0:
                neighbors.append(lines[index - 1].text)
            if index + 1 < len(lines):
                neighbors.append(lines[index + 1].text)
            for candidate in neighbors:
                clean = normalize_space(candidate)
                if len(clean) >= 10 and any(ch.isdigit() for ch in clean):
                    return clean.strip(":- ")
    fallback = [normalize_space(line.text).strip(":- ") for line in lines if len(line.text) >= 12 and any(ch.isdigit() for ch in line.text)]
    fallback = [line for line in fallback if any(ch.isalnum() for ch in line)]
    return fallback[0] if fallback else None


def extract_names(lines: list[OcrLine]) -> tuple[str | None, str | None, str | None]:
    nom = None
    prenom = None
    gerant = None
    for line in lines:
        match = NAME_LINE_RE.search(line.text)
        if not match:
            continue
        label = match.group(1).lower()
        value = normalize_space(match.group(2))
        if not value:
            continue
        if "ger" in label or "responsable" in label or AR_MANAGER in match.group(1):
            gerant = gerant or value
        elif "prenom" in label or AR_FIRST_NAME in match.group(1):
            prenom = prenom or value
        elif "nom" in label or AR_LAST_NAME in match.group(1):
            nom = nom or value
    return nom, prenom, gerant


def extract_business_name(lines: list[OcrLine]) -> str | None:
    for index, line in enumerate(lines):
        upper = normalize_space(line.text).upper()
        if "RAISON SOCIALE" in upper or "NOM ET PRENOM" in upper:
            for offset in (1, 2):
                if index + offset < len(lines):
                    candidate = normalize_space(lines[index + offset].text).strip('"')
                    candidate_upper = candidate.upper()
                    if candidate and not any(stop in candidate_upper for stop in BUSINESS_STOPWORDS):
                        return candidate
    for line in lines:
        clean = normalize_space(line.text).strip('"')
        upper = clean.upper()
        if any(prefix in upper for prefix in BUSINESS_PREFIXES) and not any(stop in upper for stop in BUSINESS_STOPWORDS):
            return clean
    fallback = [normalize_space(line.text).strip('"') for line in lines if len(normalize_space(line.text)) >= 5 and not any(ch.isdigit() for ch in line.text)]
    fallback = [line for line in fallback if not any(stop in line.upper() for stop in BUSINESS_STOPWORDS)]
    return fallback[0] if fallback else None


def estimate_kyc_score(data: dict[str, str | None], confidence: float) -> int:
    score = 0
    if data.get("matriculeFiscal"):
        score += 25
    if data.get("raisonSociale"):
        score += 25
    if data.get("nomGerant") or (data.get("nom") and data.get("prenom")):
        score += 25
    if data.get("formeJuridique") or data.get("adresse"):
        score += 15
    if data.get("cin") or data.get("dateCreation"):
        score += 10
    if confidence < 0.55:
        score = max(0, score - 20)
    return min(score, 100)


@app.post("/ocr/parse", response_model=OcrExtractionResult)
async def parse_documents(files: list[UploadFile] = File(...)) -> OcrExtractionResult:
    if not files:
        raise HTTPException(status_code=400, detail="Aucun fichier recu.")

    valides = [f for f in files if (f.content_type or "").lower() in ALLOWED_MIME]
    if not valides:
        return OcrExtractionResult(
            ocrSuccess=False,
            confidenceScore=0.0,
            erreurMessage="Types non supportes. Utilisez JPG, PNG, WEBP ou PDF.",
        )

    merged: dict[str, str | None] = {
        "typeDocument": None,
        "matriculeFiscal": None,
        "raisonSociale": None,
        "nomGerant": None,
        "nom": None,
        "prenom": None,
        "cin": None,
        "formeJuridique": None,
        "adresse": None,
        "dateCreation": None,
    }
    raw_texts: list[str] = []
    all_scores: list[float] = []
    errors: list[str] = []

    for upload in valides:
        try:
            file_bytes = await upload.read()
            mime = (upload.content_type or "image/jpeg").lower()
            pages = iter_images(file_bytes, mime)
            file_lines: list[OcrLine] = []
            for image in pages:
                file_lines.extend(ocr_image(image))
            file_lines = dedupe_lines(file_lines)
            if not file_lines:
                errors.append(f"{upload.filename}: aucune ligne OCR detectee")
                continue

            file_text = "\n".join(line.text for line in file_lines)
            raw_texts.append(file_text)
            all_scores.extend(line.score for line in file_lines if line.score > 0)

            doc_type = detect_document_type(file_text)
            merged["typeDocument"] = merged["typeDocument"] or doc_type
            merged["matriculeFiscal"] = merged["matriculeFiscal"] or extract_matricule_fiscal(file_text)
            merged["formeJuridique"] = merged["formeJuridique"] or extract_forme_juridique(file_text)
            merged["dateCreation"] = merged["dateCreation"] or extract_date(file_text)
            merged["cin"] = merged["cin"] or extract_cin(file_text)
            merged["adresse"] = merged["adresse"] or extract_address(file_lines)
            nom, prenom, gerant = extract_names(file_lines)
            merged["nom"] = merged["nom"] or nom
            merged["prenom"] = merged["prenom"] or prenom
            merged["nomGerant"] = merged["nomGerant"] or gerant
            if doc_type in {"FISCAL", "RNE", "INCONNU"}:
                merged["raisonSociale"] = merged["raisonSociale"] or extract_business_name(file_lines)
        except Exception as exc:
            log.exception("Erreur OCR sur %s", upload.filename)
            errors.append(f"{upload.filename}: {exc}")

    confidence = round(sum(all_scores) / len(all_scores), 4) if all_scores else 0.0
    texte_brut = "\n\n=====\n\n".join(raw_texts)
    extracted_count = sum(1 for key, value in merged.items() if key != "typeDocument" and value)

    if extracted_count == 0:
        return OcrExtractionResult(
            ocrSuccess=False,
            confidenceScore=confidence,
            typeDocument=merged.get("typeDocument"),
            texteBrut=texte_brut,
            erreurMessage=" ; ".join(errors) if errors else "Extraction OCR vide.",
        )

    kyc_score = estimate_kyc_score(merged, confidence)
    return OcrExtractionResult(
        ocrSuccess=True,
        confidenceScore=confidence,
        typeDocument=merged.get("typeDocument"),
        matriculeFiscalExtrait=merged.get("matriculeFiscal"),
        raisonSocialeExtraite=merged.get("raisonSociale"),
        nomGerantExtrait=merged.get("nomGerant"),
        nomExtrait=merged.get("nom"),
        prenomExtrait=merged.get("prenom"),
        cinExtrait=merged.get("cin"),
        formeJuridiqueExtraite=merged.get("formeJuridique"),
        adresseExtraite=merged.get("adresse"),
        dateCreationExtraite=merged.get("dateCreation"),
        kycScore=kyc_score,
        texteBrut=texte_brut,
        erreurMessage=" ; ".join(errors) if errors else None,
    )


@app.get("/health")
def health() -> dict[str, object]:
    return {
        "status": "ok",
        "engine": "PaddleOCR",
        "languages": ["latin", "arabic"],
    }
