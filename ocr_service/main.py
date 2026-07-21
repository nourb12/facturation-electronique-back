from __future__ import annotations

import io
import json
import logging
import os
import re
import unicodedata
from dataclasses import dataclass
from difflib import SequenceMatcher
from functools import lru_cache
from typing import Iterable, Sequence

import fitz  # PyMuPDF
import numpy as np
from fastapi import FastAPI, File, Form, HTTPException, UploadFile
from fastapi.middleware.cors import CORSMiddleware
os.environ.setdefault("PADDLE_PDX_DISABLE_MODEL_SOURCE_CHECK", "True")
from paddleocr import PaddleOCR
from PIL import Image
from pydantic import BaseModel

try:
    import cv2  # type: ignore[import-not-found]
except Exception:  # pragma: no cover
    cv2 = None

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
TIME_RE = re.compile(r"\b([01]?\d|2[0-3]):([0-5]\d)\b")
IBAN_RE = re.compile(r"\b([A-Z]{2}\d{2}[A-Z0-9]{10,30})\b")
AMOUNT_RE = re.compile(r"(-?\d{1,3}(?:[\s.,]\d{3})*(?:[.,]\d{2,3})|-?\d+(?:[.,]\d{2,3}))")
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
    box: tuple[tuple[float, float], tuple[float, float], tuple[float, float], tuple[float, float]] | None = None

    def bounds(self) -> tuple[float, float, float, float] | None:
        if not self.box:
            return None
        xs = [p[0] for p in self.box]
        ys = [p[1] for p in self.box]
        return min(xs), min(ys), max(xs), max(ys)

    def x_mid(self) -> float | None:
        bounds = self.bounds()
        if not bounds:
            return None
        return (bounds[0] + bounds[2]) / 2

    def y_mid(self) -> float | None:
        bounds = self.bounds()
        if not bounds:
            return None
        return (bounds[1] + bounds[3]) / 2


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


class AccountingFieldResult(BaseModel):
    key: str
    label: str
    value: str | None = None
    confidence: int = 0
    required: bool = False
    requiresReview: bool = False


class AccountingOcrExtractionResult(BaseModel):
    schemaVersion: str = "1.0"
    schema_version: str = "1.0"
    ocrSuccess: bool
    overallConfidence: int
    documentType: str
    fields: list[AccountingFieldResult] = []
    missingFields: list[str] = []
    document: dict[str, object] = {}
    rawText: str = ""
    sourceMode: str = "ocr"
    imageQuality: dict[str, object] = {}
    errorMessage: str | None = None


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

    def parse_box(node: object) -> tuple[tuple[float, float], tuple[float, float], tuple[float, float], tuple[float, float]] | None:
        if not isinstance(node, (list, tuple)) or len(node) != 4:
            return None
        points: list[tuple[float, float]] = []
        for point in node:
            if not isinstance(point, (list, tuple)) or len(point) < 2:
                return None
            x, y = point[0], point[1]
            if not isinstance(x, (int, float)) or not isinstance(y, (int, float)):
                return None
            points.append((float(x), float(y)))
        return (points[0], points[1], points[2], points[3])

    def parse_text_score(node: object) -> tuple[str, float] | None:
        if not isinstance(node, (list, tuple)) or len(node) < 2:
            return None
        text = node[0]
        if not isinstance(text, str):
            return None
        try:
            score = float(node[1])
        except Exception:
            score = 0.0
        return normalize_space(text), score

    def maybe_add_line(node: object) -> bool:
        if not isinstance(node, (list, tuple)) or len(node) < 2:
            return False
        payload = parse_text_score(node[1])
        if not payload:
            return False
        text, score = payload
        if not text:
            return False
        box = parse_box(node[0])
        lines.append(OcrLine(text=text, score=score, box=box))
        return True

    def walk(node: object) -> None:
        if isinstance(node, (list, tuple)):
            if maybe_add_line(node):
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


def pdf_to_text_lines(file_bytes: bytes) -> list[OcrLine]:
    document = fitz.open(stream=file_bytes, filetype="pdf")
    if document.page_count == 0:
        return []

    first_text = (document[0].get_text("text") or "").strip()
    if len(first_text) < 50:
        return []

    lines: list[OcrLine] = []
    for page in document:
        blocks = page.get_text("blocks") or []
        for block in blocks:
            if len(block) < 5:
                continue
            x0, y0, x1, y1, text = block[0], block[1], block[2], block[3], block[4]
            if not isinstance(text, str):
                continue
            box = ((float(x0), float(y0)), (float(x1), float(y0)), (float(x1), float(y1)), (float(x0), float(y1)))
            for raw_line in text.splitlines():
                clean = normalize_space(raw_line)
                if clean:
                    lines.append(OcrLine(text=clean, score=0.99, box=box))
    return dedupe_lines(lines)


def iter_images(file_bytes: bytes, mime_type: str) -> list[np.ndarray]:
    return pdf_to_images(file_bytes) if mime_type == "application/pdf" else [image_from_bytes(file_bytes)]

def _rotate_image(bgr: np.ndarray, angle_degrees: float) -> np.ndarray:
    if cv2 is None:
        return bgr
    height, width = bgr.shape[:2]
    center = (width / 2.0, height / 2.0)
    matrix = cv2.getRotationMatrix2D(center, angle_degrees, 1.0)
    return cv2.warpAffine(
        bgr,
        matrix,
        (width, height),
        flags=cv2.INTER_CUBIC,
        borderMode=cv2.BORDER_REPLICATE,
    )


def _deskew_bgr(bgr: np.ndarray) -> np.ndarray:
    if cv2 is None:
        return bgr
    gray = cv2.cvtColor(bgr, cv2.COLOR_BGR2GRAY)
    gray = cv2.bitwise_not(gray)
    _, thresh = cv2.threshold(gray, 0, 255, cv2.THRESH_BINARY | cv2.THRESH_OTSU)
    coords = np.column_stack(np.where(thresh > 0))
    if coords.shape[0] < 1000:
        return bgr
    angle = cv2.minAreaRect(coords)[-1]
    if angle < -45:
        angle = -(90 + angle)
    else:
        angle = -angle
    if abs(angle) < 0.5 or abs(angle) > 15:
        return bgr
    return _rotate_image(bgr, angle)


def _try_unwarp_document(bgr: np.ndarray) -> np.ndarray:
    if cv2 is None:
        return bgr
    height, width = bgr.shape[:2]
    if height < 300 or width < 300:
        return bgr

    gray = cv2.cvtColor(bgr, cv2.COLOR_BGR2GRAY)
    blur = cv2.GaussianBlur(gray, (5, 5), 0)
    edges = cv2.Canny(blur, 50, 150)
    edges = cv2.dilate(edges, np.ones((3, 3), np.uint8), iterations=2)

    found = cv2.findContours(edges, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)
    contours = found[0] if len(found) == 2 else found[1]
    if not contours:
        return bgr

    image_area = float(height * width)
    contours = sorted(contours, key=cv2.contourArea, reverse=True)
    quad = None
    for contour in contours[:8]:
        area = cv2.contourArea(contour)
        if area < 0.15 * image_area:
            continue
        peri = cv2.arcLength(contour, True)
        approx = cv2.approxPolyDP(contour, 0.02 * peri, True)
        if len(approx) == 4:
            quad = approx.reshape(4, 2).astype(np.float32)
            break

    if quad is None:
        return bgr

    sums = quad.sum(axis=1)
    diffs = np.diff(quad, axis=1).reshape(-1)
    top_left = quad[np.argmin(sums)]
    bottom_right = quad[np.argmax(sums)]
    top_right = quad[np.argmin(diffs)]
    bottom_left = quad[np.argmax(diffs)]
    src = np.array([top_left, top_right, bottom_right, bottom_left], dtype=np.float32)

    width_a = np.linalg.norm(bottom_right - bottom_left)
    width_b = np.linalg.norm(top_right - top_left)
    max_width = int(max(width_a, width_b))

    height_a = np.linalg.norm(top_right - bottom_right)
    height_b = np.linalg.norm(top_left - bottom_left)
    max_height = int(max(height_a, height_b))

    if max_width < 300 or max_height < 300:
        return bgr

    dst = np.array(
        [[0, 0], [max_width - 1, 0], [max_width - 1, max_height - 1], [0, max_height - 1]],
        dtype=np.float32,
    )
    matrix = cv2.getPerspectiveTransform(src, dst)
    return cv2.warpPerspective(bgr, matrix, (max_width, max_height))


def preprocess_image(image_rgb: np.ndarray) -> tuple[np.ndarray, dict[str, float]]:
    if cv2 is None:
        return image_rgb, {}
    if image_rgb.ndim != 3 or image_rgb.shape[2] != 3:
        return image_rgb, {}

    bgr = cv2.cvtColor(image_rgb, cv2.COLOR_RGB2BGR)
    bgr = _try_unwarp_document(bgr)

    gray = cv2.cvtColor(bgr, cv2.COLOR_BGR2GRAY)
    blur_score = float(cv2.Laplacian(gray, cv2.CV_64F).var())

    clahe = cv2.createCLAHE(clipLimit=2.0, tileGridSize=(8, 8))
    lab = cv2.cvtColor(bgr, cv2.COLOR_BGR2LAB)
    l, a, b = cv2.split(lab)
    l = clahe.apply(l)
    bgr = cv2.cvtColor(cv2.merge((l, a, b)), cv2.COLOR_LAB2BGR)

    bgr = _deskew_bgr(bgr)

    out = cv2.cvtColor(bgr, cv2.COLOR_BGR2RGB)
    return out, {"blur_score": blur_score}


def assess_image_quality(image_rgb: np.ndarray) -> dict[str, object]:
    height, width = image_rgb.shape[:2]
    result: dict[str, object] = {
        "width": int(width),
        "height": int(height),
        "score": 70,
        "warnings": [],
    }
    warnings: list[str] = []

    if cv2 is None or image_rgb.ndim != 3:
        result["warnings"] = ["Qualité image non mesurable."]
        return result

    gray = cv2.cvtColor(image_rgb, cv2.COLOR_RGB2GRAY)
    blur_score = float(cv2.Laplacian(gray, cv2.CV_64F).var())
    brightness = float(np.mean(gray))
    contrast = float(np.std(gray))
    min_side = min(width, height)

    score = 100
    if min_side < 900:
        score -= 25
        warnings.append("Résolution faible.")
    if blur_score < 80:
        score -= 35
        warnings.append("Photo floue.")
    elif blur_score < 140:
        score -= 15
        warnings.append("Netteté moyenne.")
    if brightness < 65 or brightness > 220:
        score -= 15
        warnings.append("Luminosité difficile.")
    if contrast < 35:
        score -= 15
        warnings.append("Contraste faible.")

    result.update(
        {
            "score": max(0, min(100, int(score))),
            "blurScore": round(blur_score, 1),
            "brightness": round(brightness, 1),
            "contrast": round(contrast, 1),
            "warnings": warnings,
        }
    )
    return result


