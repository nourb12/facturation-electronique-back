# Service ML - prediction de retard de paiement

Ce projet Python contient le modele de prediction de retard de paiement. Il est separe du backend .NET :

- `notebooks/` : exploration et entrainement pedagogique ;
- `app/` : API FastAPI qui exposera le modele au backend ;
- `models/` : modele entraine (ignore par Git) ;
- `sql/` : creation du schema PostgreSQL `ml_training`.

## Demarrage local

Dans un terminal ouvert dans ce dossier :

```powershell
py -m venv .venv
.\.venv\Scripts\Activate.ps1
pip install -r requirements.txt
Copy-Item .env.example .env
```

Dans `.env`, remplacer uniquement les valeurs `CHANGE_ME`. Ne jamais envoyer ce fichier dans Git.

Pour démarrer l'API sécurisée, définir aussi la même clé que `MachineLearning__ApiKey` côté .NET :

```powershell
$env:TUNIFLOW_ML_API_KEY = "CHANGE_ME_INTERNAL_ML_API_KEY_MIN_32_CHARS"
uvicorn app.main:app --reload --port 8001
```

Toutes les routes de calcul exigent l'en-tête interne `X-Internal-Api-Key`. Seule la route `/health` reste accessible pour la supervision.

## Entrainement reproductible

Apres avoir execute `sql/001_create_ml_training_schema.sql` sur PostgreSQL, lancer :

```powershell
.\.venv\Scripts\python.exe .\train_late_payment_model.py
```

Pour generer immediatement un jeu de donnees synthetique reproductible, sans dependance a PostgreSQL :

```powershell
.\.venv\Scripts\python.exe .\train_late_payment_model.py --source synthetic
```

Le script exporte les donnees de `ml_training.retard_features`, effectue une separation temporelle train/test et compare une regression logistique avec LightGBM. Il genere :

- `data/retard_features.csv` ;
- `reports/metrics.json` et `reports/rapport_evaluation.csv` ;
- `reports/predictions_test.csv` ;
- `models/retard_model.joblib`, le meilleur modele selon F1, puis ROC-AUC et recall.

## Exploration pedagogique

Ouvrir `notebooks/01_exploration_retards.ipynb`, selectionner l'interpreteur `.venv`, puis executer les cellules. Le notebook lit la vue `ml_training.retard_features` et affiche la repartition des retards.

## API (etape ulterieure)

```powershell
uvicorn app.main:app --reload --port 8001
```

La route `GET /health` permet de verifier que l'API Python fonctionne.
## Détection d'anomalies de transactions

Le script `train_transaction_anomaly_model.py` compare deux détecteurs non supervisés :
`Isolation Forest` et `Local Outlier Factor` (`novelty=True`). La colonne `Class` du
dataset ULB/Kaggle est strictement réservée à l'évaluation; elle n'est jamais fournie
aux modèles pendant leur entraînement.

```powershell
# Démonstration reproductible sans téléchargement externe
.\.venv\Scripts\python.exe .\train_transaction_anomaly_model.py --source synthetic

# Évaluation avec le dataset ULB/Kaggle déjà téléchargé
.\.venv\Scripts\python.exe .\train_transaction_anomaly_model.py --input C:\chemin\creditcard.csv
```

Les résultats sont exportés dans `data/transaction_anomaly_features.csv`,
`reports/anomaly_metrics.json`, `reports/anomaly_evaluation.csv`,
`reports/anomaly_predictions.csv`, `reports/anomaly_pca.csv` et le modèle final dans
`models/transaction_anomaly_model.joblib`.
# Detection des erreurs OCR

Le pipeline `train_ocr_error_model.py` genere des factures propres, injecte des erreurs OCR explicables
(caracteres confondus, date/montant invalide, champ manquant, incoherence HT + TVA = TTC), puis compare
une regression logistique et LightGBM. Le label est issu de l'injection: les resultats sont donc de
demonstration et doivent etre completes par les corrections reelles des utilisateurs avant production.

```powershell
.\.venv\Scripts\python.exe .\train_ocr_error_model.py --rows 4500
```

Le modele final est sauvegarde dans `models/ocr_error_model.joblib`. L'API expose
`POST /predict/ocr-error`, et le backend .NET le rend accessible via `POST /api/scan/ocr-error/score`.
