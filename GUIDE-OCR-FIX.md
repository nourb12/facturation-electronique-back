# 🔧 Guide de résolution : OCR non configuré

## 🎯 Problème
- Message : "Service OCR non configuré - validation manuelle requise"
- Les champs ne se remplissent pas automatiquement depuis les documents
- Le score KYC est calculé sans les données OCR

## ✅ Solution rapide

### Étape 1 : Vérifier si Python est installé
```powershell
python --version
```
Si Python n'est pas installé, téléchargez-le depuis https://www.python.org/downloads/

### Étape 2 : Installer les dépendances OCR (première fois seulement)
```powershell
cd c:\backendpfe\einvoicing\ocr_service
python -m venv .venv
.\.venv\Scripts\activate
pip install --upgrade pip
pip install -r requirements.txt
```

### Étape 3 : Démarrer le service OCR
**Option A - Avec le script batch :**
```powershell
cd c:\backendpfe\einvoicing\ocr_service
.\START_OCR.bat
```

**Option B - Manuellement :**
```powershell
cd c:\backendpfe\einvoicing\ocr_service
.\.venv\Scripts\activate
uvicorn main:app --host 0.0.0.0 --port 8000 --reload
```

### Étape 4 : Vérifier que le service fonctionne
Ouvrez votre navigateur et allez sur : http://localhost:8000/health

Vous devriez voir :
```json
{"status": "healthy", "service": "OCR TunisFlow"}
```

### Étape 5 : Redémarrer le backend .NET
Le backend détectera automatiquement que le service OCR est disponible.

### Étape 6 : Tester
1. Allez sur la page "Demandes KYC" dans l'admin
2. Cliquez sur une demande
3. L'OCR devrait maintenant extraire les données des documents
4. Les champs devraient se remplir automatiquement

## 🔍 Vérification

### Le service OCR est-il démarré ?
```powershell
curl http://localhost:8000/health
```

### Logs du backend
Vérifiez les logs du backend .NET pour voir si l'OCR est détecté :
```
[INFO] OCR service prêt: http://localhost:8000/health
```

## 🚨 Dépannage

### Erreur : "uvicorn n'est pas reconnu"
```powershell
cd c:\backendpfe\einvoicing\ocr_service
.\.venv\Scripts\activate
pip install uvicorn
```

### Erreur : "Port 8000 déjà utilisé"
Changez le port dans `appsettings.json` :
```json
"OcrService": {
    "BaseUrl": "http://localhost:8001"
}
```

Et démarrez l'OCR sur le nouveau port :
```powershell
uvicorn main:app --host 0.0.0.0 --port 8001
```

### L'OCR ne détecte rien
- Vérifiez que les documents sont bien des images ou PDF
- Vérifiez que le texte est lisible
- Vérifiez les logs du service OCR

## 📝 Notes importantes

1. **Le service OCR doit rester démarré** pendant que vous utilisez l'application
2. **En production**, le service OCR devrait être déployé séparément (Docker, service Windows, etc.)
3. **En développement**, le backend peut auto-démarrer l'OCR si `OcrService:AutoStart` est `true`

## 🎯 Résultat attendu

Une fois l'OCR démarré :
- ✅ Le message "OCR non configuré" disparaît
- ✅ Les champs se remplissent automatiquement
- ✅ Le score de confiance OCR s'affiche
- ✅ Les comparaisons formulaire/OCR sont disponibles