def merge_image_quality(qualities: Sequence[dict[str, object]], *, native_pdf: bool = False) -> dict[str, object]:
    if native_pdf:
        return {"score": 100, "warnings": [], "source": "native_pdf"}
    if not qualities:
        return {"score": 0, "warnings": ["Aucune page image analysée."], "source": "unknown"}
    score = int(round(sum(int(q.get("score", 0)) for q in qualities) / len(qualities)))
    warnings: list[str] = []
    for quality in qualities:
        for warning in quality.get("warnings", []):
            if isinstance(warning, str) and warning not in warnings:
                warnings.append(warning)
    return {"score": score, "warnings": warnings, "pages": list(qualities), "source": "image_ocr"}


def ocr_image(image: np.ndarray) -> list[OcrLine]:
    prepared, metrics = preprocess_image(image)
    if metrics:
        log.info("preprocess blur_score=%.1f", metrics.get("blur_score", 0.0))
    lines: list[OcrLine] = []
    for engine in (get_latin_ocr(), get_arabic_ocr()):
        try:
            raw = engine.ocr(prepared, cls=True)
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
        if any(upper.startswith(prefix) for prefix in BUSINESS_PREFIXES) and not any(stop in upper for stop in BUSINESS_STOPWORDS):
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


SUPPORTED_ACCOUNTING_TYPES: dict[str, dict[str, object]] = {
    "supplier_invoice": {
        "required": ["supplier_name", "invoice_number", "issue_date", "total_ttc"],
        "labels": {
            "supplier_name": "Fournisseur",
            "supplier_tax_id": "Matricule fiscal fournisseur",
            "invoice_number": "Numéro facture",
            "issue_date": "Date d'émission",
            "due_date": "Date d'échéance",
            "total_ht": "Total HT",
            "total_tva": "TVA",
            "total_ttc": "Total TTC",
            "currency": "Devise",
        },
    },
    "cash_receipt": {
        "required": ["merchant_name", "issue_date", "total_ttc"],
        "labels": {
            "merchant_name": "Enseigne",
            "receipt_number": "Référence ticket",
            "issue_date": "Date",
            "issue_time": "Heure",
            "payment_method": "Mode de paiement",
            "total_ht": "Total HT",
            "total_tva": "TVA",
            "total_ttc": "Total TTC",
            "currency": "Devise",
        },
    },
    "payment_receipt": {
        "required": ["reference", "issue_date", "amount"],
        "labels": {
            "reference": "Référence",
            "issue_date": "Date",
            "amount": "Montant",
            "issuer_name": "Émetteur",
            "beneficiary_name": "Bénéficiaire",
            "payment_method": "Mode de paiement",
            "currency": "Devise",
        },
    },
    "delivery_note": {
        "required": ["delivery_note_number", "issue_date", "supplier_name"],
        "labels": {
            "delivery_note_number": "Numéro BL",
            "issue_date": "Date",
            "supplier_name": "Fournisseur",
            "order_reference": "Référence commande",
            "client_name": "Client",
            "client_code": "Code client",
            "supplier_tax_id": "Matricule fiscal",
            "payment_method": "Mode de règlement",
            "due_date": "Échéance",
            "brut_ht": "Brut HT",
            "remise_amount": "Remise",
            "net_ht": "Net HT",
            "total_tva": "Total TVA",
            "timbre": "Timbre",
            "total_ttc": "Total TTC",
            "total_to_pay": "Total à payer",
        },
    },
    "expense_report": {
        "required": ["expense_type", "issue_date", "total_ttc"],
        "labels": {
            "expense_owner": "Employé",
            "issue_date": "Date",
            "expense_type": "Nature de dépense",
            "total_ttc": "Montant TTC",
            "vat_recoverable": "TVA récupérable",
            "currency": "Devise",
        },
    },
    "bank_statement": {
        "required": ["iban", "period_label"],
        "labels": {
            "iban": "IBAN",
            "period_label": "Période",
            "currency": "Devise",
        },
    },
}

COMMON_DOCUMENT_LABELS: dict[str, str] = {
    "trade_sense": "Achat / Vente",
    "payment_status": "Statut paiement",
    "document_number": "Numéro document",
    "invoice_number": "Numéro facture",
    "delivery_note_number": "Numéro BL",
    "order_reference": "Référence commande",
    "reference": "Référence",
    "issue_date": "Date",
    "due_date": "Échéance",
    "supplier_name": "Émetteur / Fournisseur",
    "supplier_tax_id": "MF émetteur",
    "supplier_address": "Adresse émetteur",
    "client_name": "Destinataire / Client",
    "client_code": "Code client",
    "client_address": "Adresse destinataire",
    "client_phone": "Téléphone destinataire",
    "payment_method": "Mode de règlement",
    "brut_ht": "Brut HT",
    "remise_amount": "Remise",
    "net_ht": "Net HT",
    "total_ht": "Total HT",
    "total_tva": "Total TVA",
    "timbre": "Timbre",
    "total_ttc": "Total TTC",
    "total_to_pay": "Total à payer",
    "amount": "Montant",
    "currency": "Devise",
    "articles": "Articles",
}


def labels_for(*keys: str) -> dict[str, str]:
    return {key: COMMON_DOCUMENT_LABELS[key] for key in keys}


SUPPORTED_ACCOUNTING_TYPES = {
    "facture": {
        "required": ["document_number", "issue_date", "supplier_name", "client_name", "total_ttc", "trade_sense", "payment_status"],
        "labels": labels_for("trade_sense", "payment_status", "document_number", "invoice_number", "issue_date", "due_date", "supplier_name", "supplier_tax_id", "supplier_address", "client_name", "client_code", "client_address", "payment_method", "total_ht", "total_tva", "timbre", "total_ttc", "total_to_pay", "currency", "articles"),
    },
    "proforma": {
        "required": ["document_number", "issue_date", "supplier_name", "client_name", "total_ttc", "trade_sense"],
        "labels": labels_for("trade_sense", "document_number", "invoice_number", "issue_date", "due_date", "supplier_name", "supplier_tax_id", "client_name", "client_code", "payment_method", "total_ht", "total_tva", "total_ttc", "total_to_pay", "currency", "articles"),
    },
    "avoir": {
        "required": ["document_number", "issue_date", "supplier_name", "total_ttc", "trade_sense", "payment_status"],
        "labels": labels_for("trade_sense", "payment_status", "document_number", "reference", "issue_date", "supplier_name", "supplier_tax_id", "client_name", "total_ht", "total_tva", "total_ttc", "total_to_pay", "currency", "articles"),
    },
    "devis": {
        "required": ["document_number", "issue_date", "supplier_name", "client_name", "total_ttc"],
        "labels": labels_for("document_number", "reference", "issue_date", "due_date", "supplier_name", "supplier_tax_id", "client_name", "client_code", "total_ht", "total_tva", "total_ttc", "total_to_pay", "currency", "articles"),
    },
    "bon_commande": {
        "required": ["document_number", "issue_date", "supplier_name", "client_name"],
        "labels": labels_for("trade_sense", "document_number", "order_reference", "issue_date", "due_date", "supplier_name", "supplier_tax_id", "client_name", "client_code", "payment_method", "total_ht", "total_tva", "total_ttc", "currency", "articles"),
    },
    "bon_livraison": {
        "required": ["document_number", "issue_date", "supplier_name", "client_name"],
        "labels": labels_for("trade_sense", "payment_status", "document_number", "delivery_note_number", "order_reference", "issue_date", "due_date", "supplier_name", "supplier_tax_id", "supplier_address", "client_name", "client_code", "client_address", "client_phone", "payment_method", "brut_ht", "remise_amount", "net_ht", "total_tva", "timbre", "total_ttc", "total_to_pay", "currency", "articles"),
    },
    "bon_sortie": {
        "required": ["document_number", "issue_date"],
        "labels": labels_for("document_number", "issue_date", "supplier_name", "client_name", "order_reference", "articles"),
    },
    "paiement_recu": {
        "required": ["document_number", "issue_date", "amount", "payment_status"],
        "labels": labels_for("payment_status", "document_number", "reference", "issue_date", "supplier_name", "client_name", "payment_method", "amount", "currency"),
    },
    "paiement_emis": {
        "required": ["document_number", "issue_date", "amount", "payment_status"],
        "labels": labels_for("payment_status", "document_number", "reference", "issue_date", "supplier_name", "client_name", "payment_method", "amount", "currency"),
    },
    "ordre_fabrication": {
        "required": ["document_number", "issue_date"],
        "labels": labels_for("document_number", "issue_date", "supplier_name", "client_name", "order_reference", "articles"),
    },
}

ACCOUNTING_TYPE_ALIASES = {
    "supplier_invoice": "facture",
    "cash_receipt": "facture",
    "payment_receipt": "paiement_recu",
    "delivery_note": "bon_livraison",
    "expense_report": "facture",
    "bank_statement": "paiement_emis",
    "credit_note": "avoir",
    "quote": "devis",
    "purchase_order": "bon_commande",
    "other": "facture",
}

GENERIC_STOPWORDS = {
    "FACTURE", "INVOICE", "TICKET", "RECU", "REÇU", "DELIVERY", "BON",
    "TVA", "HT", "TTC", "TOTAL", "DATE", "HEURE", "MERCI", "CLIENT",
    "LIVRAISON", "SIGNATURE",
}


def score_to_percent(score: float) -> int:
    return max(0, min(100, int(round(score * 100))))


def normalize_accounting_type(document_type: str | None) -> str:
    normalized = normalize_space(document_type).lower().replace("-", "_").replace(" ", "_")
    normalized = ACCOUNTING_TYPE_ALIASES.get(normalized, normalized)
    return normalized if normalized in SUPPORTED_ACCOUNTING_TYPES else "facture"


def detect_accounting_type(raw_text: str, requested_type: str) -> str:
    upper = raw_text.upper()
    rules = [
        ("bon_livraison", ["BON DE LIVRAISON", "LIVRAISON"]),
        ("bon_commande", ["BON DE COMMANDE", "COMMANDE"]),
        ("bon_sortie", ["BON DE SORTIE", "SORTIE"]),
        ("ordre_fabrication", ["ORDRE DE FABRICATION", "FABRICATION"]),
        ("paiement_recu", ["PAIEMENT RECU", "REÇU DE PAIEMENT", "RECU DE PAIEMENT", "REÇU"]),
        ("paiement_emis", ["PAIEMENT EMIS", "DÉCAISSEMENT", "DECAISSEMENT"]),
        ("avoir", ["AVOIR", "NOTE DE CREDIT", "NOTE DE CRÉDIT"]),
        ("proforma", ["PROFORMA"]),
        ("devis", ["DEVIS", "OFFRE DE PRIX"]),
        ("facture", ["FACTURE"]),
    ]
    for doc_type, keywords in rules:
        if any(keyword in upper for keyword in keywords):
            return doc_type
    return requested_type


