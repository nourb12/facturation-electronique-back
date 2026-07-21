# OCR accounting ground truth

Ce dossier contient le banc de test multi-documents pour l'OCR comptable.

## Structure

- `ground_truth/*.json` : verite terrain attendue par document.
- `samples/` : documents reels a deposer localement (PDF/images). Les chemins absolus existants sont aussi acceptes.
- `evaluate_ground_truth.py` : compare la sortie OCR avec les JSON attendus et calcule les metriques.

## Commandes

Validation des JSON uniquement :

```powershell
python tests\evaluate_ground_truth.py --ground-truth tests\ground_truth
```

Comparer des resultats OCR deja exportes :

```powershell
python tests\evaluate_ground_truth.py --ground-truth tests\ground_truth --actual-dir tests\actual
```

Appeler le service OCR local et sauvegarder les sorties :

```powershell
python tests\evaluate_ground_truth.py --ground-truth tests\ground_truth --ocr-url http://localhost:8001/ocr/accounting/parse --out-dir tests\actual
```

Le score principal est `accuracy = champs_corrects / champs_attendus`.
