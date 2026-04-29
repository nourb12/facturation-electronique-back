# OCR Service ? TunisFlow

Microservice OCR local pour l'analyse KYC des documents tunisiens.

## Stack
- PaddleOCR (latin + arabe)
- FastAPI
- PyMuPDF pour les PDF

## Installation locale
```powershell
python -m venv .venv
.\.venv\Scriptsctivate
pip install --upgrade pip
pip install -r requirements.txt
```

## Lancement
```powershell
.\.venv\Scriptsctivate
uvicorn main:app --host 0.0.0.0 --port 8000
```

## Endpoints
- `GET /health`
- `POST /ocr/parse`

## Formats accept?s
- JPG / JPEG
- PNG
- WEBP
- TIFF
- PDF

## R?ponse
Le service renvoie un JSON compatible avec le backend .NET (`OcrExtractionResult`) :
- `ocrSuccess`
- `confidenceScore`
- `typeDocument`
- `matriculeFiscalExtrait`
- `raisonSocialeExtraite`
- `nomGerantExtrait`
- `nomExtrait`
- `prenomExtrait`
- `cinExtrait`
- `formeJuridiqueExtraite`
- `adresseExtraite`
- `dateCreationExtraite`
- `kycScore`
- `texteBrut`
- `erreurMessage`

## Notes
- Aucun document n'est envoy? vers un service cloud.
- Le matching, la normalisation et le scoring restent dans le backend .NET.
- En cas d'?chec OCR, le dashboard admin doit basculer en v?rification manuelle.