def contains_keyword(text: str, keywords: Iterable[str]) -> bool:
    upper = normalize_space(text).upper()
    return any(keyword.upper() in upper for keyword in keywords)


def search_key(value: str | None) -> str:
    clean = normalize_space(value)
    clean = unicodedata.normalize("NFKD", clean)
    clean = clean.encode("ascii", "ignore").decode("ascii")
    clean = re.sub(r"[^A-Za-z0-9]+", " ", clean).strip().upper()
    return clean


def field_result(
    key: str,
    label: str,
    value: str | None,
    score: float,
    required: bool,
) -> AccountingFieldResult:
    clean_value = normalize_space(value) or None
    confidence = score_to_percent(score) if clean_value else 0
    requires_review = clean_value is None or confidence < 70
    return AccountingFieldResult(
        key=key,
        label=label,
        value=clean_value,
        confidence=confidence,
        required=required,
        requiresReview=requires_review,
    )


def first_date(text: str) -> str | None:
    match = DATE_RE.search(text)
    return match.group(1) if match else None


def first_time(text: str) -> str | None:
    match = TIME_RE.search(text)
    return match.group(0) if match else None


def extract_currency(text: str) -> str | None:
    upper = text.upper()
    if "TND" in upper or "DINAR" in upper:
        return "TND"
    if "EUR" in upper or "€" in text:
        return "EUR"
    if "USD" in upper or "$" in text:
        return "USD"
    return None


def parse_amount_from_text(text: str) -> str | None:
    matches = AMOUNT_RE.findall(text.replace("\xa0", " "))
    if not matches:
        return None
    raw = matches[-1].replace(" ", "")
    return raw.replace(",", ".") if raw.count(",") == 1 and raw.count(".") == 0 else raw.replace(",", "")


def first_regex(lines: list[OcrLine], pattern: re.Pattern[str]) -> tuple[str | None, float]:
    for line in lines:
        match = pattern.search(line.text)
        if match:
            return normalize_space(match.group(1)), line.score
    return None, 0.0


def first_date_line(lines: list[OcrLine], keywords: Iterable[str] | None = None) -> tuple[str | None, float]:
    if keywords:
        for line in lines:
            if contains_keyword(line.text, keywords):
                value = first_date(line.text)
                if value:
                    return value, line.score
    for line in lines:
        value = first_date(line.text)
        if value:
            return value, line.score
    return None, 0.0


def first_time_line(lines: list[OcrLine]) -> tuple[str | None, float]:
    for line in lines:
        value = first_time(line.text)
        if value:
            return value, line.score
    return None, 0.0


def amount_by_keywords(lines: list[OcrLine], keywords: Iterable[str]) -> tuple[str | None, float]:
    for line in lines:
        if contains_keyword(line.text, keywords):
            value = parse_amount_from_text(line.text)
            if value:
                return value, line.score
    return None, 0.0


def first_line_value(lines: list[OcrLine], keywords: Iterable[str], pattern: re.Pattern[str] | None = None) -> tuple[str | None, float]:
    for line in lines:
        if not contains_keyword(line.text, keywords):
            continue
        if pattern:
            match = pattern.search(line.text)
            if match:
                return normalize_space(match.group(1)), line.score
        cleaned = normalize_space(line.text)
        for keyword in keywords:
            if keyword.upper() in cleaned.upper():
                candidate = re.sub(re.escape(keyword), "", cleaned, flags=re.IGNORECASE)
                candidate = candidate.strip(" :-#")
                if candidate:
                    return candidate, line.score
    return None, 0.0


def guess_party_name(lines: list[OcrLine]) -> tuple[str | None, float]:
    for line in lines:
        clean = normalize_space(line.text).strip('"')
        upper = clean.upper()
        if len(clean) < 4:
            continue
        if any(stop in upper for stop in GENERIC_STOPWORDS):
            continue
        if sum(ch.isdigit() for ch in clean) > 5:
            continue
        return clean, line.score
    return None, 0.0


def first_iban(lines: list[OcrLine]) -> tuple[str | None, float]:
    return first_regex(lines, IBAN_RE)


def extract_payment_method(lines: list[OcrLine]) -> tuple[str | None, float]:
    mapping = {
        "Carte bancaire": ["CARTE", "CB", "VISA", "MASTERCARD"],
        "Espèces": ["ESPECES", "ESPÈCES", "CASH"],
        "Virement": ["VIREMENT"],
        "Chèque": ["CHEQUE", "CHÈQUE"],
    }
    for line in lines:
        upper = line.text.upper()
        for label, keywords in mapping.items():
            if any(keyword in upper for keyword in keywords):
                return label, line.score
    return None, 0.0


def extract_period_label(lines: list[OcrLine], raw_text: str) -> tuple[str | None, float]:
    period_match = re.search(r"(du\s+\d{2}/\d{2}/\d{4}\s+au\s+\d{2}/\d{2}/\d{4})", raw_text, re.IGNORECASE)
    if period_match:
        return period_match.group(1), 0.75
    dates = DATE_RE.findall(raw_text)
    if len(dates) >= 2:
        return f"Du {dates[0]} au {dates[1]}", 0.65
    return None, 0.0


PARTY_LABELS_SUPPLIER = (
    "fournisseur", "emetteur", "vendeur", "supplier", "vendor", "seller",
    "raison sociale", "societe", "ste",
)
PARTY_LABELS_CLIENT = (
    "client", "destinataire", "acheteur", "customer", "buyer", "bill to",
    "ship to", "livre a", "facture a", "adresse livraison",
)
PARTY_NOISE_KEYS = {
    "SIGNATURE", "BON DE LIVRAISON", "FACTURE", "AVOIR", "DEVIS", "DATE",
    "NUMERO", "REFERENCE", "CODE CLIENT", "MODE REGLEMENT", "ECHEANCE",
    "COMMERCIAL", "COMMANDE", "TOTAL", "TVA", "HT", "TTC", "TIMBRE",
    "ARTICLE", "DESIGNATION", "PRIX", "QTE", "UNITE",
}
PAYMENT_LABELS = (
    "mode reglement", "mode de reglement", "mode paiement", "mode de paiement",
    "payment method", "reglement", "paiement",
)
DUE_DATE_LABELS = (
    "echeance", "date echeance", "date d echeance", "due date", "terme",
    "delai paiement", "delai de paiement",
)
DUE_TERM_RE = re.compile(r"\b(\d{1,3}\s*(?:j|jour|jours|days?)|fin\s+de\s+mois|comptant)\b", re.IGNORECASE)


def normalize_tax_id(value: str | None) -> str | None:
    if not value:
        return None
    clean = re.sub(r"[^0-9A-Za-z]", "", value).upper()
    return clean if len(clean) >= 8 else None


def clean_party_candidate(value: str | None) -> str | None:
    clean = normalize_space(value).strip(":-#|\"'")
    if not clean:
        return None
    key = search_key(clean)
    for label in (*PARTY_LABELS_SUPPLIER, *PARTY_LABELS_CLIENT):
        label_key = search_key(label)
        if key.startswith(label_key):
            clean = normalize_space(clean[len(label):]).strip(":-#|\"'")
            key = search_key(clean)
    return clean if clean and key else None


