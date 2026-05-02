# 🚀 Configuration du démarrage automatique de l'OCR

## ✅ Ce qui est déjà configuré

Le backend .NET est **déjà configuré** pour démarrer automatiquement le service OCR :

1. ✅ `appsettings.Development.json` → `"AutoStart": true`
2. ✅ `Program.cs` → Appel à `OcrServiceDevLauncher.EnsureRunningAsync()`
3. ✅ Le code détecte automatiquement le dossier `ocr_service`
4. ✅ Le code détecte automatiquement l'environnement virtuel Python

## 🔧 Ce qu'il faut faire (une seule fois)

### Étape 1 : Installer les dépendances Python

**Option A - Avec le script automatique (RECOMMANDÉ) :**
```powershell
cd c:\backendpfe\einvoicing\ocr_service
.\INSTALL_OCR.bat
```

**Option B - Manuellement :**
```powershell
cd c:\backendpfe\einvoicing\ocr_service
python -m venv .venv
.\.venv\Scripts\activate
pip install --upgrade pip
pip install -r requirements.txt
```

⚠️ **Note :** L'installation peut prendre 5-10 minutes car PaddleOCR est volumineux (~500 MB).

### Étape 2 : C'est tout ! 🎉

Une fois les dépendances installées, le service OCR démarrera **automatiquement** à chaque fois que vous lancez le backend .NET.

## 🎯 Comment ça marche ?

Quand vous démarrez le backend .NET (en mode Development) :

1. Le backend vérifie si `http://localhost:8000/health` répond
2. Si non, il cherche le dossier `ocr_service`
3. Il trouve l'environnement virtuel Python (`.venv`)
4. Il démarre automatiquement : `python -m uvicorn main:app --host 127.0.0.1 --port 8000`
5. Il attend que `/health` réponde (max 10 secondes)
6. Quand vous arrêtez le backend, l'OCR s'arrête aussi automatiquement

## 📋 Vérification

### 1. Vérifier que Python est installé
```powershell
python --version
```
Devrait afficher : `Python 3.11.x` ou supérieur

### 2. Vérifier que les dépendances sont installées
```powershell
cd c:\backendpfe\einvoicing\ocr_service
.\.venv\Scripts\activate
python -c "import paddleocr; print('PaddleOCR OK')"
```

### 3. Démarrer le backend et vérifier les logs
Cherchez dans les logs du backend :
```
[INFO] Démarrage OCR service: ...
[INFO] OCR service prêt: http://localhost:8000/health
```

### 4. Tester l'OCR
Ouvrez votre navigateur : http://localhost:8000/health

Devrait afficher :
```json
{"status": "healthy", "service": "OCR TunisFlow"}
```

## 🔍 Résultat dans l'application

Une fois l'OCR configuré et démarré :

### Avant (OCR non configuré) :
- ❌ Message : "Service OCR non configuré - validation manuelle requise"
- ❌ Champs vides
- ❌ Pas de score de confiance
- ❌ Pas de comparaisons

### Après (OCR actif) :
- ✅ Message : "OCR exploitable" (si confiance > 60%)
- ✅ Champs remplis automatiquement depuis les documents
- ✅ Score de confiance affiché (ex: 87%)
- ✅ Comparaisons formulaire/OCR disponibles
- ✅ Détection automatique des incohérences

## 🚨 Dépannage

### Problème : "Python introuvable pour OCR"
**Solution :** Installez Python depuis https://www.python.org/downloads/
Assurez-vous de cocher "Add Python to PATH" pendant l'installation.

### Problème : "OCR workdir introuvable"
**Solution :** Le dossier `ocr_service` doit être au même niveau que `Einvoicing.Api`.
Structure attendue :
```
c:\backendpfe\einvoicing\
├── Einvoicing.Api/
├── Einvoicing.Application/
├── ocr_service/          ← Doit être ici
│   ├── .venv/
│   ├── main.py
│   └── requirements.txt
```

### Problème : "Échec démarrage OCR service"
**Solution :** Vérifiez que les dépendances sont installées :
```powershell
cd c:\backendpfe\einvoicing\ocr_service
.\INSTALL_OCR.bat
```

### Problème : "Port 8000 déjà utilisé"
**Solution 1 - Arrêter le processus existant :**
```powershell
netstat -ano | findstr :8000
taskkill /PID <PID> /F
```

**Solution 2 - Changer le port :**
Modifiez `appsettings.json` :
```json
"OcrService": {
    "BaseUrl": "http://localhost:8001"
}
```

### Problème : L'OCR démarre mais ne détecte rien
**Causes possibles :**
1. Document illisible ou de mauvaise qualité
2. Format non supporté (seuls JPG, PNG, PDF sont supportés)
3. Texte trop petit ou flou

**Solution :** Vérifiez les logs du service OCR pour plus de détails.

## 🎛️ Configuration avancée

### Désactiver le démarrage automatique
Dans `appsettings.Development.json` :
```json
"OcrService": {
    "AutoStart": false
}
```

### Utiliser un Python personnalisé
Dans `appsettings.Development.json` :
```json
"OcrService": {
    "PythonExe": "C:\\Python311\\python.exe"
}
```

### Utiliser un dossier OCR personnalisé
Dans `appsettings.Development.json` :
```json
"OcrService": {
    "WorkDir": "../mon_ocr_service"
}
```

## 📊 Performance

- **Premier démarrage :** ~5-10 secondes (chargement des modèles PaddleOCR)
- **Démarrages suivants :** ~2-3 secondes
- **Traitement d'un document :** ~1-3 secondes selon la taille

## 🔐 Sécurité

- ✅ Aucun document n'est envoyé vers un service cloud
- ✅ Tout le traitement OCR est local
- ✅ Les documents restent sur votre machine
- ✅ En production, déployez l'OCR séparément (Docker, service Windows, etc.)

## 📝 Notes importantes

1. **Le démarrage automatique ne fonctionne qu'en mode Development**
2. **En production, vous devez déployer l'OCR séparément**
3. **L'OCR s'arrête automatiquement quand vous arrêtez le backend**
4. **Les modèles PaddleOCR sont téléchargés au premier lancement (~500 MB)**

## ✅ Checklist finale

- [ ] Python installé et dans le PATH
- [ ] Dépendances installées (`.\INSTALL_OCR.bat`)
- [ ] Backend .NET démarre sans erreur
- [ ] Logs montrent "OCR service prêt"
- [ ] http://localhost:8000/health répond
- [ ] Page "Demandes KYC" affiche les données OCR
- [ ] Champs se remplissent automatiquement

## 🎉 Résultat

Maintenant, **à chaque fois** que vous démarrez le backend :
1. L'OCR démarre automatiquement en arrière-plan
2. Les documents sont analysés automatiquement
3. Les champs se remplissent automatiquement
4. Le scoring KYC utilise les données OCR
5. Vous gagnez un temps précieux ! ⚡