def is_party_candidate(value: str | None) -> bool:
    clean = clean_party_candidate(value)
    if not clean:
        return False
    key = search_key(clean)
    if len(clean) < 3 or key in PARTY_NOISE_KEYS:
        return False
    if any(noise in key for noise in PARTY_NOISE_KEYS):
        return False
    if MF_LONG_RE.search(clean) or IBAN_RE.search(clean) or DATE_RE.search(clean):
        return False
    if PHONE_RE.search(clean):
        return False
    if sum(ch.isdigit() for ch in clean) > max(2, len(clean) // 3):
        return False
    return any(ch.isalpha() for ch in clean)


def labeled_party_value(lines: Sequence[OcrLine], labels: Sequence[str]) -> tuple[str | None, float]:
    dims = page_dimensions(lines)
    if dims is None:
        return None, 0.0
    width, height = dims
    label_lines = [
        (label_match_score(line.text, labels), line)
        for line in lines
        if line.bounds() is not None
    ]
    label_lines = [(score, line) for score, line in label_lines if score >= 0.72]
    if not label_lines:
        return None, 0.0

    best: tuple[str, float, float] | None = None
    for label_score, label_line in label_lines:
        label_bounds = label_line.bounds()
        if label_bounds is None:
            continue
        lx0, ly0, lx1, ly1 = label_bounds
        label_y = (ly0 + ly1) / 2

        inline = clean_party_candidate(label_line.text)
        if inline and is_party_candidate(inline) and label_match_score(inline, labels) < 0.60:
            best = (inline, min(0.88, label_line.score), 0.0)

        for candidate in lines:
            if candidate is label_line:
                continue
            cb = candidate.bounds()
            if cb is None:
                continue
            cx0, cy0, cx1, cy1 = cb
            candidate_y = (cy0 + cy1) / 2
            row_dy = abs(candidate_y - label_y)
            right_dx = cx0 - lx1
            below_dy = cy0 - ly1
            overlap_x = max(0.0, min(lx1, cx1) - max(lx0, cx0))
            is_right = -2 <= right_dx <= 0.42 * width and row_dy <= 0.05 * height
            is_below = 0 <= below_dy <= 0.14 * height and (overlap_x > 0 or abs((cx0 + cx1 - lx0 - lx1) / 2) <= 0.16 * width)
            if not (is_right or is_below):
                continue
            value = clean_party_candidate(candidate.text)
            if not is_party_candidate(value):
                continue
            dist = (right_dx if is_right else below_dy) + row_dy - (label_score * 10)
            if best is None or dist < best[2]:
                best = (value or "", min(0.92, candidate.score * (0.8 + label_score * 0.15)), dist)

    return (best[0], best[1]) if best else (None, 0.0)


def zone_party_value(lines: Sequence[OcrLine]) -> tuple[str | None, float]:
    business = extract_business_name(list(lines))
    if business and is_party_candidate(business):
        return business, 0.84
    for line in sorted(lines, key=lambda item: (item.y_mid() or 0, item.x_mid() or 0)):
        value = clean_party_candidate(line.text)
        if is_party_candidate(value):
            return value, line.score
    return None, 0.0


def extract_parties_robust(zones: dict[str, list[OcrLine]], lines: Sequence[OcrLine]) -> tuple[str | None, float, str | None, float]:
    header = zones.get("header") or list(lines)
    header_left = zones.get("header_left") or header
    header_right = zones.get("header_right") or header

    supplier, supplier_score = labeled_party_value(header, PARTY_LABELS_SUPPLIER)
    client, client_score = labeled_party_value(header, PARTY_LABELS_CLIENT)

    if not supplier:
        supplier, supplier_score = zone_party_value(header_left)
    if not client:
        client, client_score = zone_party_value(header_right)

    if supplier and client and search_key(supplier) == search_key(client):
        right_client, right_score = zone_party_value([line for line in header_right if search_key(line.text) != search_key(supplier)])
        if right_client:
            client, client_score = right_client, right_score
    return supplier, supplier_score, client, client_score


def extract_tax_id_robust(preferred_lines: Sequence[OcrLine], all_lines: Sequence[OcrLine]) -> tuple[str | None, float]:
    for source_lines, boost in ((preferred_lines, 0.08), (all_lines, 0.0)):
        for line in source_lines:
            match = MF_LONG_RE.search(line.text)
            if match:
                return normalize_tax_id(match.group(1)), min(0.98, line.score + boost)
        value, score = generic_label_value(
            source_lines,
            ["matricule fiscal", "mf", "identifiant fiscal", "tva", "tax id"],
            value_pattern=MF_LONG_RE,
            min_label_score=0.62,
        )
        normalized = normalize_tax_id(value)
        if normalized:
            return normalized, min(0.95, score + boost)
    return None, 0.0


def extract_due_date_robust(header_lines: Sequence[OcrLine], all_lines: Sequence[OcrLine], issue_date: str | None) -> tuple[str | None, float]:
    for source_lines, boost in ((header_lines, 0.08), (all_lines, 0.0)):
        value, score = generic_label_value(
            source_lines,
            DUE_DATE_LABELS,
            value_pattern=DATE_RE,
            min_label_score=0.66,
        )
        if value and value != issue_date:
            return value, min(0.95, score + boost)
        term, term_score = generic_label_value(
            source_lines,
            DUE_DATE_LABELS,
            value_pattern=DUE_TERM_RE,
            min_label_score=0.66,
        )
        if term:
            return normalize_space(term), min(0.86, term_score + boost)
    return None, 0.0


def normalize_payment_method(value: str | None) -> str | None:
    key = search_key(value)
    if not key:
        return None
    if any(token in key for token in ("ESPECE", "CASH")):
        return "Espèces"
    if any(token in key for token in ("CHEQUE", "CHQ")):
        return "Chèque"
    if any(token in key for token in ("VIREMENT", "TRANSFER", "BANK")):
        return "Virement"
    if any(token in key for token in ("CARTE", "CB", "VISA", "MASTERCARD", "TPE")):
        return "Carte bancaire"
    if any(token in key for token in ("TRAITE", "EFFET", "LCR")):
        return "Traite"
    if "COMPTANT" in key:
        return "Comptant"
    if key in {"ECHEANCE", "DATE", "COMMERCIAL", "COMMANDE", "CODE CLIENT"}:
        return None
    return normalize_space(value)


def extract_payment_method_robust(header_lines: Sequence[OcrLine], all_lines: Sequence[OcrLine]) -> tuple[str | None, float]:
    direct, direct_score = extract_payment_method(list(all_lines))
    if direct:
        return direct, direct_score
    for source_lines, boost in ((header_lines, 0.08), (all_lines, 0.0)):
        value, score = generic_label_value(
            source_lines,
            PAYMENT_LABELS,
            min_label_score=0.64,
        )
        normalized = normalize_payment_method(value)
        if normalized:
            return normalized, min(0.90, score + boost)
    return None, 0.0


def page_dimensions(lines: Sequence[OcrLine]) -> tuple[float, float] | None:
    bounds = [line.bounds() for line in lines]
    bounds = [b for b in bounds if b is not None]
    if not bounds:
        return None
    max_x = max(b[2] for b in bounds)
    max_y = max(b[3] for b in bounds)
    if max_x <= 0 or max_y <= 0:
        return None
    return max_x, max_y


def split_layout_zones(lines: Sequence[OcrLine]) -> dict[str, list[OcrLine]]:
    dims = page_dimensions(lines)
    if dims is None:
        return {"all": list(lines), "header": list(lines), "body": [], "footer": []}
    width, height = dims
    header_y = 0.35 * height
    footer_y = 0.72 * height

    header: list[OcrLine] = []
    footer: list[OcrLine] = []
    body: list[OcrLine] = []

    for line in lines:
        y = line.y_mid()
        if y is None:
            body.append(line)
        elif y <= header_y:
            header.append(line)
        elif y >= footer_y:
            footer.append(line)
        else:
            body.append(line)

    return {
        "all": list(lines),
        "header": header,
        "body": body,
        "footer": footer,
        "header_left": [l for l in header if (l.x_mid() or 0) < 0.5 * width],
        "header_right": [l for l in header if (l.x_mid() or 0) >= 0.5 * width],
        "footer_left": [l for l in footer if (l.x_mid() or 0) < 0.5 * width],
        "footer_right": [l for l in footer if (l.x_mid() or 0) >= 0.5 * width],
    }


def best_value_below_label(
    lines: Sequence[OcrLine],
    label_keywords: Sequence[str],
    value_pattern: re.Pattern[str] | None = None,
) -> tuple[str | None, float]:
    dims = page_dimensions(lines)
    if dims is None:
        return first_line_value(list(lines), label_keywords, pattern=value_pattern)

    _, height = dims
    max_delta_y = 0.08 * height

    label_candidates = [l for l in lines if contains_keyword(l.text, label_keywords)]
    label_candidates = [l for l in label_candidates if l.bounds() is not None]
    if not label_candidates:
        return first_line_value(list(lines), label_keywords, pattern=value_pattern)

    label_line = sorted(label_candidates, key=lambda l: l.bounds()[1] if l.bounds() else 0)[0]
    label_bounds = label_line.bounds()
    if label_bounds is None:
        return first_line_value(list(lines), label_keywords, pattern=value_pattern)

    lx0, _, lx1, ly1 = label_bounds
    best: tuple[str, float, float] | None = None  # (value, score, delta_y)

    for candidate in lines:
        cb = candidate.bounds()
        if cb is None:
            continue
        cx0, cy0, cx1, _ = cb
        if cy0 <= ly1:
            continue
        delta_y = cy0 - ly1
        if delta_y > max_delta_y:
            continue
        overlap = max(0.0, min(lx1, cx1) - max(lx0, cx0))
        if overlap <= 0:
            continue

        text = candidate.text
        if value_pattern:
            match = value_pattern.search(text)
            if not match:
                continue
            value = normalize_space(match.group(1))
        else:
            value = normalize_space(text)

        if not value:
            continue

        if best is None or delta_y < best[2]:
            best = (value, candidate.score, delta_y)

    if best:
        return best[0], best[1]

    return first_line_value(list(lines), label_keywords, pattern=value_pattern)


def best_value_below_fuzzy_label(
    lines: Sequence[OcrLine],
    label_text: str,
    value_pattern: re.Pattern[str] | None = None,
    min_ratio: float = 0.72,
) -> tuple[str | None, float]:
    dims = page_dimensions(lines)
    if dims is None:
        return None, 0.0

    target = normalize_space(label_text).upper()
    if not target:
        return None, 0.0

    label_candidates: list[tuple[float, OcrLine]] = []
    for line in lines:
        if line.bounds() is None:
            continue
        candidate = normalize_space(re.sub(r"\d", "", line.text)).upper()
        if not candidate:
            continue
        ratio = SequenceMatcher(None, candidate, target).ratio()
        if ratio >= min_ratio:
            label_candidates.append((ratio, line))

    if not label_candidates:
        return None, 0.0

    # Best fuzzy match, then same "value below" logic.
    label_line = sorted(label_candidates, key=lambda item: item[0], reverse=True)[0][1]
    label_bounds = label_line.bounds()
    if label_bounds is None:
        return None, 0.0

    _, height = dims
    max_delta_y = 0.08 * height

    lx0, _, lx1, ly1 = label_bounds
    best: tuple[str, float, float] | None = None

    for candidate_line in lines:
        cb = candidate_line.bounds()
        if cb is None:
            continue
        cx0, cy0, cx1, _ = cb
        if cy0 <= ly1:
            continue
        delta_y = cy0 - ly1
        if delta_y > max_delta_y:
            continue
        overlap = max(0.0, min(lx1, cx1) - max(lx0, cx0))
        if overlap <= 0:
            continue

        if value_pattern:
            match = value_pattern.search(candidate_line.text)
            if not match:
                continue
            value = normalize_space(match.group(1))
        else:
            value = normalize_space(candidate_line.text)

        if not value:
            continue

        if best is None or delta_y < best[2]:
            best = (value, candidate_line.score, delta_y)

    if best:
        return best[0], best[1]
    return None, 0.0


def best_value_near_fuzzy_label(
    lines: Sequence[OcrLine],
    label_text: str,
    value_pattern: re.Pattern[str] | None = None,
    min_ratio: float = 0.72,
) -> tuple[str | None, float]:
    dims = page_dimensions(lines)
    if dims is None:
        return None, 0.0
    width, height = dims

    target = normalize_space(label_text).upper()
    if not target:
        return None, 0.0

    label_candidates: list[tuple[float, OcrLine]] = []
    for line in lines:
        if line.bounds() is None:
            continue
        candidate = normalize_space(re.sub(r"\d", "", line.text)).upper()
        if not candidate:
            continue
        ratio = SequenceMatcher(None, candidate, target).ratio()
        if ratio >= min_ratio:
            label_candidates.append((ratio, line))

    if not label_candidates:
        return None, 0.0

    label_line = sorted(label_candidates, key=lambda item: item[0], reverse=True)[0][1]
    label_bounds = label_line.bounds()
    if label_bounds is None:
        return None, 0.0

    lx0, ly0, lx1, ly1 = label_bounds
    label_y_mid = (ly0 + ly1) / 2

    max_right_dx = 0.35 * width
    max_below_dy = 0.12 * height
    max_row_dy = 0.04 * height

    best: tuple[str, float, float] | None = None  # (value, score, dist)

    for candidate_line in lines:
        cb = candidate_line.bounds()
        if cb is None:
            continue
        cx0, cy0, cx1, cy1 = cb
        cy_mid = (cy0 + cy1) / 2

        # Prefer values on the same row, to the right.
        right_dx = cx0 - lx1
        row_dy = abs(cy_mid - label_y_mid)
        is_right = right_dx >= -2 and right_dx <= max_right_dx and row_dy <= max_row_dy

        # Also consider values just below the label, same column-ish.
        below_dy = cy0 - ly1
        overlap_x = max(0.0, min(lx1, cx1) - max(lx0, cx0))
        is_below = below_dy > 0 and below_dy <= max_below_dy and overlap_x > 0

        if not (is_right or is_below):
            continue

        text = candidate_line.text
        if value_pattern:
            match = value_pattern.search(text)
            if not match:
                continue
            value = normalize_space(match.group(1))
        else:
            value = normalize_space(text)

        if not value:
            continue

        dist = (right_dx if is_right else below_dy) + row_dy
        if best is None or dist < best[2]:
            best = (value, candidate_line.score, dist)

    if best:
        return best[0], best[1]
    return None, 0.0


DOCUMENT_NUMBER_RE = re.compile(r"\b([A-Z]{0,6}[-/]?\d{3,}[A-Z0-9./-]*)\b", re.IGNORECASE)
CLIENT_CODE_RE = re.compile(r"\b((?=[A-Z0-9]*\d)[A-Z0-9]{5,})\b", re.IGNORECASE)


def label_match_score(text: str, labels: Sequence[str]) -> float:
    candidate = search_key(re.sub(r"\d", "", text))
    if not candidate:
        return 0.0

    best = 0.0
    for label in labels:
        target = search_key(label)
        if not target:
            continue
        if target in candidate:
            best = max(best, 1.0)
            continue
        if len(target) > 4 and candidate in target:
            best = max(best, 0.9)
            continue
        best = max(best, SequenceMatcher(None, candidate, target).ratio())
    return best


def clean_candidate_value(
    raw_value: str,
    *,
    value_pattern: re.Pattern[str] | None = None,
    amount: bool = False,
) -> str | None:
    value = normalize_space(raw_value).strip(" :-#|")
    if not value:
        return None

    if value_pattern:
        match = value_pattern.search(value)
        if not match:
            return None
        value = normalize_space(match.group(1))

    if amount:
        return parse_amount_from_text(value)

    return value


def is_bad_document_number(value: str | None) -> bool:
    if not value:
        return True
    key = search_key(value)
    if len(value) < 3:
        return True
    if not any(ch.isdigit() for ch in value):
        return True
    if key in {"BON", "BON DE", "LIVRAISON", "BON DE LIVRAISON", "FACTURE", "NUMERO", "NO", "N"}:
        return True
    return False


def generic_label_value(
    lines: Sequence[OcrLine],
    labels: Sequence[str],
    *,
    value_pattern: re.Pattern[str] | None = None,
    amount: bool = False,
    min_label_score: float = 0.72,
    reject_label_like: bool = True,
) -> tuple[str | None, float]:
    dims = page_dimensions(lines)
    if dims is None:
        return None, 0.0

    width, height = dims
    label_lines = [
        (label_match_score(line.text, labels), line)
        for line in lines
        if line.bounds() is not None
    ]
    label_lines = [(score, line) for score, line in label_lines if score >= min_label_score]
    if not label_lines:
        return None, 0.0

    best: tuple[str, float, float] | None = None

    for label_score, label_line in label_lines:
        label_bounds = label_line.bounds()
        if label_bounds is None:
            continue

        inline = label_line.text
        for label in labels:
            inline = re.sub(re.escape(label), " ", inline, flags=re.IGNORECASE)
        inline_value = clean_candidate_value(inline, value_pattern=value_pattern, amount=amount)
        if inline_value:
            field_score = min(0.99, label_line.score * (0.78 + label_score * 0.18))
            candidate_rank = 0.0
            if best is None or field_score - candidate_rank > best[2]:
                best = (inline_value, field_score, field_score - candidate_rank)

        lx0, ly0, lx1, ly1 = label_bounds
        label_y = (ly0 + ly1) / 2

        for candidate_line in lines:
            if candidate_line is label_line:
                continue
            candidate_bounds = candidate_line.bounds()
            if candidate_bounds is None:
                continue

            cx0, cy0, cx1, cy1 = candidate_bounds
            candidate_y = (cy0 + cy1) / 2
            row_dy = abs(candidate_y - label_y)
            right_dx = cx0 - lx1
            left_dx = lx0 - cx1
            below_dy = cy0 - ly1
            overlap_x = max(0.0, min(lx1, cx1) - max(lx0, cx0))

            relation_bonus = 0.0
            distance_penalty = 0.0

            if -2 <= right_dx <= 0.45 * width and row_dy <= 0.045 * height:
                relation_bonus = 0.28
                distance_penalty = (right_dx / width) + (row_dy / height)
            elif -2 <= below_dy <= 0.10 * height and overlap_x > 0:
                relation_bonus = 0.22
                distance_penalty = (below_dy / height) + (abs((cx0 + cx1 - lx0 - lx1) / 2) / width)
            elif 0 <= left_dx <= 0.35 * width and row_dy <= 0.045 * height:
                relation_bonus = 0.10
                distance_penalty = (left_dx / width) + (row_dy / height)
            else:
                continue

            if reject_label_like and label_match_score(candidate_line.text, labels) >= min_label_score:
                continue

            value = clean_candidate_value(candidate_line.text, value_pattern=value_pattern, amount=amount)
            if not value:
                continue

            field_score = min(
                0.99,
                candidate_line.score * (0.55 + relation_bonus + label_score * 0.15) - distance_penalty,
            )
            if field_score <= 0:
                continue

            if best is None or field_score > best[2]:
                best = (value, field_score, field_score)

    if best:
        return best[0], best[1]
    return None, 0.0


def generic_first_pattern(
    lines: Sequence[OcrLine],
    pattern: re.Pattern[str],
    *,
    forbidden_labels: Sequence[str] = (),
    amount: bool = False,
) -> tuple[str | None, float]:
    for line in lines:
        if forbidden_labels and label_match_score(line.text, forbidden_labels) >= 0.82:
            continue
        value = clean_candidate_value(line.text, value_pattern=pattern, amount=amount)
        if value:
            return value, line.score
    return None, 0.0


def extract_document_number_generic(lines: Sequence[OcrLine]) -> tuple[str | None, float]:
    labels = [
        "numero",
        "numero document",
        "n document",
        "n bl",
        "bon de livraison",
        "facture",
        "reference",
        "ref",
        "document no",
        "piece",
    ]
    value, score = generic_label_value(
        lines,
        labels,
        value_pattern=DOCUMENT_NUMBER_RE,
        min_label_score=0.68,
    )
    if not is_bad_document_number(value):
        return value, score

    for line in lines:
        if line.bounds() is None:
            continue
        key = search_key(line.text)
        if any(token in key for token in ("FACTURE", "BON", "LIVRAISON", "NUMERO", "REF", "REFERENCE")):
            matches = DOCUMENT_NUMBER_RE.findall(line.text)
            for candidate in matches:
                if not is_bad_document_number(candidate):
                    return candidate, line.score

    return None, 0.0


def extract_total_generic(lines: Sequence[OcrLine], labels: Sequence[str]) -> tuple[str | None, float]:
    value, score = generic_label_value(
        lines,
        labels,
        value_pattern=AMOUNT_RE,
        amount=True,
        min_label_score=0.70,
    )
    if value:
        return value, score
    return None, 0.0


PHONE_RE = re.compile(r"\b(?:\+?216[\s.-]*)?(\d{2}[\s.-]?\d{3}[\s.-]?\d{3})\b")


def extract_phone(lines: Sequence[OcrLine]) -> tuple[str | None, float]:
    for line in lines:
        match = PHONE_RE.search(line.text)
        if match:
            return normalize_space(match.group(0)), line.score
    return None, 0.0


def extract_articles_json(body_lines: Sequence[OcrLine]) -> tuple[str | None, float]:
    rows: list[dict[str, object]] = []
    for line in body_lines:
        text = normalize_space(line.text)
        upper = text.upper()
        if len(text) < 8:
            continue
        if any(label in upper for label in ("REFERENCE", "DESIGNATION", "TOTAL", "TVA", "PRIX", "QTE", "QTÉ")):
            continue
        amounts = AMOUNT_RE.findall(text)
        if not amounts:
            continue
        numbers = [parse_amount_from_text(value) for value in amounts]
        numbers = [value for value in numbers if value]
        if not numbers:
            continue
        designation = re.sub(AMOUNT_RE, " ", text)
        designation = normalize_space(re.sub(r"\b[A-Z0-9./-]{4,}\b", " ", designation)).strip(" -")
        if len(designation) < 3:
            continue
        row: dict[str, object] = {"designation": designation}
        if len(numbers) >= 1:
            row["total"] = numbers[-1]
        if len(numbers) >= 2:
            row["prix_unitaire"] = numbers[-2]
        rows.append(row)
        if len(rows) >= 20:
            break
    if not rows:
        return None, 0.0
    return json.dumps(rows, ensure_ascii=False), 0.65


TABLE_COLUMN_ALIASES: dict[str, tuple[str, ...]] = {
    "reference": ("REFERENCE", "REF", "CODE", "CODE ARTICLE", "ARTICLE"),
    "designation": ("DESIGNATION", "DESIGNATION ARTICLE", "LIBELLE", "DESCRIPTION", "PRODUIT", "ARTICLE"),
    "unite": ("UNITE", "UN", "U"),
    "quantite": ("QTE", "QTY", "QUANTITE", "QUANT"),
    "prix_unitaire_ht": ("PU", "P U", "PU HT", "PRIX UNIT", "PRIX UNITAIRE", "PRIX UNIT HT", "UNIT HT", "P U NET HT"),
    "remise": ("REMISE", "DISCOUNT", "RABAIS"),
    "taux_tva": ("TVA", "TAUX TVA", "% TVA", "TAX"),
    "total_ht": ("TOTAL", "TOTAL HT", "MONTANT", "MONTANT HT", "NET HT"),
}

ARTICLE_FOOTER_KEYS = {
    "TOTAL", "TOTAL HT", "TOTAL TTC", "NET HT", "BRUT HT", "BASE HT", "TVA",
    "MONTANT TVA", "TIMBRE", "TOTAL A PAYER", "NET A PAYER", "REMISE",
}


def table_column_for_text(text: str) -> str | None:
    key = search_key(text)
    best: tuple[str, int] | None = None
    for column, aliases in TABLE_COLUMN_ALIASES.items():
        for alias in aliases:
            alias_key = search_key(alias)
            if alias_key and (alias_key == key or alias_key in key):
                if best is None or len(alias_key) > best[1]:
                    best = (column, len(alias_key))
    return best[0] if best else None


def group_ocr_rows(lines: Sequence[OcrLine]) -> list[list[OcrLine]]:
    positioned = [line for line in lines if line.bounds() is not None]
    if not positioned:
        return []
    heights = []
    for line in positioned:
        bounds = line.bounds()
        if bounds:
            heights.append(max(1.0, bounds[3] - bounds[1]))
    median_height = sorted(heights)[len(heights) // 2] if heights else 12.0
    threshold = max(8.0, median_height * 0.75)

    rows: list[list[OcrLine]] = []
    for line in sorted(positioned, key=lambda item: (item.y_mid() or 0, item.x_mid() or 0)):
        y = line.y_mid() or 0
        if not rows:
            rows.append([line])
            continue
        previous_y = sum(item.y_mid() or y for item in rows[-1]) / len(rows[-1])
        if abs(y - previous_y) <= threshold:
            rows[-1].append(line)
        else:
            rows.append([line])
    return [sorted(row, key=lambda item: item.x_mid() or 0) for row in rows]


def table_row_text(row: Sequence[OcrLine]) -> str:
    return normalize_space(" ".join(line.text for line in sorted(row, key=lambda item: item.x_mid() or 0)))


def table_row_is_footer(row: Sequence[OcrLine]) -> bool:
    key = search_key(table_row_text(row))
    if not key:
        return True
    if key.startswith(("TOTAL", "NET HT", "BRUT HT", "BASE HT", "TIMBRE")):
        return True
    return any(footer_key in key for footer_key in ARTICLE_FOOTER_KEYS) and "DESIGNATION" not in key and "ARTICLE" not in key


def infer_table_columns(rows: Sequence[Sequence[OcrLine]]) -> tuple[int, list[tuple[str, float]]]:
    best_index = -1
    best_columns: list[tuple[str, float]] = []
    best_score = 0
    for index, row in enumerate(rows):
        columns: list[tuple[str, float]] = []
        found: set[str] = set()
        for line in row:
            column = table_column_for_text(line.text)
            x = line.x_mid()
            if not column or x is None or column in found:
                continue
            found.add(column)
            columns.append((column, x))
        row_key = search_key(table_row_text(row))
        score = len(found)
        if "DESIGNATION" in row_key or "LIBELLE" in row_key:
            score += 2
        if "QTE" in row_key or "QUANTITE" in row_key:
            score += 1
        if "PRIX" in row_key or "PU" in row_key:
            score += 1
        if score > best_score and len(found) >= 2:
            best_index = index
            best_columns = columns
            best_score = score
    return best_index, sorted(best_columns, key=lambda item: item[1])


def nearest_article_column(x: float, columns: Sequence[tuple[str, float]]) -> str | None:
    return min(columns, key=lambda item: abs(item[1] - x))[0] if columns else None


def clean_article_text(value: str | None) -> str | None:
    clean = normalize_space(value).strip(":-| ")
    return clean or None


def article_number(value: str | None) -> str | None:
    if not value:
        return None
    parsed = parse_amount_from_text(value)
    return parsed if parsed and re.search(r"\d", parsed) else None


def normalize_article_row(cells: dict[str, str], score: float) -> dict[str, object] | None:
    all_text = normalize_space(" ".join(cells.values()))
    reference = clean_article_text(cells.get("reference"))
    designation = clean_article_text(cells.get("designation"))
    unite = clean_article_text(cells.get("unite"))
    quantite = article_number(cells.get("quantite"))
    prix_unitaire_ht = article_number(cells.get("prix_unitaire_ht"))
    remise = article_number(cells.get("remise"))
    taux_tva = article_number(cells.get("taux_tva"))
    total_ht = article_number(cells.get("total_ht"))

    if not reference:
        reference_match = re.search(r"\b[A-Z0-9][A-Z0-9./-]{3,}\b", all_text)
        reference = reference_match.group(0) if reference_match else None

    amounts = [parse_amount_from_text(value) for value in AMOUNT_RE.findall(all_text)]
    amounts = [value for value in amounts if value]
    if not total_ht and amounts:
        total_ht = amounts[-1]
    if not prix_unitaire_ht and len(amounts) >= 2:
        prix_unitaire_ht = amounts[-2]
    if not quantite and len(amounts) >= 3:
        quantite = amounts[0]

    if designation:
        designation = normalize_space(re.sub(AMOUNT_RE, " ", designation)).strip(":-| ")
    if not designation:
        free_text = " ".join(
            value for key, value in cells.items()
            if key not in {"reference", "unite", "quantite", "prix_unitaire_ht", "remise", "taux_tva", "total_ht"}
        )
        if reference:
            free_text = free_text.replace(reference, " ", 1)
        designation = clean_article_text(re.sub(AMOUNT_RE, " ", free_text))

    if not any([reference, designation, quantite, prix_unitaire_ht, total_ht]) or (not designation and not reference):
        return None

    article: dict[str, object] = {}
    if reference:
        article["reference"] = reference
    if designation:
        article["designation"] = designation
    if unite:
        article["unite"] = unite
    if quantite:
        article["quantite"] = quantite
    if prix_unitaire_ht:
        article["prix_unitaire_ht"] = prix_unitaire_ht
    if remise:
        article["remise"] = remise
    if taux_tva:
        article["taux_tva"] = taux_tva
    if total_ht:
        article["total_ht"] = total_ht
    article["confidence"] = max(0.0, min(0.99, round(score, 2)))
    return article


def fallback_article_from_line(line: OcrLine) -> dict[str, object] | None:
    text = normalize_space(line.text)
    if len(text) < 8 or table_row_is_footer([line]):
        return None
    key = search_key(text)
    if any(token in key for token in ("REFERENCE DESIGNATION", "DESIGNATION ARTICLE", "PRIX UNIT", "TAUX TVA")):
        return None

    amounts = [parse_amount_from_text(value) for value in AMOUNT_RE.findall(text)]
    amounts = [value for value in amounts if value]
    if not amounts:
        return None

    reference_match = re.search(r"\b[A-Z0-9][A-Z0-9./-]{3,}\b", text)
    reference = reference_match.group(0) if reference_match else None
    designation = text.replace(reference, " ", 1) if reference else text
    designation = normalize_space(re.sub(AMOUNT_RE, " ", designation)).strip(":-| ")
    if len(designation) < 3:
        return None

    cells: dict[str, str] = {"designation": designation}
    if reference:
        cells["reference"] = reference
    if len(amounts) >= 1:
        cells["total_ht"] = amounts[-1]
    if len(amounts) >= 2:
        cells["prix_unitaire_ht"] = amounts[-2]
    if len(amounts) >= 3:
        cells["quantite"] = amounts[0]
    return normalize_article_row(cells, min(0.72, line.score))


def extract_articles_json_structured(lines: Sequence[OcrLine]) -> tuple[str | None, float]:
    ocr_rows = group_ocr_rows(lines)
    header_index, columns = infer_table_columns(ocr_rows)
    articles: list[dict[str, object]] = []

    if header_index >= 0 and len(columns) >= 2:
        for row in ocr_rows[header_index + 1:]:
            if table_row_is_footer(row):
                if articles:
                    break
                continue
            cells: dict[str, str] = {}
            scores: list[float] = []
            for line in row:
                x = line.x_mid()
                if x is None:
                    continue
                column = nearest_article_column(x, columns)
                if not column:
                    continue
                cells[column] = normalize_space(f"{cells.get(column, '')} {line.text}")
                scores.append(line.score)
            article = normalize_article_row(cells, sum(scores) / len(scores) if scores else 0.65)
            if article:
                articles.append(article)
            if len(articles) >= 50:
                break

    if not articles:
        dims = page_dimensions(lines)
        _, page_height = dims if dims else (0.0, 0.0)
        for line in sorted(lines, key=lambda item: (item.y_mid() or 0, item.x_mid() or 0)):
            y = line.y_mid()
            if page_height and y is not None and (y < 0.22 * page_height or y > 0.82 * page_height):
                continue
            article = fallback_article_from_line(line)
            if article:
                articles.append(article)
            if len(articles) >= 50:
                break

    deduped: list[dict[str, object]] = []
    seen: set[str] = set()
    for article in articles:
        key = search_key(f"{article.get('reference', '')} {article.get('designation', '')}")
        if not key or key in seen:
            continue
        seen.add(key)
        deduped.append(article)

    if not deduped:
        return None, 0.0

    completeness = 0.0
    for article in deduped:
        completeness += sum(1 for key in ("designation", "quantite", "prix_unitaire_ht", "total_ht") if article.get(key)) / 4
    completeness = completeness / len(deduped)
    score = 0.72 + min(0.18, completeness * 0.18)
    if header_index >= 0 and len(columns) >= 3:
        score += 0.06
    return json.dumps(deduped, ensure_ascii=False), min(0.92, score)


def infer_trade_sense(document_type: str, raw_text: str) -> tuple[str | None, float]:
    upper = raw_text.upper()
    if document_type in {"paiement_recu"}:
        return "vente", 0.55
    if document_type in {"paiement_emis"}:
        return "achat", 0.55
    if any(token in upper for token in ("FOURNISSEUR", "ACHAT", "FACTURE FOURNISSEUR")):
        return "achat", 0.55
    if any(token in upper for token in ("CLIENT", "VENTE", "FACTURE CLIENT")):
        return "vente", 0.5
    return None, 0.0


def infer_payment_status(raw_text: str) -> tuple[str | None, float]:
    key = search_key(raw_text)
    if any(token in key for token in ("PAYE", "REGLE", "SOLDE")):
        return "paye", 0.65
    if any(token in key for token in ("IMPAYE", "ECHEANCE DEPASSEE")):
        return "impaye", 0.65
    if any(token in key for token in ("ECHEANCE", "A PAYER", "EN ATTENTE")):
        return "en_attente", 0.55
    return "en_attente", 0.45


def parse_decimal(value: str | None) -> float | None:
    if not value:
        return None
    try:
        clean = value.replace(" ", "").replace(",", ".")
        return float(re.sub(r"[^0-9.\-]", "", clean))
    except Exception:
        return None


def score_totals_coherence(fields: Sequence[AccountingFieldResult]) -> int:
    values = {field.key: field.value for field in fields}
    ht = parse_decimal(values.get("total_ht") or values.get("net_ht"))
    tva = parse_decimal(values.get("total_tva"))
    ttc = parse_decimal(values.get("total_ttc") or values.get("total_to_pay"))
    if ht is None or tva is None or ttc is None:
        return 50
    diff = abs((ht + tva) - ttc)
    tolerance = max(0.02, ttc * 0.015)
    return 100 if diff <= tolerance else 35


def calculate_overall_confidence(
    document_type: str,
    fields: Sequence[AccountingFieldResult],
    image_quality: dict[str, object],
) -> int:
    required = set(SUPPORTED_ACCOUNTING_TYPES[document_type]["required"])
    required_fields = [field for field in fields if field.key in required]
    if required_fields:
        required_score = sum(field.confidence if field.value else 0 for field in required_fields) / len(required_fields)
    else:
        required_score = 0

    critical_keys = {"supplier_tax_id", "client_name", "total_ht", "total_tva", "total_ttc", "total_to_pay", "articles"}
    critical = [field for field in fields if field.key in critical_keys and field.value]
    critical_score = sum(field.confidence for field in critical) / len(critical) if critical else 45
    coherence_score = score_totals_coherence(fields)
    quality_score = int(image_quality.get("score", 70))

    score = required_score * 0.55 + critical_score * 0.15 + coherence_score * 0.20 + quality_score * 0.10
    missing_required = sum(1 for field in required_fields if not field.value)
    score -= missing_required * 10
    return max(0, min(100, int(round(score))))


def build_versioned_accounting_document(
    document_type: str,
    fields: Sequence[AccountingFieldResult],
    overall_confidence: int,
    source_mode: str,
    image_quality: dict[str, object],
) -> dict[str, object]:
    values = {field.key: field.value for field in fields}
    confidence = {field.key: field.confidence for field in fields}

    def pick(*keys: str) -> str | None:
        for key in keys:
            value = values.get(key)
            if value:
                return value
        return None

    articles: list[object] = []
    raw_articles = values.get("articles")
    if raw_articles:
        try:
            parsed_articles = json.loads(raw_articles)
            if isinstance(parsed_articles, list):
                articles = parsed_articles
        except Exception:
            articles = []

    missing_fields = [
        field.key for field in fields if field.required and not field.value
    ]
    review_fields = [
        field.key for field in fields if field.requiresReview or (field.required and not field.value)
    ]

    return {
        "type": document_type,
        "sens": pick("trade_sense"),
        "statut_paiement": pick("payment_status"),
        "numero": pick("document_number", "invoice_number", "delivery_note_number", "receipt_number", "reference", "order_reference"),
        "date": pick("issue_date"),
        "echeance": pick("due_date"),
        "mode_reglement": pick("payment_method"),
        "emetteur": {
            "nom": pick("supplier_name", "merchant_name", "issuer_name"),
            "mf": pick("supplier_tax_id", "merchant_tax_id", "issuer_tax_id"),
            "adresse": pick("supplier_address", "merchant_address", "issuer_address"),
            "iban": pick("supplier_iban", "iban"),
        },
        "destinataire": {
            "nom": pick("client_name", "beneficiary_name"),
            "code_client": pick("client_code"),
            "adresse": pick("client_address"),
            "telephone": pick("client_phone"),
        },
        "articles": articles,
        "totaux": {
            "brut_ht": pick("brut_ht"),
            "remise": pick("remise_amount"),
            "net_ht": pick("net_ht", "total_ht"),
            "tva": pick("total_tva"),
            "timbre": pick("timbre"),
            "ttc": pick("total_ttc", "amount"),
            "total_a_payer": pick("total_to_pay", "total_ttc", "amount"),
            "devise": pick("currency"),
        },
        "confiance": {
            "score_global": round(overall_confidence / 100, 2),
            "score_global_percent": overall_confidence,
            "champs": confidence,
            "champs_manquants": missing_fields,
            "champs_a_verifier": review_fields,
        },
        "source": {
            "mode": source_mode,
            "qualite_image": image_quality,
        },
    }


def build_accounting_fields(document_type: str, lines: list[OcrLine], raw_text: str) -> list[AccountingFieldResult]:
    config = SUPPORTED_ACCOUNTING_TYPES[document_type]
    required_keys = set(config["required"])
    labels: dict[str, str] = config["labels"]  # type: ignore[assignment]

    zones = split_layout_zones(lines)
    header_lines = zones.get("header") or lines
    header_left = zones.get("header_left") or header_lines
    header_right = zones.get("header_right") or header_lines
    footer_lines = zones.get("footer") or lines

    supplier_name, supplier_score = guess_party_name(lines)
    issue_date, issue_date_score = first_date_line(lines, ["DATE", "EMISSION", "ÉMISSION"])
    due_date, due_date_score = first_date_line(lines, ["ECHEANCE", "ÉCHÉANCE", "DUE"])
    total_ht, total_ht_score = extract_total_generic(
        footer_lines,
        ["total ht", "brut ht", "base ht", "montant ht", "hors taxe"],
    )
    if not total_ht:
        total_ht, total_ht_score = amount_by_keywords(footer_lines, ["TOTAL HT", "HT"])

    total_tva, total_tva_score = extract_total_generic(
        footer_lines,
        ["total tva", "montant tva", "tva"],
    )
    if not total_tva:
        total_tva, total_tva_score = amount_by_keywords(footer_lines, ["TOTAL TVA", "MONTANT TVA", "TVA"])

    total_ttc, total_ttc_score = amount_by_keywords(
        footer_lines,
        ["TOTAL TTC", "TTC", "NET A PAYER", "NET À PAYER", "MONTANT TTC", "TOTAL A PAYER", "TOTAL À PAYER"],
    )
    generic_total_ttc, generic_total_ttc_score = extract_total_generic(
        footer_lines,
        ["total ttc", "net a payer", "net à payer", "montant ttc", "total a payer", "total à payer"],
    )
    if generic_total_ttc:
        total_ttc, total_ttc_score = generic_total_ttc, generic_total_ttc_score

    generic_issue_date, generic_issue_date_score = generic_label_value(
        header_lines,
        ["date", "date emission", "date livraison"],
        value_pattern=DATE_RE,
    )
    if generic_issue_date:
        issue_date, issue_date_score = generic_issue_date, generic_issue_date_score

    generic_due_date, generic_due_date_score = generic_label_value(
        header_lines,
        ["echeance", "échéance", "due date", "date echeance"],
        value_pattern=DATE_RE,
        min_label_score=0.82,
    )
    if generic_due_date:
        due_date, due_date_score = generic_due_date, generic_due_date_score
    robust_due_date, robust_due_date_score = extract_due_date_robust(header_lines, lines, issue_date)
    if robust_due_date:
        due_date, due_date_score = robust_due_date, robust_due_date_score
    if due_date and issue_date and due_date == issue_date:
        due_date, due_date_score = None, 0.0

    currency = extract_currency(raw_text)
    payment_method, payment_score = extract_payment_method_robust(header_lines, lines)
    invoice_number, invoice_score = first_line_value(lines, ["FACTURE", "INVOICE", "N°", "NUMERO"])
    receipt_number, receipt_score = first_line_value(lines, ["TICKET", "RECU", "REÇU", "REF"])
    document_number, document_number_score = extract_document_number_generic(header_lines)
    delivery_number, delivery_score = document_number, document_number_score
    if document_number:
        invoice_number, invoice_score = document_number, document_number_score
    order_reference, order_score = first_line_value(lines, ["COMMANDE", "ORDER", "REF COMMANDE"])
    amount, amount_score = amount_by_keywords(lines, ["MONTANT", "AMOUNT", "TOTAL"])
    issue_time, issue_time_score = first_time_line(lines)
    supplier_tax_id, tax_score = extract_tax_id_robust(header_left, lines)
    iban, iban_score = first_iban(lines)
    period_label, period_score = extract_period_label(lines, raw_text)
    expense_type, expense_type_score = first_line_value(lines, ["NATURE", "DEPENSE", "DÉPENSE", "TYPE"])
    expense_owner, expense_owner_score = first_line_value(lines, ["EMPLOYE", "EMPLOYE", "SALARIE", "SALARIÉ"])
    reference, reference_score = first_line_value(lines, ["REFERENCE", "RÉFÉRENCE", "REF"])
    beneficiary_name, beneficiary_score = first_line_value(lines, ["BENEFICIAIRE", "BÉNÉFICIAIRE"])
    issuer_name, issuer_score = first_line_value(lines, ["EMETTEUR", "ÉMETTEUR", "FOURNISSEUR"])
    vat_recoverable = "Oui" if contains_keyword(raw_text, ["TVA"]) else "Non"

    client_name: str | None = None
    client_score = 0.0
    client_code: str | None = None
    client_code_score = 0.0
    supplier_address, supplier_address_score = extract_address(header_left), 0.65
    client_address, client_address_score = extract_address(header_right), 0.65
    client_phone, client_phone_score = extract_phone(header_right)
    trade_sense, trade_sense_score = infer_trade_sense(document_type, raw_text)
    payment_status, payment_status_score = infer_payment_status(raw_text)
    articles, articles_score = extract_articles_json_structured(lines)
    brut_ht: str | None = None
    brut_ht_score = 0.0
    remise_amount: str | None = None
    remise_score = 0.0
    net_ht: str | None = None
    net_ht_score = 0.0
    timbre: str | None = None
    timbre_score = 0.0
    total_to_pay: str | None = None
    total_to_pay_score = 0.0

    robust_supplier, robust_supplier_score, robust_client, robust_client_score = extract_parties_robust(zones, lines)
    if robust_supplier:
        supplier_name, supplier_score = robust_supplier, robust_supplier_score
    if robust_client:
        client_name, client_score = robust_client, robust_client_score

    supplier_from_layout = extract_business_name(header_left)
    if supplier_from_layout and (not supplier_name or supplier_score < 0.86):
        supplier_name = supplier_from_layout
        supplier_score = 0.86

    client_guess, client_guess_score = guess_party_name(header_right)
    if client_guess and (not client_name or client_score < client_guess_score):
        client_name = client_guess
        client_score = client_guess_score

    supplier_tax_id_from_layout, tax_layout_score = first_regex(header_left, MF_LONG_RE)
    if supplier_tax_id_from_layout:
        supplier_tax_id, tax_score = normalize_tax_id(supplier_tax_id_from_layout), tax_layout_score

    client_code, client_code_score = generic_label_value(
        header_lines,
        ["code client", "client code", "code tiers", "compte client"],
        value_pattern=CLIENT_CODE_RE,
        min_label_score=0.68,
    )

    payment_method_from_layout, payment_layout_score = generic_label_value(
        header_lines,
        ["mode reglement", "mode règlement", "mode paiement", "payment method"],
        min_label_score=0.68,
    )
    if payment_method_from_layout and not payment_method:
        payment_key = search_key(payment_method_from_layout)
        if payment_key not in {"ECHEANCE", "COMMERCIAL", "COMMANDE", "DATE", "CODE CLIENT"}:
            payment_method, payment_score = normalize_payment_method(payment_method_from_layout), payment_layout_score

    if document_type == "bon_livraison":
        supplier_from_layout = extract_business_name(header_left)
        if supplier_from_layout and (not supplier_name or supplier_score < 0.9):
            supplier_name = supplier_from_layout
            supplier_score = 0.9

        bl_supplier_tax_id, bl_tax_score = first_regex(header_left, MF_LONG_RE)
        if bl_supplier_tax_id:
            supplier_tax_id, tax_score = normalize_tax_id(bl_supplier_tax_id), bl_tax_score

        client_guess, client_guess_score = guess_party_name(header_right)
        if client_guess and (not client_name or client_score < client_guess_score):
            client_name = client_guess
            client_score = client_guess_score

        client_code, client_code_score = generic_label_value(
            header_lines,
            ["code client", "client code", "code tiers", "compte client"],
            value_pattern=CLIENT_CODE_RE,
            min_label_score=0.68,
        )
        if not client_code:
            client_code, client_code_score = best_value_below_label(
                header_lines,
                ["CODE", "CLIENT"],
                value_pattern=re.compile(r"(\d{5,})"),
            )

        payment_method_from_layout, payment_layout_score = generic_label_value(
            header_lines,
            ["mode reglement", "mode règlement", "mode paiement", "payment method"],
            min_label_score=0.68,
        )
        if payment_method_from_layout and not payment_method:
            payment_key = search_key(payment_method_from_layout)
            if payment_key not in {"ECHEANCE", "COMMERCIAL", "COMMANDE", "DATE", "CODE CLIENT"}:
                payment_method, payment_score = normalize_payment_method(payment_method_from_layout), payment_layout_score

        brut_ht, brut_ht_score = extract_total_generic(
            footer_lines,
            ["brut ht", "total brut", "base ht", "montant ht"],
        )
        if not brut_ht:
            brut_ht, brut_ht_score = amount_by_keywords(footer_lines, ["BRUT HT", "BRUT"])

        remise_amount, remise_score = extract_total_generic(
            footer_lines,
            ["remise", "discount", "rabais"],
        )
        if not remise_amount:
            remise_amount, remise_score = amount_by_keywords(footer_lines, ["REMISE"])

        net_ht, net_ht_score = extract_total_generic(
            footer_lines,
            ["net ht", "net hors taxe"],
        )
        if not net_ht:
            net_ht, net_ht_score = amount_by_keywords(footer_lines, ["NET HT"])

        total_tva, total_tva_score = extract_total_generic(
            footer_lines,
            ["total tva", "montant tva", "tva"],
        )
        if not total_tva:
            total_tva, total_tva_score = amount_by_keywords(footer_lines, ["TOTAL TVA", "TVA"])

        timbre, timbre_score = extract_total_generic(
            footer_lines,
            ["timbre", "timbre fiscal"],
        )
        if not timbre:
            timbre, timbre_score = amount_by_keywords(footer_lines, ["TIMBRE"])

        total_ttc, total_ttc_score = extract_total_generic(
            footer_lines,
            ["total ttc", "ttc", "net a payer", "net à payer"],
        )
        if not total_ttc:
            total_ttc, total_ttc_score = amount_by_keywords(footer_lines, ["TOTAL TTC", "TTC"])

        total_to_pay, total_to_pay_score = extract_total_generic(
            footer_lines,
            ["total a payer", "total à payer", "net a payer", "net à payer"],
        )
        if not total_to_pay:
            total_to_pay, total_to_pay_score = amount_by_keywords(footer_lines, ["TOTAL À PAYER", "TOTAL A PAYER"])

        def _to_float(value: str | None) -> float | None:
            if not value:
                return None
            try:
                return float(value)
            except Exception:
                return None

        total_ref = _to_float(total_to_pay) or _to_float(total_ttc)
        if total_ref:
            tv = _to_float(total_tva)
            if tv is not None and tv > total_ref * 0.5:
                total_tva = None
                total_tva_score = 0.0

            stamp = _to_float(timbre)
            if stamp is not None and (stamp > max(10.0, total_ref * 0.05) or abs(stamp - total_ref) < 0.001):
                timbre = None
                timbre_score = 0.0

            brut_val = _to_float(brut_ht)
            net_val = _to_float(net_ht)
            if brut_val is not None and net_val is not None and brut_val <= net_val:
                brut_ht = None
                brut_ht_score = 0.0

            dims_all = page_dimensions(lines)
            if dims_all:
                width, _ = dims_all
                close_to_total = max(1.0, total_ref * 0.01)

                right_amounts: dict[float, tuple[str, float]] = {}
                for candidate_line in footer_lines:
                    cb = candidate_line.bounds()
                    if cb is None:
                        continue
                    x_mid = (cb[0] + cb[2]) / 2
                    if x_mid < 0.6 * width:
                        continue
                    match = AMOUNT_RE.search(candidate_line.text)
                    if not match:
                        continue
                    normalized = parse_amount_from_text(match.group(1)) or parse_amount_from_text(candidate_line.text)
                    if not normalized:
                        continue
                    value = _to_float(normalized)
                    if value is None:
                        continue
                    existing = right_amounts.get(value)
                    if existing is None or candidate_line.score > existing[1]:
                        right_amounts[value] = (normalized, candidate_line.score)

                values_sorted = sorted(right_amounts.keys())

                if not brut_ht:
                    brut_candidates = [v for v in values_sorted if total_ref - close_to_total <= v < total_ref]
                    if brut_candidates:
                        v = max(brut_candidates)
                        brut_ht, brut_ht_score = right_amounts[v][0], right_amounts[v][1]

                if not net_ht:
                    net_candidates = [
                        v
                        for v in values_sorted
                        if v < total_ref - close_to_total and v > total_ref * 0.4
                    ]
                    if net_candidates:
                        v = max(net_candidates)
                        net_ht, net_ht_score = right_amounts[v][0], right_amounts[v][1]

                # Infer discount and gross if labels are missing in OCR output.
                net_val = _to_float(net_ht)
                remise_val = _to_float(remise_amount)
                if brut_ht is None and net_val is not None and remise_val is not None:
                    inferred = round(net_val + remise_val, 3)
                    if abs(total_ref - inferred) <= close_to_total:
                        brut_ht = f"{inferred:.3f}".rstrip("0").rstrip(".")
                        brut_ht_score = min(max(net_ht_score, remise_score), 0.65)

                if remise_amount is None and net_val is not None:
                    best_discount: tuple[float, str, float, float] | None = None  # (diff, norm, score, brut)
                    for v in values_sorted:
                        if v <= 0 or v >= net_val:
                            continue
                        brut = net_val + v
                        if brut <= 0:
                            continue
                        diff = abs(total_ref - brut)
                        if diff > close_to_total:
                            continue
                        norm, score = right_amounts[v]
                        if best_discount is None or diff < best_discount[0]:
                            best_discount = (diff, norm, score, brut)
                    if best_discount is not None:
                        _, norm, score, brut = best_discount
                        remise_amount, remise_score = norm, score
                        if brut_ht is None:
                            brut_ht = f"{brut:.3f}".rstrip("0").rstrip(".")
                            brut_ht_score = 0.65

                # If we have `net_ht` and `total_ttc` but missing `total_tva`, infer it.
                tv = _to_float(total_tva)
                ttc_val = _to_float(total_ttc)
                if tv is None and net_val is not None and ttc_val is not None:
                    inferred = round(ttc_val - net_val, 3)
                    if inferred > 0 and inferred <= total_ref * 0.5:
                        total_tva = f"{inferred:.3f}".rstrip("0").rstrip(".")
                        total_tva_score = 0.65

                # If we have `brut_ht` and `net_ht` but missing `remise_amount`, infer it.
                brut_val = _to_float(brut_ht)
                net_val = _to_float(net_ht)
                if remise_amount is None and brut_val is not None and net_val is not None and brut_val > net_val:
                    inferred = round(brut_val - net_val, 3)
                    if inferred > 0:
                        remise_amount = f"{inferred:.3f}".rstrip("0").rstrip(".")
                        remise_score = 0.65

    values: dict[str, tuple[str | None, float]] = {
        "trade_sense": (trade_sense, trade_sense_score),
        "payment_status": (payment_status, payment_status_score),
        "document_number": (document_number, document_number_score),
        "supplier_name": (supplier_name, supplier_score),
        "supplier_tax_id": (supplier_tax_id, tax_score),
        "supplier_address": (supplier_address, supplier_address_score if supplier_address else 0.0),
        "invoice_number": (invoice_number, invoice_score),
        "issue_date": (issue_date, issue_date_score),
        "due_date": (due_date, due_date_score),
        "total_ht": (total_ht, total_ht_score),
        "total_tva": (total_tva, total_tva_score),
        "total_ttc": (total_ttc, total_ttc_score),
        "currency": (currency, 0.85 if currency else 0.0),
        "merchant_name": (supplier_name, supplier_score),
        "receipt_number": (receipt_number, receipt_score),
        "issue_time": (issue_time, issue_time_score),
        "payment_method": (payment_method, payment_score),
        "reference": (reference, reference_score),
        "amount": (amount or total_ttc, amount_score or total_ttc_score),
        "issuer_name": (issuer_name or supplier_name, issuer_score or supplier_score),
        "beneficiary_name": (beneficiary_name, beneficiary_score),
        "delivery_note_number": (delivery_number, delivery_score),
        "order_reference": (order_reference, order_score),
        "client_name": (client_name, client_score),
        "client_code": (client_code, client_code_score),
        "client_address": (client_address, client_address_score if client_address else 0.0),
        "client_phone": (client_phone, client_phone_score),
        "brut_ht": (brut_ht, brut_ht_score),
        "remise_amount": (remise_amount, remise_score),
        "net_ht": (net_ht, net_ht_score),
        "timbre": (timbre, timbre_score),
        "total_to_pay": (total_to_pay, total_to_pay_score),
        "expense_owner": (expense_owner, expense_owner_score),
        "expense_type": (expense_type, expense_type_score),
        "vat_recoverable": (vat_recoverable, 0.7),
        "iban": (iban, iban_score),
        "period_label": (period_label, period_score),
        "articles": (articles, articles_score),
    }

    return [
        field_result(
            key=key,
            label=labels[key],
            value=values.get(key, (None, 0.0))[0],
            score=values.get(key, (None, 0.0))[1],
            required=key in required_keys,
        )
        for key in labels
    ]


@app.post("/ocr/accounting/parse", response_model=AccountingOcrExtractionResult)
async def parse_accounting_document(
    file: UploadFile = File(...),
    document_type: str = Form(...),
) -> AccountingOcrExtractionResult:
    normalized_type = normalize_accounting_type(document_type)
    if normalized_type not in SUPPORTED_ACCOUNTING_TYPES:
        raise HTTPException(status_code=400, detail="Type de document comptable non supporté.")

    mime = (file.content_type or "").lower()
    if mime not in ALLOWED_MIME:
        return AccountingOcrExtractionResult(
            ocrSuccess=False,
            overallConfidence=0,
            documentType=normalized_type,
            errorMessage="Types non supportés. Utilisez JPG, PNG, WEBP ou PDF.",
        )

    file_bytes = await file.read()
    lines: list[OcrLine] = []
    source_mode = "ocr"
    image_quality: dict[str, object] = {}
    if mime == "application/pdf":
        lines = pdf_to_text_lines(file_bytes)
        if lines:
            source_mode = "native_pdf"
            image_quality = merge_image_quality([], native_pdf=True)

    if not lines:
        pages = iter_images(file_bytes, mime)
        image_quality = merge_image_quality([assess_image_quality(page) for page in pages])
        for image in pages:
            lines.extend(ocr_image(image))

    lines = dedupe_lines(lines)
    if not lines:
        return AccountingOcrExtractionResult(
            ocrSuccess=False,
            overallConfidence=0,
            documentType=normalized_type,
            errorMessage="Aucune ligne OCR détectée.",
        )

    raw_text = "\n".join(line.text for line in lines)
    normalized_type = detect_accounting_type(raw_text, normalized_type)
    fields = build_accounting_fields(normalized_type, lines, raw_text)
    if not image_quality:
        image_quality = {"score": 70, "warnings": [], "source": source_mode}
    overall_confidence = calculate_overall_confidence(normalized_type, fields, image_quality)
    missing_fields = [field.key for field in fields if field.required and not field.value]
    versioned_document = build_versioned_accounting_document(
        normalized_type,
        fields,
        overall_confidence,
        source_mode,
        image_quality,
    )

    return AccountingOcrExtractionResult(
        schemaVersion="1.0",
        schema_version="1.0",
        ocrSuccess=True,
        overallConfidence=overall_confidence,
        documentType=normalized_type,
        fields=fields,
        missingFields=missing_fields,
        document=versioned_document,
        rawText=raw_text,
        sourceMode=source_mode,
        imageQuality=image_quality,
        errorMessage=None,
    )


@app.get("/health")
def health() -> dict[str, object]:
    return {
        "status": "ok",
        "engine": "PaddleOCR",
        "languages": ["latin", "arabic"],
    }
